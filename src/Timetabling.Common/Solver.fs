namespace Timetabling.Common

open Random
open Solution
open System
open System.Threading
open Timetabling.Internal

module Solver =
  let temperatureInitial = 0.1
  let temperatureChange = 0.99995
  let maxTimeout = 100_000

  let penaltyDecay = 0.5
  let penaltyGainFlat = 0.1
  let penaltyGainRate = 1.1
  let penaltyGainDistanceFactor = 1.0
  let penaltyMin = 0.01
  let penaltyMax = 50_000.0

  let assignmentInactiveDecayFlat = 0.01
  let assignmentInactiveDecayRate = 0.8
  let assignmentPenalizedGainFlat = 0.01
  let assignmentPenalizedGainRate = 1.5
  let assignmentCurrentGainFlat = 0.00005
  let assignmentCurrentGainRate = 0.5

  let private clamp v =
    if v <= 0.5 then 0.5
    else if v >= 1.0 then 1.0
    else v

  let private scale (penalty : float, prevDistance : float) (distance : float) =
    if distance <= 0.0 then
      Math.Max(penaltyMin, penalty * penaltyDecay), distance
    else if distance < prevDistance then
      penalty * clamp(1.0 - (prevDistance - distance)), distance
    else
      Math.Min(penaltyMax, penalty * penaltyGainRate + penaltyGainFlat + distance * penaltyGainDistanceFactor), distance

  let penalizeAssignment conflicts penalty =
    penalty * assignmentPenalizedGainRate + float conflicts * assignmentPenalizedGainFlat

  let pressureAssignment penalty =
    penalty * assignmentCurrentGainRate + assignmentCurrentGainFlat

  let decayAssignment penalty =
    penalty * assignmentInactiveDecayRate - assignmentInactiveDecayFlat |> min0

  let scaleClassPenalties (candidate : Solution) =
    let conflicts = candidate.ViolatingClasses()
    fun (penalties : ClassPenalties) ->
      let cls = penalties.Class
      let classTime = candidate.GetTimeIndex cls
      let classRoom = candidate.GetRoomIndex cls
      let hasTimeChoices = penalties.Times.Length > 1
      let hasRoomChoices = penalties.Rooms.Length > 1
      let conflicts =
        if conflicts.ContainsKey(cls)
        then conflicts.[cls]
        else ClassConflicts(0, 0)

      let hasTimeConflict = conflicts.Time > 0
      let hasRoomConflict = conflicts.Room > 0

      { penalties with
          Times = penalties.Times |> Array.mapi (fun i p ->
            if hasTimeChoices && i = classTime then
              if hasTimeConflict
              then penalizeAssignment conflicts.Time p
              else pressureAssignment p
            else decayAssignment p)
          Rooms = penalties.Rooms |> Array.mapi (fun i p ->
            if hasRoomChoices && i = classRoom then
              if hasRoomConflict
              then penalizeAssignment conflicts.Room p
              else pressureAssignment p
            else decayAssignment p) }

  let scalePenalties penalties (candidate : Solution) =
    { penalties with
        ClassConflicts = scale penalties.ClassConflicts candidate.NormalizedClassConflicts
        RoomUnavailable = scale penalties.RoomUnavailable candidate.NormalizedRoomsUnavailable
        SoftPenalty = scale penalties.SoftPenalty candidate.NormalizedSoftPenalty
        HardConstraints = penalties.HardConstraints |> Array.mapi (fun i c -> scale c (candidate.NormalizedHardConstraintPenalty i))
        ClassPenalties = penalties.ClassPenalties |> Array.map (scaleClassPenalties candidate) }

  let betterThan (s2 : Solution) (s1 : Solution) =
    s1.HardPenalty < s2.HardPenalty
    || s1.HardPenalty = s2.HardPenalty && s1.SoftPenalty < s2.SoftPenalty

  let rec nest n f x =
    if n = 0 then x
    else nest (n - 1) f (f x)

  let solve seed (cancellation : CancellationToken) problem =
    let instance = problem.Instance
    let random = Random.ofSeed seed

    let mutable penalties = EvaluationPenalties.defaults problem

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
    let mutable localHardPenalty = System.Int32.MaxValue
    let mutable assignmentPenalty = classPenalty penalties current
    let mutable currentPenalty = float current.HardPenalty + assignmentPenalty // + manhattanPenalty penalties current
    let mutable timeout = 0
    let mutable cycle = 0
    let mutable t = temperatureInitial

    while not cancellation.IsCancellationRequested do
      cycle <- cycle + 1
      if cycle % 2000 = 0 then
        printfn "%A"
          {|
            Best = best.HardPenalty
            Current = {| ManhattanPenalty = currentPenalty
                         HardPenalty = current.HardPenalty
                         SoftPenalty = current.SoftPenalty
                         ClassConflicts = current.ClassConflicts
                         RoomsUnavailable = current.RoomsUnavailable
                         AssignmentPenalty = assignmentPenalty
                         FailedConstraints = current.FailedHardConstraints() |}
            Search = {| Timeout = timeout
                        Tempetature = t |}
            Penalties = {| SoftPenalty = penalties.SoftPenalty
                           ClassConflicts = penalties.ClassConflicts
                           RoomUnavailable = penalties.RoomUnavailable
                           HardConstraints = penalties.HardConstraints |}
          |}
      // if current.HardPenalty = 0 then
      //   System.Console.ReadLine() |> ignore

      let candidate, delta = mutate current
      let assignmentPenalty' = assignmentPenalty + delta |> min0

      let candidatePenalty = float candidate.HardPenalty + assignmentPenalty' // + manhattanPenalty penalties candidate

      if candidate |> betterThan best then
        if candidate.HardPenalty < best.HardPenalty then
          timeout <- 0
        best <- candidate

      if candidatePenalty < currentPenalty then
        localHardPenalty <- max localHardPenalty candidate.HardPenalty

      if candidatePenalty <= currentPenalty then
        current <- candidate
        currentPenalty <- candidatePenalty
        if candidate.HardPenalty < localHardPenalty
           || candidate.HardPenalty = localHardPenalty && assignmentPenalty' < assignmentPenalty then
          timeout <- 0
          localHardPenalty <- candidate.HardPenalty
        assignmentPenalty <- assignmentPenalty'
      else if Math.Exp((currentPenalty - candidatePenalty) / t) > next random then
        current <- candidate
        currentPenalty <- candidatePenalty
        assignmentPenalty <- assignmentPenalty'

      t <- temperatureChange * t
      timeout <- timeout + 1
      if timeout > maxTimeout then
        timeout <- 0
        t <- temperatureInitial
        penalties <- scalePenalties penalties candidate
        assignmentPenalty <- classPenalty penalties current
        localHardPenalty <- System.Int32.MaxValue
        currentPenalty <- float current.HardPenalty + assignmentPenalty // + manhattanPenalty penalties current

    best
