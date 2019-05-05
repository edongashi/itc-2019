namespace Timetabling.Common

open Random
open Solution
open System
open System.Threading
open Timetabling.Internal

module Solver =
  let temperatureInitial = 0.25
  let temperatureChange = 0.99999
  let temperatureSoftDivider = 500.0
  let maxTimeout = 200_000

  let inactiveDecayFlat = 0.000002
  let inactiveDecayRate = 0.5

  let hardPenalizationFlat = 0.01
  let hardPenalizationRate = 1.5

  let softPenalizationRate       = 0.95
  let softPenalizationFlat       = 0.0002
  let softPenalizationConflicts  = 0.001
  let softPenalizationAssignment = 0.0001

  let penalizeAssignment conflicts penalty =
    penalty * hardPenalizationRate + float conflicts * hardPenalizationFlat

  let pressureAssignment conflicts assignmentPenalty penalty =
    if conflicts = 0 && assignmentPenalty = 0
    then penalty * softPenalizationRate
    else penalty * softPenalizationRate
         + softPenalizationFlat
         + float conflicts * softPenalizationConflicts
         + float assignmentPenalty * softPenalizationAssignment

  let decayAssignment penalty =
    penalty * inactiveDecayRate - inactiveDecayFlat |> min0

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
              if hasTimeConflict
              then penalizeAssignment conflicts.Time p
              else if feasible
              then pressureAssignment softConflicts.Time timeAssignmentPenalty p
              else decayAssignment p
            else decayAssignment p)
          Rooms = penalties.Rooms |> Array.mapi (fun i p ->
            if hasRoomChoices && i = classRoom then
              if hasRoomConflict
              then penalizeAssignment conflicts.Room p
              else if feasible
              then pressureAssignment softConflicts.Room roomAssignmentPenalty p
              else decayAssignment p
            else decayAssignment p) }

  let scalePenalties penalties (candidate : Solution) =
    penalties |> Array.map (scaleClassPenalties candidate)

  let betterThan (s2 : Solution) (s1 : Solution) =
    s1.HardPenalty < s2.HardPenalty
    || s1.HardPenalty = s2.HardPenalty && s1.SoftPenalty < s2.SoftPenalty

  let rec nest n f x =
    if n = 0 then x
    else nest (n - 1) f (f x)

  let solve seed (cancellation : CancellationToken) problem =
    let instance = problem.Instance
    let random = Random.ofSeed seed

    let mutable penalties = ClassPenalties.defaults problem

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
      ] |> Array.ofList

    let mutate s =
      let randomCount = Math.Max(1, (nextN 15 random) - 9) - 1
      let mutable y = s
      let mutable delta = 0.0
      for _ in 0..randomCount do
        let (y', d) = y |> (nextIndex random mutations)
        delta <- delta + d
        y <- y'
      y, delta

    let mutable current = problem |> Solution.initial
    let mutable best = current
    let mutable localPenalty = System.Double.PositiveInfinity
    let mutable assignmentPenalty = dynamicPenalty penalties current
    let mutable currentPenalty = current.SearchPenalty + assignmentPenalty
    let mutable timeout = 0
    let mutable cycle = 0
    let mutable t = temperatureInitial

    while not cancellation.IsCancellationRequested do
      cycle <- cycle + 1
      if cycle % 2000 = 0 then
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
                        Tempetature = t |}
          |}

      let candidate, delta = mutate current
      let assignmentPenalty' = assignmentPenalty + delta |> min0
      let candidatePenalty = candidate.SearchPenalty + assignmentPenalty'

      if candidate |> betterThan best then
        best <- candidate

      if candidatePenalty < currentPenalty then
        localPenalty <- max localPenalty candidate.SearchPenalty

      if candidate.SearchPenalty < localPenalty then
        timeout <- 0
        localPenalty <- candidate.SearchPenalty

      let t' = if current.HardPenalty = 0
                                then t / temperatureSoftDivider
                                else t

      if candidatePenalty <= currentPenalty then
        current <- candidate
        currentPenalty <- candidatePenalty
        assignmentPenalty <- assignmentPenalty'
      else if Math.Exp((currentPenalty - candidatePenalty) / t') > next random then
        current <- candidate
        currentPenalty <- candidatePenalty
        assignmentPenalty <- assignmentPenalty'

      t <- temperatureChange * t
      timeout <- timeout + 1

      if timeout > maxTimeout then
        timeout <- 0
        t <- temperatureInitial
        penalties <- scalePenalties penalties candidate
        assignmentPenalty <- dynamicPenalty penalties current
        localPenalty <- System.Double.PositiveInfinity
        currentPenalty <- current.SearchPenalty + assignmentPenalty

    best
