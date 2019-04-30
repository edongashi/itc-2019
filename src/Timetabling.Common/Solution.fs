namespace Timetabling.Common

open System

type Solution = Timetabling.Internal.Solution

type EvaluationPenalties =
  { ClassConflicts : float
    RoomUnavailable : float
    SoftPenalty : float
    HardConstraints : float [] }

module EvaluationPenalties =
  let private minOne v = Math.Max(1.0, v)

  let defaults count =
    { ClassConflicts = float count |> Math.Sqrt |> minOne
      RoomUnavailable = float count |> Math.Sqrt |> minOne
      SoftPenalty = 1.0
      HardConstraints = Array.replicate count 1.0 }

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
    let count = constraints.Length - 1
    for i in 0..count do
      sum <- sum + Math.Pow(solution.NormalizedHardConstraintPenalty(i) * constraints.[i], 2.0)
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
    Math.Pow(solution.NormalizedClassConflicts * penalties.ClassConflicts, 2.0)
    + Math.Pow(solution.NormalizedRoomsUnavailable * penalties.RoomUnavailable, 2.0)
    + Math.Pow(solution.NormalizedSoftPenalty * penalties.SoftPenalty, 2.0)
    + zip penalties solution

  let euclideanPenalty p s = euclideanPenalty2 p s |> Math.Sqrt
