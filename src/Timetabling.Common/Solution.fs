namespace Timetabling.Common
open Timetabling.Common.Domain
open Timetabling.Internal.Specialized

type Solution = Timetabling.Internal.Solution

type ClassPenalties =
  { Class : int
    Rooms : float []
    Times : float [] }

module ClassPenalties =
  let private constant n = fun _ -> n

  let defaults (problem : Problem) =
    problem.Instance.Classes
    |> Array.mapi (fun i c -> { Class = i
                                Rooms = c.PossibleRooms |> Array.map (constant 0.0)
                                Times = c.PossibleSchedules |> Array.map (constant 0.0) })

module Solution =
  let initial problem =
    problem |> Problem.initialSolution

  let withTime (time : int) (cls : int) (solution : Solution) =
    solution.WithTime(cls, time)

  let withRoom (room : int) (cls : int) (solution : Solution) =
    solution.WithRoom(cls, room)

  let withEnrollment (cls : int) (student : int) (solution : Solution) =
    solution.WithEnrollment(student, cls)

  let private constraints (solution : Solution) =
    solution.HardConstraintStates()
    |> Seq.toArray

  let dynamicPenalty (penalties : ClassPenalties []) (solution : Solution) =
    let variablePenalty (variable : Variable) =
      let cls = variable.Class
      let classState = penalties.[cls]
      if variable.Type = VariableType.Room then
        classState.Rooms.[solution.GetRoomIndex(cls)]
      else
        classState.Times.[solution.GetTimeIndex(cls)]

    solution.Problem.AllClassVariables
    |> Array.sumBy variablePenalty

  let serialize info problem seed runtime solution =
    Convert.toSolution problem solution runtime
    |> Serialize.solution info seed
    |> Serialize.toXml

  let stats (solution : Solution) =
    let problem = solution.Problem
    {| HardPenalty = solution.HardPenalty
       SoftPenalty = solution.SoftPenalty
       SoftPenaltyNormalized = solution.NormalizedSoftPenalty
       SearchPenalty = solution.SearchPenalty
       ClassConflicts = solution.ClassConflicts
       RoomsUnavailable = solution.RoomsUnavailable
       FailedHardConstraints = solution.FailedHardConstraints()
       TimePenalty = solution.TimePenalty()
       TimePenaltyMin = solution.TimePenaltyMin()
       TimePenaltyAverage = (solution.TimePenalty() |> float) / (float problem.TimeVariables.Length)
       TimePenaltyMax = solution.TimePenaltyMax()
       RoomPenalty = solution.RoomPenalty()
       RoomPenaltyMin = solution.RoomPenaltyMin()
       RoomPenaltyAverage = (solution.RoomPenalty() |> float) / (float problem.RoomVariables.Length)
       RoomPenaltyMax = solution.RoomPenaltyMax()
       DistributionPenalty = solution.DistributionPenalty()
       FailedSoftConstraints = solution.FailedSoftConstraints()
       StudentPenalty = solution.StudentPenalty() |}
