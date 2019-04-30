namespace Timetabling.Common

module Mutate =
  let private toNum (n : int) (rand : float) =
    (float n) * rand |> int

  let private randomIndex random (arr : 'a []) =
    arr.[random |> toNum arr.Length]

  let time random1 random2 (solution : Solution) =
    let variable = solution.Problem.TimeVariables |> randomIndex random1
    let cls = variable.Class
    let time = random2 |> toNum variable.MaxValue
    solution |> Solution.withTime time cls

  let room random1 random2 (solution : Solution) =
    let variable = solution.Problem.RoomVariables |> randomIndex random1
    let cls = variable.Class
    let room = random2 |> toNum variable.MaxValue
    solution |> Solution.withRoom room cls

  open Timetabling.Internal.Specialized

  let variable random1 random2 (solution : Solution) =
    let variable = solution.Problem.AllClassVariables |> randomIndex random1
    let cls = variable.Class
    let value = random2 |> toNum variable.MaxValue
    if variable.Type = VariableType.Time then
      solution |> Solution.withTime value cls
    else
      solution |> Solution.withRoom value cls

  let enrollment random1 random2 (solution : Solution) =
    let variable = solution.Problem.StudentVariables |> randomIndex random1
    let value = variable.LooseValues |> randomIndex random2
    solution |> Solution.withEnrollment value variable.Student
