namespace Timetabling.Common

open Random
open Solution
open System
open System.Threading
open Timetabling.Internal
open Timetabling.Common.Domain
open System.Diagnostics

module Solver =
  let temperatureInitial    = 1E-3
  let temperatureRestart    = 1E-4
  let temperatureChangeSlow = 0.999999
  let temperatureChangeFast = 0.99999
  let maxTimeout = 1_000_000

  let hardPenalizationFlat     = 0.008
  let hardPenalizationRate     = 1.1
  let hardPenalizationPressure = 0.0
  let hardPenalizationDecay    = 0.9

  let softPenalizationRate       = 1.0
  let softPenalizationFlat       = 0.00001
  let softPenalizationConflicts  = 0.0001
  let softPenalizationStudents   = 0.00001
  let softPenalizationAssignment = 0.0001
  let softPenalizationDecayFlat  = 0.00001
  let softPenalizationDecayRate  = 0.6

  let penalizeAssignment conflicts penalty =
    penalty * hardPenalizationRate + float conflicts * hardPenalizationFlat

  let pressureTimeAssignment (optimization: Optimization) conflicts studentConflicts assignmentPenalty penalty =
    penalty * softPenalizationRate
    + softPenalizationFlat
    + float conflicts * softPenalizationConflicts * float optimization.Distribution
    + float studentConflicts * softPenalizationStudents * float optimization.Student
    + float assignmentPenalty * softPenalizationAssignment * float optimization.Time

  let pressureRoomAssignment (optimization: Optimization) conflicts studentConflicts assignmentPenalty penalty =
    penalty * softPenalizationRate
    + softPenalizationFlat
    + float conflicts * softPenalizationConflicts * float optimization.Distribution
    + float studentConflicts * softPenalizationStudents * float optimization.Student
    + float assignmentPenalty * softPenalizationAssignment * float optimization.Room

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
                pressureTimeAssignment optimization softConflicts.Time studentPenalty timeAssignmentPenalty p
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
                pressureRoomAssignment optimization softConflicts.Room studentPenalty roomAssignmentPenalty p
              else p + hardPenalizationPressure
            else if feasible then
              decayAssignmentSoft p
            else
              decayAssignmentHard p) }

  let scalePenalties penalties (candidate : Solution) =
    penalties |> Array.map (scaleClassPenalties candidate)

  let betterThan (s2 : Solution) (s1 : Solution) =
    s1.HardPenalty < s2.HardPenalty
    || s1.HardPenalty = s2.HardPenalty && s1.SoftPenalty < s2.SoftPenalty

  let gamma = 0.2

  let fstun f f0 =
    1.0 - Math.Exp(-gamma * (f - f0))

  let solve seed (cancellation : CancellationToken) (problem : Problem) initialSolution =
    let stopwatch = Stopwatch.StartNew()

    let instance = problem.Instance
    let random = Random.ofSeed seed

    let initialPenalties = ClassPenalties.defaults problem
    let mutable penalties = initialPenalties

    let mutations =
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
      let randomCount = Math.Max(1, (nextN 7 random) - 3) - 1
      let mutable y = s
      let mutable delta = 0.0
      for _ in 0..randomCount do
        let (y', d) = y |> (nextIndex random mutations)
        delta <- delta + d
        y <- y'
      y, delta

    let mutable current = initialSolution
    let mutable best = current
    let mutable localPenalty = System.Double.PositiveInfinity
    let mutable assignmentPenalty = dynamicPenalty penalties current
    let mutable currentPenalty = current.SearchPenalty + assignmentPenalty
    let mutable timeout = 0
    let mutable cycle = 0ul
    let mutable t = if best.HardPenalty = 0
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
                        FStun = fstun (current.SearchPenalty + assignmentPenalty) best.SearchPenalty |}
          |}
        if cycle % 500_000ul = 0ul then
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

      //if candidatePenalty < currentPenalty then
      //  localPenalty <- max localPenalty candidate.SearchPenalty

      if candidatePenalty < localPenalty then
        timeout <- 0
        localPenalty <- candidatePenalty
        t <- t * temperatureChangeFast
      else
        t <- t * temperatureChangeSlow

      let f0 = best.SearchPenalty
      let nextSearch = fstun candidatePenalty f0
      let currentSearch = fstun currentPenalty f0
      if nextSearch <= currentSearch then
        //if candidatePenalty < currentPenalty then
        //  timeout <- 0
        current <- candidate
        currentPenalty <- candidatePenalty
        assignmentPenalty <- assignmentPenalty'
      else if Math.Exp((currentSearch - nextSearch) / t) > next random then
        current <- candidate
        currentPenalty <- candidatePenalty
        assignmentPenalty <- assignmentPenalty'

      timeout <- timeout + 1

      if candidate.HardPenalty < best.HardPenalty
         || candidate.HardPenalty = 0
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
        current <- best
        assignmentPenalty <- dynamicPenalty penalties current
        localPenalty <- Double.PositiveInfinity
        currentPenalty <- current.SearchPenalty + assignmentPenalty

      if candidate |> betterThan best then
        best <- candidate

      cycle <- cycle + 1ul

    best, stopwatch.Elapsed.TotalSeconds
