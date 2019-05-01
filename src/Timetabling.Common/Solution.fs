namespace Timetabling.Common

open Timetabling.Internal.Specialized

type Solution = Timetabling.Internal.Solution

type ClassPenalties =
  { Class : int
    Rooms : float []
    Times : float [] }

type EvaluationPenalties =
  { ClassConflicts : float * float
    RoomUnavailable : float * float
    SoftPenalty : float * float
    HardConstraints : (float * float) []
    ClassPenalties : ClassPenalties [] }

module EvaluationPenalties =
  let private minOne v = System.Math.Max(1.0, v)

  let private constant n = fun _ -> n

  let defaults (problem : Problem) =
    let instance = problem.Instance
    let count = instance.HardConstraints.Length
    { ClassConflicts = count |> double |> (*) 2.0 |> sqrt, 0.0
      RoomUnavailable = count |> double |> (*) 2.0 |> sqrt, 0.0
      SoftPenalty = 0.1, 0.0
      HardConstraints = Array.replicate count (1.0, 0.0)
      ClassPenalties =
        instance.Classes
        |> Array.mapi (fun i c -> { Class = i
                                    Rooms = c.PossibleRooms |> Array.map (constant 0.0)
                                    Times = c.PossibleSchedules |> Array.map (constant 0.0) }) }

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
      sum <- sum + (solution.NormalizedHardConstraintPenalty(i) |> square) * (fst constraints.[i])
    sum

  let inline private linearZip (penalties : EvaluationPenalties) (solution : Solution) =
    let mutable sum = 0.0
    let constraints = penalties.HardConstraints
    let max = constraints.Length - 1
    for i in 0..max do
      sum <- sum + solution.NormalizedHardConstraintPenalty(i) * fst constraints.[i]
    sum

  let unscaledEuclideanPenalty2 (solution : Solution) =
    square solution.NormalizedClassConflicts
    + square solution.NormalizedRoomsUnavailable
    + square solution.NormalizedSoftPenalty
    + solution.HardConstraintSquaredSum()

  let unscaledEuclideanPenalty s = unscaledEuclideanPenalty2 s |> sqrt

  let euclideanPenalty2 (penalties : EvaluationPenalties) (solution : Solution) =
    square solution.NormalizedClassConflicts * fst penalties.ClassConflicts
    + square solution.NormalizedRoomsUnavailable * fst penalties.RoomUnavailable
    + square solution.NormalizedSoftPenalty * fst penalties.SoftPenalty
    + zip penalties solution

  let euclideanPenalty p s = euclideanPenalty2 p s |> sqrt

  let manhattanPenalty (penalties : EvaluationPenalties) (solution : Solution) =
    solution.NormalizedClassConflicts * fst penalties.ClassConflicts
    + solution.NormalizedRoomsUnavailable * fst penalties.RoomUnavailable
    + solution.NormalizedSoftPenalty * fst penalties.SoftPenalty
    + linearZip penalties solution

  let classPenalty (penalties : EvaluationPenalties) (solution : Solution) =
    let variablePenalty (variable : Variable) =
      let cls = variable.Class
      let classState = penalties.ClassPenalties.[cls]
      if variable.Type = VariableType.Room then
        classState.Rooms.[solution.GetRoomIndex(cls)]
      else
        classState.Times.[solution.GetTimeIndex(cls)]

    solution.Problem.AllClassVariables
    |> Array.sumBy variablePenalty

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
