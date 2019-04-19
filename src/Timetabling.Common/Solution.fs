namespace Timetabling.Common
open Timetabling

type ProblemWrapper =
  { Problem : Problem
    Instance : Internal.Problem
    IdMapping : IdMapping }

module ProblemWrapper =
  let wrap problem =
    let ids, instance = Convert.fromProblem problem
    { Problem = problem
      Instance = instance
      IdMapping = ids }

  let initialSolution p = p.Instance.InitialSolution

open Timetabling.Internal

type EvaluationPenalties =
  { ClassConflicts : double
    RoomUnavailable : double
    SoftPenalty : double
    HardContraints : double [] }

module EvaluationPenalties =
  let defaults count =
    { ClassConflicts = 1.0
      RoomUnavailable = 1.0
      SoftPenalty = 1.0
      HardContraints = Array.replicate count 1.0 }

module Solution =
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

  open System

  let private constraints (solution : Solution) =
    solution.HardConstraintStates()
    |> Seq.toArray

  let inline private zip (penalties : EvaluationPenalties) (solution : Solution) =
    let mutable sum = 0.0
    let constraints = penalties.HardContraints
    let count = constraints.Length
    for i in 0..count do
      sum <- sum + Math.Pow(solution.NormalizedHardConstraintPenalty(i) * constraints.[i], 2.0)
    sum

  let unscaledEuclideanPenalty (solution : Solution) =
    Math.Sqrt(
      Math.Pow(solution.NormalizedClassConflicts, 2.0)
    + Math.Pow(solution.NormalizedRoomsUnavailable, 2.0)
    + Math.Pow(solution.NormalizedSoftPenalty, 2.0)
    + solution.HardConstraintSquaredSum())

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

  let euclideanPenalty (penalties : EvaluationPenalties) (solution : Solution) =
    Math.Sqrt(
      Math.Pow(solution.NormalizedClassConflicts * penalties.ClassConflicts, 2.0)
    + Math.Pow(solution.NormalizedRoomsUnavailable * penalties.RoomUnavailable, 2.0)
    + Math.Pow(solution.NormalizedSoftPenalty * penalties.SoftPenalty, 2.0)
    + zip penalties solution)
