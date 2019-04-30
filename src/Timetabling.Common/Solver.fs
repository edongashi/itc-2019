namespace Timetabling.Common

open Random
open Solution
open System
open System.Threading
open Timetabling.Internal

module Solver =
  let temperatureInitial = 0.001
  let temperatureChange = 0.99999
  let maxTimeout = 50_000

  let penaltyDecay = 0.5
  let penaltyGainFlat = 0.05
  let penaltyGainBias = 0.0
  let penaltyGainDistanceFactor = 1.0
  let penaltyGainExponent = 1.0
  let penaltyMin = 0.01

  let private scale (penalty : float) (distance : float) =
    if distance <= 0.0 then
      Math.Max(penaltyMin, penalty * penaltyDecay)
    else
      (penalty + penaltyGainFlat) * Math.Pow (1.0 + penaltyGainBias + distance * penaltyGainDistanceFactor, penaltyGainExponent)

  let scalePenalties penalties (candidate : Solution) =
    { penalties with
        ClassConflicts = scale penalties.ClassConflicts candidate.NormalizedClassConflicts
        RoomUnavailable = scale penalties.RoomUnavailable candidate.NormalizedRoomsUnavailable
        SoftPenalty = scale penalties.SoftPenalty candidate.NormalizedSoftPenalty
        HardConstraints = penalties.HardConstraints |> Array.mapi (fun i c -> scale c (candidate.NormalizedHardConstraintPenalty i)) }

  let betterThan (s2 : Solution) (s1 : Solution) =
    s1.HardPenalty < s2.HardPenalty
    || s1.HardPenalty = s2.HardPenalty && s1.SoftPenalty <= s2.SoftPenalty

  let rec nest n f x =
    if n=0 then x
    else nest (n-1) f (f x)

  let bestOf (s2 : Solution) (s1 : Solution) =
    if s2 |> betterThan s1 then s2
    else s1

  let solve seed (cancellation : CancellationToken) problem =
    let instance = problem.Instance
    let random = Random.ofSeed seed

    let mutations =
      [
        if instance.TimeVariables.Length > 0 then
          yield (fun s -> Mutate.time (next random) (next random) s)
        if instance.RoomVariables.Length > 0 then
          yield (fun s -> Mutate.room (next random) (next random) s)
        if instance.StudentVariables.Length > 0 then
          yield (fun s -> Mutate.enrollment (next random) (next random) s)
      ] |> Array.ofList

    if mutations.Length = 0 then
      failwith "No variables for instance."

    let mutate s =
      let randomCount = Math.Max(1, (nextN 7 random) - 3) - 1
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

    while not cancellation.IsCancellationRequested do
      cycle <- cycle + 1
      if cycle % 2000 = 0 then
        printfn "%A" (timeout, t, currentPenalty, best.HardPenalty, Solution.unscaledEuclideanPenalty current, current.HardPenalty, current.SoftPenalty, stats current)
        printfn "%A" penalties

      let candidate = mutate current
      let candidatePenalty = euclideanPenalty penalties candidate

      if candidate |> betterThan best then
        best <- candidate

      if candidatePenalty < currentPenalty then
        timeout <- 0

      if candidatePenalty <= currentPenalty then
        current <- candidate
        currentPenalty <- candidatePenalty

      // else if Math.Exp((currentPenalty - candidatePenalty) / t) > next random then
      //   current <- candidate
      //   currentPenalty <- candidatePenalty

      t <- temperatureChange * t
      timeout <- timeout + 1
      if timeout > maxTimeout then
        timeout <- 0
        penalties <- scalePenalties penalties candidate
        currentPenalty <- Solution.euclideanPenalty penalties current

    best
