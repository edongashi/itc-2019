namespace Timetabling.Common

open Random
open Solution
open System
open System.Threading
open Timetabling.Internal
open Timetabling.Common.Domain
open System.Diagnostics

module Solver =
  let temperatureUnfeasibleInitial = 1.0
  let temperatureUnfeasibleRestart = 0.25
  let temperatureFeasibleInitial   = 1E-3
  let temperatureFeasibleRestart   = 1E-5
  let temperatureChangeUnfeasible  = 0.9999996
  let temperatureChangeFeasible    = 0.9999998
  let maxTimeout                   = 1_000_000

  let hardPenalizationFlat     = 0.3
  let hardPenalizationRate     = 1.1
  let hardPenalizationPressure = 0.0
  let hardPenalizationDecay    = 0.9

  let softPenalizationRate       = 0.7
  let softPenalizationFlat       = 0.000002
  let softPenalizationConflicts  = 0.000005
  let softPenalizationAssignment = 0.000001
  let softPenalizationDecayFlat  = 0.000001
  let softPenalizationDecayRate  = 0.6

  let penalizeAssignment conflicts penalty =
    penalty * hardPenalizationRate + float conflicts * hardPenalizationFlat

  let pressureAssignment conflicts assignmentPenalty penalty =
    penalty * softPenalizationRate
    + softPenalizationFlat
    + float conflicts * softPenalizationConflicts
    + float assignmentPenalty * softPenalizationAssignment

  let decayAssignmentHard penalty =
    penalty * hardPenalizationDecay

  let decayAssignmentSoft penalty =
    penalty * softPenalizationDecayRate - softPenalizationDecayFlat |> min0

  let scaleClassPenalties (candidate : Solution) =
    let feasible = candidate.HardPenalty = 0
    let classes = candidate.Problem.Classes
    let conflicts = candidate.ViolatingClasses()
    let softConflicts = candidate.SoftViolatingClasses()
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

      { penalties with
          Times = penalties.Times |> Array.mapi (fun i p ->
            if hasTimeChoices && i = classTime then
              if hasTimeConflict then
                penalizeAssignment conflicts.Time p
              else if feasible then
                pressureAssignment softConflicts.Time timeAssignmentPenalty p
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
                pressureAssignment softConflicts.Room roomAssignmentPenalty p
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

    let unfeasibleMutations =
      [
        if instance.TimeVariables.Length > 0 then
          yield (fun s -> Mutate.time penalties (next random) (next random) s)
          yield (fun s -> Mutate.variable penalties (next random) (next random) s)
        if instance.RoomVariables.Length > 0 then
          yield (fun s -> Mutate.room penalties (next random) (next random) s)
          yield (fun s -> Mutate.variable penalties (next random) (next random) s)
      ] |> Array.ofList

    let mutate (s : Solution) =
      let mutationsList = if s.HardPenalty = 0 then mutations else unfeasibleMutations
      let randomCount = Math.Max(1, (nextN 14 random) - 9) - 1
      let mutable y = s
      let mutable delta = 0.0
      for _ in 0..randomCount do
        let (y', d) = y |> (nextIndex random mutationsList)
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
    let mutable t = if current.HardPenalty = 0
                    then temperatureFeasibleInitial
                    else if best.HardPenalty > 50 then temperatureUnfeasibleInitial
                    else temperatureUnfeasibleRestart

    while not cancellation.IsCancellationRequested do
      if cycle % 50_000ul = 0ul then
        printfn "%A"
          {|
            Best = {| HardPenalty = best.HardPenalty
                      SoftPenalty = best.SoftPenalty
                      SearchPenalty = best.SearchPenalty |}
            Current = {| HardPenalty = current.HardPenalty
                         SoftPenalty = current.SoftPenalty
                         SearchPenalty = current.SearchPenalty
                         ClassConflicts = current.ClassConflicts
                         RoomsUnavailable = current.RoomsUnavailable
                         AssignmentPenalty = assignmentPenalty
                         FailedConstraints = current.FailedHardConstraints() |}
            Search = {| Timeout = timeout
                        Temperature = t
                        Time = stopwatch.Elapsed.TotalSeconds |}
          |}
        if cycle % 5_000_000ul = 0ul then
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

      if candidatePenalty < currentPenalty then
        localPenalty <- max localPenalty candidate.SearchPenalty

      if candidate.SearchPenalty < localPenalty then
        timeout <- 0
        localPenalty <- candidate.SearchPenalty

      if candidatePenalty <= currentPenalty then
        if candidatePenalty < currentPenalty then
          timeout <- 0
        current <- candidate
        currentPenalty <- candidatePenalty
        assignmentPenalty <- assignmentPenalty'
      else if Math.Exp((currentPenalty - candidatePenalty) / t) > next random then
        current <- candidate
        currentPenalty <- candidatePenalty
        assignmentPenalty <- assignmentPenalty'

      t <- t * if current.HardPenalty = 0
               then temperatureChangeFeasible
               else temperatureChangeUnfeasible
      timeout <- timeout + 1

      if candidate.HardPenalty < best.HardPenalty
         || candidate.HardPenalty = 0
         && candidate.SoftPenalty < best.SoftPenalty then
        timeout <- 0
        current <- candidate
        penalties <- initialPenalties
        assignmentPenalty <- 0.0
        localPenalty <- System.Double.PositiveInfinity
        currentPenalty <- current.SearchPenalty
      else if timeout > maxTimeout then
        timeout <- 0
        t <- if current.HardPenalty = 0
             then temperatureFeasibleRestart
             else temperatureUnfeasibleRestart
        penalties <- scalePenalties penalties candidate
        assignmentPenalty <- dynamicPenalty penalties current
        localPenalty <- System.Double.PositiveInfinity
        currentPenalty <- current.SearchPenalty + assignmentPenalty

      if candidate |> betterThan best then
        if candidate.HardPenalty = 0 && best.HardPenalty > 0 then
          t <- temperatureFeasibleInitial
        best <- candidate

      cycle <- cycle + 1ul

    best, stopwatch.Elapsed.TotalSeconds
