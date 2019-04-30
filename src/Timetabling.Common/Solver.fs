namespace Timetabling.Common

module Solver =
  type private State =
    { Problem : Problem
      Solution : Solution }

  open System
  open System.Threading
  open Random
  open Solution

  let temperatureInitial = 0.1
  let temperatureChange = 0.99999

  let solve seed (cancellation : CancellationToken) problem =
    let random = Random.ofSeed seed
    let initial = Solution.initial problem

    let mutable current = initial
    let mutable currentPenalty = unscaledEuclideanPenalty2 initial

    let mutable best = current
    let mutable bestPenalty = currentPenalty

    let mutable cycle = 0
    let mutable t = temperatureInitial

    while not cancellation.IsCancellationRequested do
      cycle <- cycle + 1
      if cycle % 2000 = 0 then
        printfn "%A" (t, stats current)

      t <- temperatureChange * t
      let candidate = current |> Mutate.variable (next random) (next random)
      let candidatePenalty = unscaledEuclideanPenalty2 candidate

      if candidatePenalty < bestPenalty then
        best <- candidate
        bestPenalty <- candidatePenalty

      if candidatePenalty < currentPenalty then
        current <- candidate
        currentPenalty <- candidatePenalty
      else if Math.Exp((currentPenalty - candidatePenalty) / t) > next random then
        current <- candidate
        currentPenalty <- candidatePenalty

    best
