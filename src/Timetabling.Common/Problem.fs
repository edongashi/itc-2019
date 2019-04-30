namespace Timetabling.Common

open Timetabling
open Timetabling.Common.Domain

type Problem =
  { Problem : ProblemModel
    Instance : Internal.Problem
    IdMapping : IdMapping }

module Problem =
  let wrap problem =
    let ids, instance = Convert.fromProblem problem
    { Problem = problem
      Instance = instance
      IdMapping = ids }

  let parse xml =
    Parse.problem xml
    |> Result.map wrap

  let initialSolution p = p.Instance.InitialSolution
