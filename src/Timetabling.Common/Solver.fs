namespace Timetabling.Common

open Random
open Solution
open System
open System.Threading
open Timetabling.Internal

module Solver =
  let temperatureInitial = 1E-6
  let temperatureChange = 0.999999
  let maxTimeout = 20_000

  let penaltyDecay = 0.5
  let penaltyGainFlat = 0.1
  let penaltyGainBias = 0.5
  let penaltyGainDistanceFactor = 2.0
  let penaltyGainExponent = 1.0
  let penaltyMin = 0.01
  let penaltyMax = 50_000.0

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
      Math.Min(penaltyMax, (penalty + penaltyGainFlat) * Math.Pow (1.0 + penaltyGainBias + distance * penaltyGainDistanceFactor, penaltyGainExponent)), distance

  let scalePenalties penalties (candidate : Solution) =
    { penalties with
        ClassConflicts = scale penalties.ClassConflicts candidate.NormalizedClassConflicts
        RoomUnavailable = scale penalties.RoomUnavailable candidate.NormalizedRoomsUnavailable
        SoftPenalty = scale penalties.SoftPenalty candidate.NormalizedSoftPenalty
        HardConstraints = penalties.HardConstraints |> Array.mapi (fun i c -> scale c (candidate.NormalizedHardConstraintPenalty i)) }

  let betterThan (s2 : Solution) (s1 : Solution) =
    s1.HardPenalty < s2.HardPenalty
    || s1.HardPenalty = s2.HardPenalty && s1.SoftPenalty < s2.SoftPenalty

  let rec nest n f x =
    if n = 0 then x
    else nest (n - 1) f (f x)

  let solve seed (cancellation : CancellationToken) problem =
    let instance = problem.Instance
    let random = Random.ofSeed seed

    let mutations =
      [
        if instance.TimeVariables.Length > 0 then
          yield (fun s -> Mutate.time (next random) (next random) s)
          yield (fun s -> Mutate.time (next random) (next random) s)
        if instance.RoomVariables.Length > 0 then
          yield (fun s -> Mutate.room (next random) (next random) s)
          yield (fun s -> Mutate.room (next random) (next random) s)
        if instance.StudentVariables.Length > 0 then
          yield (fun s -> Mutate.enrollment (next random) (next random) s)
      ] |> Array.ofList

    if mutations.Length = 0 then
      failwith "No variables for instance."

    let mutate s =
      let randomCount = Math.Max(1, (nextN 15 random) - 9) - 1
      let mutable y = s
      for _ in 0..randomCount do
        y <- y |> (nextIndex random mutations)
      y

    let mutable penalties = EvaluationPenalties.defaultsOf problem

    let mutable current = problem |> Solution.initial
    let mutable currentPenalty = euclideanPenalty penalties current

    let mutable best = current

    let mutable cycle = 0
    let mutable t = temperatureInitial
    let mutable timeout = 0
    let mutable localHardPenalty = System.Int32.MaxValue

    while not cancellation.IsCancellationRequested do
      cycle <- cycle + 1
      if cycle % 2000 = 0 then
        printfn "%A" (timeout, best.HardPenalty, best.SoftPenalty, t, currentPenalty, Solution.unscaledEuclideanPenalty current, current.HardPenalty, current.SoftPenalty, stats current)
        printfn "%A" penalties
        // current.PrintStats()

      let candidate = mutate current
      let candidatePenalty = euclideanPenalty penalties candidate

      if candidate |> betterThan best then
        if candidate.HardPenalty < best.HardPenalty then
          timeout <- 0
        best <- candidate

      if candidatePenalty < currentPenalty then
        localHardPenalty <- max localHardPenalty candidate.HardPenalty

      if candidatePenalty <= currentPenalty then
        current <- candidate
        currentPenalty <- candidatePenalty
        if candidate.HardPenalty < localHardPenalty then
          timeout <- 0
          localHardPenalty <- candidate.HardPenalty
      else if Math.Exp((currentPenalty - candidatePenalty) / t) > next random then
        current <- candidate
        currentPenalty <- candidatePenalty

      t <- temperatureChange * t
      timeout <- timeout + 1
      if timeout > maxTimeout then
        timeout <- 0
        penalties <- scalePenalties penalties candidate
        currentPenalty <- Solution.euclideanPenalty penalties current
        localHardPenalty <- System.Int32.MaxValue

    best
