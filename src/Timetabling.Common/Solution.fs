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
