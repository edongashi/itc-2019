namespace Timetabling.Common

module Mutate =
    let inline private timeDiff (penalties: ClassPenalties []) (solution: Solution) cls time =
        let penalties = penalties.[cls].Times
        let oldTime = solution.GetTimeIndex cls
        penalties.[time] - penalties.[oldTime]

    let inline private roomDiff (penalties: ClassPenalties []) (solution: Solution) cls room =
        let penalties = penalties.[cls].Rooms
        let oldRoom = solution.GetRoomIndex cls
        penalties.[room] - penalties.[oldRoom]

    let randomIndex x (arr: 'a []) = arr.[x |> Random.toInt arr.Length]

    open Timetabling.Internal.Specialized

    let variableArrayNonPenalized (variables: Variable []) random1 random2 (solution: Solution) =
        let variable = variables |> randomIndex random1
        let cls = variable.Class

        let value =
            random2 |> Random.toInt variable.MaxValue

        if variable.Type = VariableType.Time then
            solution |> Solution.withTime value cls
        else
            solution |> Solution.withRoom value cls

    let timeNonPenalized random1 random2 (solution: Solution) =
        let variable =
            solution.Problem.TimeVariables
            |> randomIndex random1

        let cls = variable.Class

        let time =
            random2 |> Random.toInt variable.MaxValue

        solution |> Solution.withTime time cls

    let time penalties random1 random2 (solution: Solution) =
        let variable =
            solution.Problem.TimeVariables
            |> randomIndex random1

        let cls = variable.Class

        let time =
            random2 |> Random.toInt variable.MaxValue

        solution |> Solution.withTime time cls, timeDiff penalties solution cls time

    let roomNonPenalized random1 random2 (solution: Solution) =
        let variable =
            solution.Problem.RoomVariables
            |> randomIndex random1

        let cls = variable.Class

        let room =
            random2 |> Random.toInt variable.MaxValue

        solution |> Solution.withRoom room cls

    let room penalties random1 random2 (solution: Solution) =
        let variable =
            solution.Problem.RoomVariables
            |> randomIndex random1

        let cls = variable.Class

        let room =
            random2 |> Random.toInt variable.MaxValue

        solution |> Solution.withRoom room cls, roomDiff penalties solution cls room

    open Timetabling.Internal.Specialized

    let variableNonPenalized random1 random2 (solution: Solution) =
        let variable =
            solution.Problem.AllClassVariables
            |> randomIndex random1

        let cls = variable.Class

        let value =
            random2 |> Random.toInt variable.MaxValue

        if variable.Type = VariableType.Time then
            solution |> Solution.withTime value cls
        else
            solution |> Solution.withRoom value cls

    let variable penalties random1 random2 (solution: Solution) =
        let variable =
            solution.Problem.AllClassVariables
            |> randomIndex random1

        let cls = variable.Class

        let value =
            random2 |> Random.toInt variable.MaxValue

        if variable.Type = VariableType.Time then
            solution |> Solution.withTime value cls, timeDiff penalties solution cls value
        else
            solution |> Solution.withRoom value cls, roomDiff penalties solution cls value

    let enrollmentNonPenalized random1 random2 (solution: Solution) =
        let variable =
            solution.Problem.StudentVariables
            |> randomIndex random1

        let value = Random.toInt variable.ChainCount random2

        solution
        |> Solution.withEnrollment value variable.VariableId

    let enrollment random1 random2 (solution: Solution) =
        let variable =
            solution.Problem.StudentVariables
            |> randomIndex random1

        let value = Random.toInt variable.ChainCount random2

        solution
        |> Solution.withEnrollment value variable.VariableId,
        0.0
