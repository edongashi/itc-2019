namespace Timetabling.Common
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
