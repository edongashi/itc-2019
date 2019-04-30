namespace Timetabling.Common

open System

type Solution = Timetabling.Internal.Solution

type EvaluationPenalties =
  { ClassConflicts : float * float
    RoomUnavailable : float * float
    SoftPenalty : float * float
    HardConstraints : (float * float) [] }

module EvaluationPenalties =
  let private minOne v = Math.Max(1.0, v)

  let defaults count =
    { ClassConflicts = float count |> Math.Sqrt |> minOne, 0.0
      RoomUnavailable = float count |> Math.Sqrt |> minOne, 0.0
      SoftPenalty = 1.0, 0.0
      HardConstraints = Array.replicate count (1.0, 0.0) }

  let defaultsOf (problem : Problem) =
    defaults problem.Instance.HardConstraints.Length

module Solution =
  let initial problem =
    problem |> Problem.initialSolution

  let withTime (time : int) (cls : int) (solution : Solution) =
    solution.WithTime(cls, time)

  let withRoom (room : int) (cls : int) (solution : Solution) =
    solution.WithRoom(cls, room)

  let withEnrollment (cls : int) (student : int) (solution : Solution) =
    solution.WithEnrollment(student, cls)

  let hardPenalty (solution : Solution) =
    solution.HardPenalty

  let softPenalty (solution : Solution) =
    solution.SoftPenalty

  let normalizedSoftPenalty (solution : Solution) =
    solution.NormalizedSoftPenalty

  let private constraints (solution : Solution) =
    solution.HardConstraintStates()
    |> Seq.toArray

  let inline private zip (penalties : EvaluationPenalties) (solution : Solution) =
    let mutable sum = 0.0
    let constraints = penalties.HardConstraints
    let max = constraints.Length - 1
    for i in 0..max do
      sum <- sum + Math.Pow(solution.NormalizedHardConstraintPenalty(i) * (fst constraints.[i]), 2.0)
    sum

  let inline private linearZip (penalties : EvaluationPenalties) (solution : Solution) =
    let mutable sum = 0.0
    let constraints = penalties.HardConstraints
    let max = constraints.Length - 1
    for i in 0..max do
      sum <- sum + solution.NormalizedHardConstraintPenalty(i) * fst constraints.[i]
    sum

  let unscaledEuclideanPenalty2 (solution : Solution) =
    Math.Pow(solution.NormalizedClassConflicts, 2.0)
    + Math.Pow(solution.NormalizedRoomsUnavailable, 2.0)
    + Math.Pow(solution.NormalizedSoftPenalty * 0.01, 2.0)
    + solution.HardConstraintSquaredSum()

  let unscaledEuclideanPenalty s = unscaledEuclideanPenalty2 s |> Math.Sqrt

  let stats (solution : Solution) =
    {| EuclideanPenalty = solution |> unscaledEuclideanPenalty
       HardPenalty = solution.HardPenalty
       SoftPenalty = solution.SoftPenalty
       ClassConflicts = solution.ClassConflicts
       RoomsUnavailable = solution.RoomsUnavailable
       FailedHardConstraints = solution.FailedHardConstraints()
       FailedSoftConstraints = solution.FailedSoftConstraints()
       NormalizedSoftPenalty = solution.NormalizedSoftPenalty
       NormalizedClassConflicts = solution.NormalizedClassConflicts
       NormalizedRoomsUnavailable = solution.NormalizedRoomsUnavailable
       NormalizedHardConstraints = constraints solution |}

  let euclideanPenalty2 (penalties : EvaluationPenalties) (solution : Solution) =
    Math.Pow(solution.NormalizedClassConflicts * fst penalties.ClassConflicts, 2.0)
    + Math.Pow(solution.NormalizedRoomsUnavailable * fst penalties.RoomUnavailable, 2.0)
    + Math.Pow(solution.NormalizedSoftPenalty * fst penalties.SoftPenalty, 2.0)
    + zip penalties solution

  let euclideanPenalty p s = euclideanPenalty2 p s |> Math.Sqrt

  let manhattanPenalty (penalties : EvaluationPenalties) (solution : Solution) =
    solution.NormalizedClassConflicts * fst penalties.ClassConflicts
    + solution.NormalizedRoomsUnavailable * fst penalties.RoomUnavailable
    + solution.NormalizedSoftPenalty * fst penalties.SoftPenalty
    + linearZip penalties solution
