namespace Timetabling.Common

module Mutate =
  let inline private timeDiff (penalties : EvaluationPenalties) (solution : Solution) cls time =
    let penalties = penalties.ClassPenalties.[cls].Times
    let oldTime = solution.GetTimeIndex cls
    penalties.[time] - penalties.[oldTime]

  let inline private roomDiff (penalties : EvaluationPenalties) (solution : Solution) cls room =
    let penalties = penalties.ClassPenalties.[cls].Rooms
    let oldRoom = solution.GetRoomIndex cls
    penalties.[room] - penalties.[oldRoom]

  let randomIndex x (arr : 'a []) =
    arr.[x |> Random.toInt arr.Length]

  let time penalties random1 random2 (solution : Solution) =
    let variable = solution.Problem.TimeVariables |> randomIndex random1
    let cls = variable.Class
    let time = random2 |> Random.toInt variable.MaxValue
    solution |> Solution.withTime time cls, timeDiff penalties solution cls time

  let room penalties random1 random2 (solution : Solution) =
    let variable = solution.Problem.RoomVariables |> randomIndex random1
    let cls = variable.Class
    let room = random2 |> Random.toInt variable.MaxValue
    solution |> Solution.withRoom room cls, roomDiff penalties solution cls room

  open Timetabling.Internal.Specialized

  let variable penalties random1 random2 (solution : Solution) =
    let variable = solution.Problem.AllClassVariables |> randomIndex random1
    let cls = variable.Class
    let value = random2 |> Random.toInt variable.MaxValue
    if variable.Type = VariableType.Time then
      solution |> Solution.withTime value cls, timeDiff penalties solution cls value
    else
      solution |> Solution.withRoom value cls, roomDiff penalties solution cls value

  let enrollment random1 random2 (solution : Solution) =
    let variable = solution.Problem.StudentVariables |> randomIndex random1
    let value = variable.LooseValues |> randomIndex random2
    solution |> Solution.withEnrollment value variable.Student, 0.0
