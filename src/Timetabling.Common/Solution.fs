namespace Timetabling.Common
open Timetabling.Common.Domain
open Timetabling.Internal.Specialized

type Solution = Timetabling.Internal.Solution

type ClassPenalties =
  { Class : int
    Rooms : float []
    Times : float [] }

type PenalizedVariable =
  | PenalizedRoomVariable of cls : int * index : int * penalty : int
  | PenalizedTimeVariable of cls : int * index : int * penalty : int

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

  let withEnrollment (chain : int) (student : int) (solution : Solution) =
    solution.WithEnrollment(student, chain)

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

  let classPenaltiesHard (solution : Solution) =
    let timeVariablesSparse = solution.Problem.TimeVariablesSparse
    let roomVariablesSparse = solution.Problem.RoomVariablesSparse
    let conflicts = solution.ViolatingClasses()
    let timePenalties =
      conflicts
      |> Seq.choose (fun pair ->
        let cls = pair.Key
        let penalty = pair.Value.Time
        if penalty > 0
        then Some (PenalizedTimeVariable (cls, solution.GetTimeIndex cls, penalty))
        else None)
    let roomPenalties =
      conflicts
      |> Seq.choose (fun pair ->
        let cls = pair.Key
        let penalty = pair.Value.Room
        if penalty > 0
        then Some (PenalizedRoomVariable (cls, solution.GetRoomIndex cls, penalty))
        else None)
    timePenalties
    |> Seq.append roomPenalties
    |> Seq.sortBy (function
      | PenalizedTimeVariable (cls, _, penalty) -> penalty, timeVariablesSparse.[cls].MaxValue
      | PenalizedRoomVariable (cls, _, penalty) -> penalty, roomVariablesSparse.[cls].MaxValue)
    |> List.ofSeq

  let classPenaltiesSoft (solution : Solution) =
    let problem = solution.Problem
    let classes = problem.Classes
    let optimization = { Time = problem.TimePenalty
                         Room = problem.RoomPenalty
                         Distribution = problem.DistributionPenalty
                         Student = problem.StudentPenalty }
    let timeVariables = problem.TimeVariables
    let roomVariables = problem.RoomVariables
    let timeVariablesSparse = problem.TimeVariablesSparse
    let roomVariablesSparse = problem.RoomVariablesSparse
    let conflicts = solution.SoftViolatingClasses()
    let studentConflicts = solution.StudentConflictingClasses()
    let timePenalties =
      timeVariables
      |> Seq.choose (fun var ->
        let cls = var.Class
        let classTime = solution.GetTimeIndex cls
        let classData = classes.[cls]
        let distributionPenalty =
          if conflicts.ContainsKey cls
          then conflicts.[cls].Time
          else 0
        let assignmentPenalty =
          classData.PossibleSchedules.[classTime].Penalty - classData.MinTimePenalty
        let studentPenalty =
          if studentConflicts.ContainsKey cls
          then studentConflicts.[cls]
          else 0
        let penalty =
          distributionPenalty * optimization.Distribution
          + assignmentPenalty * optimization.Time
          + studentPenalty
        if penalty > 0
        then Some (PenalizedTimeVariable (cls, classTime, penalty))
        else None)
    let roomPenalties =
      roomVariables
      |> Seq.choose (fun var ->
        let cls = var.Class
        let classRoom = solution.GetRoomIndex cls
        let classData = classes.[cls]
        let distributionPenalty =
          if conflicts.ContainsKey cls
          then conflicts.[cls].Room
          else 0
        let assignmentPenalty =
          if classRoom >= 0
          then classData.PossibleRooms.[classRoom].Penalty - classData.MinRoomPenalty
          else 0
        let studentPenalty =
          if studentConflicts.ContainsKey cls
          then studentConflicts.[cls]
          else 0
        let penalty =
          distributionPenalty * optimization.Distribution
          + assignmentPenalty * optimization.Room
          + studentPenalty
        if classData.PossibleRooms.Length > 0 && penalty > 0
        then Some (PenalizedRoomVariable (cls, classRoom, penalty))
        else None)
    timePenalties
    |> Seq.append roomPenalties
    |> Seq.sortBy (function
      | PenalizedTimeVariable (cls, _, penalty) -> penalty, timeVariablesSparse.[cls].MaxValue
      | PenalizedRoomVariable (cls, _, penalty) -> penalty, roomVariablesSparse.[cls].MaxValue)
    |> List.ofSeq

  let variableClass variable =
    match variable with
    | PenalizedTimeVariable (cls, _, _)
    | PenalizedRoomVariable (cls, _, _) -> cls

  let variableIndex variable =
    match variable with
    | PenalizedTimeVariable (_, index, _)
    | PenalizedRoomVariable (_, index, _) -> index

  let variablePenalty variable =
    match variable with
    | PenalizedTimeVariable (_, _, penalty)
    | PenalizedRoomVariable (_, _, penalty) -> penalty

  let penalize (increment : float) (rnd : float) (penalties : ClassPenalties[]) (solution : Solution) =
    let set (index : int) (mutator : 'a -> 'a) (arr : 'a[]) =
      let clone = arr |> Array.copy
      clone.[index] <- mutator clone.[index]
      clone

    let list =
      if solution.HardPenalty > 0
      then classPenaltiesHard solution
      else classPenaltiesSoft solution

    let penalizeClass penalties variable =
      let cls = variableClass variable
      penalties
      |> set cls
        (fun clsPenalties ->
           match variable with
           | PenalizedTimeVariable (_, index, _)
             -> { clsPenalties with
                    Times = set index (fun x -> x + increment) clsPenalties.Times }
           | PenalizedRoomVariable (_, index, _)
             -> { clsPenalties with
                    Rooms = set index (fun x -> x + increment) clsPenalties.Rooms })

    if List.isEmpty list
    then
      penalties
    else
      list
      |> List.truncate (solution.Problem.AllClassVariables.Length / 20)
      |> (fun filtered -> filtered |> List.item (int (rnd * float (List.length filtered))))
      |>  penalizeClass penalties

  let incrementWeights (weights : int[]) (solution : Solution) =
    let problem = solution.Problem
    problem.Constraints
    |> Array.mapi (
      fun i c ->
        let struct (hard, soft) = c.Evaluate(problem, solution)
        if hard > 0 || soft > 0 then
          weights.[i] + 1
        else
          0
    )

  let worstConstraints (weights : int[]) (solution : Solution) =
    let problem = solution.Problem
    problem.Constraints
    |> Seq.mapi (
      fun i c ->
        let struct (hard, soft) = c.Evaluate(problem, solution)
        hard, soft, weights.[i], i
    )
    |> Seq.filter (fun (hard, soft, _, _) -> hard + soft > 0)
    |> Seq.sortByDescending (fun (hard, soft, weight, id) -> (weight, hard, soft, id))
    |> List.ofSeq

  let worstHardConstraints (weights : int[]) (solution : Solution) =
    worstConstraints weights solution
    |> List.filter (fun (hard, _, _, _) -> hard > 0)

  let worstSoftConstraints (weights : int[]) (solution : Solution) =
    worstConstraints weights solution
    |> List.filter (fun (_, soft, _, _) -> soft > 0)

  let stats (solution : Solution) =
    let problem = solution.Problem
    {| HardPenalty = solution.HardPenalty
       SoftPenalty = solution.SoftPenalty
       SoftPenaltyNormalized = solution.NormalizedSoftPenalty
       SearchPenalty = solution.SearchPenalty
       ClassConflicts = solution.ClassConflicts
       ClassOverflows = solution.ClassCapacityPenalty()
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
