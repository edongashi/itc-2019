namespace Timetabling.Common

open Random
open Solution
open System
open System.Threading
open Timetabling.Internal
open Timetabling.Common.Domain
open System.Diagnostics

module Solver =
  let temperatureInitial    = 3E-3
  let temperatureRestart    = 9E-4
  let temperatureChange     = 0.999997
  let temperatureChangeSlow = 0.9999995
  let maxTimeout = 1_000_000

  let hardPenalizationFlat     = 0.003
  let hardPenalizationRate     = 1.1
  let hardPenalizationPressure = 0.0
  let hardPenalizationDecay    = 0.9

  let softPenalizationRate         = 1.1
  let softPenalizationFlat         = 1.0
  let softPenalizationConflicts    = 0.0
  let softPenalizationStudentsTime = 0.0
  let softPenalizationStudentsRoom = 0.0
  let softPenalizationAssignment   = 0.0
  let softPenalizationDecayFlat    = 0.0
  let softPenalizationDecayRate    = 0.6

  let penalizeAssignment conflicts penalty =
    penalty * hardPenalizationRate + float conflicts * hardPenalizationFlat

  let pressureTimeAssignment divisor (optimization: Optimization) conflicts studentConflicts assignmentPenalty penalty =
    penalty * softPenalizationRate +
    (
      softPenalizationFlat
      + float conflicts * softPenalizationConflicts * float optimization.Distribution
      + float studentConflicts * softPenalizationStudentsTime * float optimization.Student
      + float assignmentPenalty * softPenalizationAssignment * float optimization.Time
    ) / divisor

  let pressureRoomAssignment divisor (optimization: Optimization) conflicts studentConflicts assignmentPenalty penalty =
    penalty * softPenalizationRate +
    (
      softPenalizationFlat
      + float conflicts * softPenalizationConflicts * float optimization.Distribution
      + float studentConflicts * softPenalizationStudentsRoom * float optimization.Student
      + float assignmentPenalty * softPenalizationAssignment * float optimization.Room
    ) / divisor

  let decayAssignmentHard penalty =
    penalty * hardPenalizationDecay

  let decayAssignmentSoft penalty =
    penalty * softPenalizationDecayRate - softPenalizationDecayFlat |> min0

  let scaleClassPenalties (candidate : Solution) =
    let problem = candidate.Problem
    let optimization = { Time = problem.TimePenalty
                         Room = problem.RoomPenalty
                         Distribution = problem.DistributionPenalty
                         Student = problem.StudentPenalty }
    let divisor = problem.WorstSoftPenalty
    let feasible = candidate.HardPenalty = 0
    let classes = candidate.Problem.Classes
    let conflicts = candidate.ViolatingClasses()
    let softConflicts = candidate.SoftViolatingClasses()
    let studentConflicts = candidate.StudentConflictingClasses()
    fun (penalties : ClassPenalties) ->
      let cls = penalties.Class
      let classData = classes.[cls]
      let classTime = candidate.GetTimeIndex cls
      let classRoom = candidate.GetRoomIndex cls
      let hasTimeChoices = penalties.Times.Length > 1
      let hasRoomChoices = penalties.Rooms.Length > 1

      let timeAssignmentPenalty =
        classData.PossibleSchedules.[classTime].Penalty - classData.MinTimePenalty

      let roomAssignmentPenalty =
        if classRoom >= 0
        then classData.PossibleRooms.[classRoom].Penalty - classData.MinRoomPenalty
        else 0

      let conflicts =
        if conflicts.ContainsKey(cls)
        then conflicts.[cls]
        else ClassConflicts(0, 0)

      let softConflicts =
        if softConflicts.ContainsKey(cls)
        then softConflicts.[cls]
        else ClassConflicts(0, 0)

      let hasTimeConflict = conflicts.Time > 0
      let hasRoomConflict = conflicts.Room > 0

      let studentPenalty =
        if studentConflicts.ContainsKey cls
        then studentConflicts.[cls]
        else 0

      { penalties with
          Times = penalties.Times |> Array.mapi (fun i p ->
            if hasTimeChoices && i = classTime then
              if hasTimeConflict then
                penalizeAssignment conflicts.Time p
              else if feasible then
                pressureTimeAssignment divisor optimization softConflicts.Time studentPenalty timeAssignmentPenalty p
              else p + hardPenalizationPressure
            else if feasible then
              decayAssignmentSoft p
            else
              decayAssignmentHard p)
          Rooms = penalties.Rooms |> Array.mapi (fun i p ->
            if hasRoomChoices && i = classRoom then
              if hasRoomConflict then
                penalizeAssignment conflicts.Room p
              else if feasible then
                pressureRoomAssignment divisor optimization softConflicts.Room studentPenalty roomAssignmentPenalty p
              else p + hardPenalizationPressure
            else if feasible then
              decayAssignmentSoft p
            else
              decayAssignmentHard p) }

  let scalePenalties penalties (candidate : Solution) =
    penalties |> Array.map (scaleClassPenalties candidate)

  let betterThan (s2 : Solution) (s1 : Solution) =
    s1.HardPenalty < s2.HardPenalty || s1.SearchPenalty < s2.SearchPenalty

  let gamma = 0.9

  let fstun f f0 =
    1.0 - Math.Exp(-gamma * (f - f0))

  let solve seed (cancellation : CancellationToken) (problem : Problem) initialSolution =
    let stopwatch = Stopwatch.StartNew()

    let instance = problem.Instance
    let random = Random.ofSeed seed

    let initialPenalties = ClassPenalties.defaults problem
    let mutable penalties = initialPenalties
    
    let infeasibleMutations =
      [
        if instance.TimeVariables.Length > 0 then
          yield (fun s -> Mutate.time penalties (next random) (next random) s)
          yield (fun s -> Mutate.variable penalties (next random) (next random) s)
        if instance.RoomVariables.Length > 0 then
          yield (fun s -> Mutate.room penalties (next random) (next random) s)
          yield (fun s -> Mutate.variable penalties (next random) (next random) s)
      ] |> Array.ofList

    let feasibleMutations =
      [
        if instance.TimeVariables.Length > 0 then
          yield (fun s -> Mutate.time penalties (next random) (next random) s)
          yield (fun s -> Mutate.variable penalties (next random) (next random) s)
        if instance.RoomVariables.Length > 0 then
          yield (fun s -> Mutate.room penalties (next random) (next random) s)
          yield (fun s -> Mutate.variable penalties (next random) (next random) s)
        if instance.StudentVariables.Length > 0 then
          yield (fun s -> Mutate.enrollment (next random) (next random) s)
          yield (fun s -> Mutate.enrollment (next random) (next random) s)
      ] |> Array.ofList

    let mutate (s : Solution) =
      let mutations = if s.HardPenalty = 0 then feasibleMutations else infeasibleMutations
      let randomCount = Math.Max(1, (nextN 7 random) - 3) - 1
      let mutable y = s
      let mutable delta = 0.0
      for _ in 0..randomCount do
        let (y', d) = y |> (nextIndex random mutations)
        delta <- delta + d
        y <- y'
      y, delta

    let maxTimeout = maxTimeout + 100 * problem.Instance.AllClassVariables.Length
    let mutable current = initialSolution
    let mutable best = current
    let mutable localPenalty = System.Double.PositiveInfinity
    let mutable localBan = 1_000_000
    let mutable assignmentPenalty = dynamicPenalty penalties current
    let mutable currentPenalty = current.SearchPenalty + assignmentPenalty
    let mutable timeout = 0
    let mutable cycle = 0ul
    let mutable t = if best.HardPenalty < 50
                    then temperatureRestart
                    else temperatureInitial

    while not cancellation.IsCancellationRequested do
      if cycle % 50_000ul = 0ul then
        printfn "%A"
          {|
            Best = best |> stats
            Current = current |> stats
            Search = {| Timeout = timeout
                        Temperature = t
                        Time = stopwatch.Elapsed.TotalSeconds
                        AssignmentPenalty = assignmentPenalty
                        FStun = fstun (current.SearchPenalty + assignmentPenalty) best.SearchPenalty
                        MaxTimeout = maxTimeout|}
          |}
        if cycle % 2_000_000ul = 0ul then
          printfn "Saving backup..."
          best
          |> Solution.serialize
              SolverInfo.defaults
              problem
              seed
              stopwatch.Elapsed.TotalSeconds
          |> fun xml -> xml.Save(sprintf "solution_%s_%d.xml" instance.Name seed)

      let candidate, delta = mutate current
      let assignmentPenalty' = assignmentPenalty + delta |> min0
      let candidatePenalty = candidate.SearchPenalty + assignmentPenalty'
      
      if localBan > 0 then
        localBan <- localBan - 1

      if candidatePenalty < localPenalty then
        timeout <- 0
        if localBan = 0 then 
          localPenalty <- candidatePenalty

      if candidatePenalty <= currentPenalty then
        if candidatePenalty < currentPenalty then
          timeout <- 0
        current <- candidate
        currentPenalty <- candidatePenalty
        assignmentPenalty <- assignmentPenalty'
      else 
        let f0 = best.SearchPenalty
        let nextSearch = fstun candidatePenalty f0
        let currentSearch = fstun currentPenalty f0
        if nextSearch < 0.5 && Math.Exp((currentSearch - nextSearch) / t) > next random then
          current <- candidate
          currentPenalty <- candidatePenalty
          assignmentPenalty <- assignmentPenalty'

      timeout <- timeout + 1
      if current.HardPenalty = 0 || candidatePenalty < currentPenalty then
        t <- t * temperatureChange
      else
        t <- t * temperatureChangeSlow

      if candidate.HardPenalty < best.HardPenalty
         || candidate.HardPenalty = 0
         && candidate.ClassOverflows <= best.ClassOverflows
         && candidate.SoftPenalty < best.SoftPenalty then
        timeout <- 0
        current <- candidate
        penalties <- initialPenalties
        assignmentPenalty <- 0.0
        localPenalty <- Double.PositiveInfinity
        currentPenalty <- current.SearchPenalty
      else if timeout > maxTimeout then
        timeout <- 0
        t <- temperatureRestart
        penalties <- scalePenalties penalties current
        assignmentPenalty <- dynamicPenalty penalties current
        localPenalty <- Double.PositiveInfinity
        localBan <- 100_000
        currentPenalty <- current.SearchPenalty + assignmentPenalty

      if candidate |> betterThan best then
        best <- candidate

      cycle <- cycle + 1ul

    best, stopwatch.Elapsed.TotalSeconds
