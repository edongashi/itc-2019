namespace Timetabling.Common.Domain
open Timetabling.Common

type IdMap = Map<int, int>

type IdMapping =
  { Rooms : IdMap
    Courses : IdMap
    Configurations : IdMap
    Subparts : IdMap
    Classes : IdMap
    Students : IdMap }

module IdMapping =
  let private resolve (map : IdMapping -> IdMap) fid mapping item =
    mapping |> map |> Map.find (fid item)

  let resolveRoom = resolve (fun m -> m.Rooms) Room.id
  let resolveRoomId = resolve (fun m -> m.Rooms) (fun (RoomId id) -> id)
  let resolveCourse = resolve (fun m -> m.Courses) Course.id
  let resolveCourseId = resolve (fun m -> m.Courses) (fun (CourseId id) -> id)
  let resolveConfig = resolve (fun m -> m.Configurations) Config.id
  let resolveSubpart = resolve (fun m -> m.Subparts) Subpart.id
  let resolveClass = resolve (fun m -> m.Classes) Class.id
  let resolveClassId = resolve (fun m -> m.Classes) (fun (ClassId id) -> id)
  let resolveStudent = resolve (fun m -> m.Students) Student.id

  let private forwardMap (fid : 'a -> int) (items : 'a list) =
    items
    |> List.mapi (fun i x -> (fid x, i))
    |> Map.ofList

  let fromProblem (problem : ProblemModel) =
    let rooms =
      problem.Rooms
      |> forwardMap Room.id
    let courses =
      problem.Courses
      |> forwardMap Course.id
    let configurations =
      problem.Courses
      >>= Course.configurations
      |> forwardMap Config.id
    let subparts =
      problem.Courses
      >>= Course.configurations
      >>= Config.subparts
      |> forwardMap Subpart.id
    let classes =
      problem.Courses
      >>= Course.configurations
      >>= Config.subparts
      >>= Subpart.classes
      |> forwardMap Class.id
    let students =
      problem.Students
      |> forwardMap Student.id

    { Rooms = rooms
      Courses = courses
      Configurations = configurations
      Subparts = subparts
      Classes = classes
      Students = students }

module Convert =
  open IdMapping

  let private bitPattern (pattern : bool list) =
    let folder next (acc, i) =
      if next then (acc ||| (1u <<< i), i + 1)
      else (acc, i + 1)
    List.foldBack folder pattern (0u, 0) |> fst

  let private weeksBitPattern (WeeksPattern pattern) = bitPattern pattern
  let private daysBitPattern (DaysPattern pattern) = bitPattern pattern

  let private mapToArray f s =
    s |> Seq.map f |> Seq.toArray

  let private mapiToArray f s =
    s |> Seq.mapi f |> Seq.toArray

  let fromTime (time : Time) =
    Timetabling.Internal.Schedule (
      time.Weeks |> weeksBitPattern,
      time.Days |> daysBitPattern,
      time.Start,
      time.Length)

  let fromUnavailableTime (UnavailableTime time) =
    fromTime time

  let fromTravelTime ids (RoomTravelTime(room, TravelTime time)) =
    Timetabling.Internal.TravelTime(resolveRoomId ids room, time)

  let fromRoom ids (room : Room) =
    let (RoomCapacity capacity) = room.Capacity
    Timetabling.Internal.Room (
      resolveRoom ids room,
      capacity,
      room.UnavailableTimes |> mapToArray fromUnavailableTime,
      room.TravelTimes |> mapToArray (fromTravelTime ids))

  let fromRoomAssignmentDetails ids (r : RoomAssignmentDetails) =
    let (RoomAssignmentPenalty penalty) = r.Penalty
    Timetabling.Internal.RoomAssignment(
      resolveRoomId ids r.RoomId,
      penalty)

  let private listOfRoomAssignment r =
    match r with
    | NoRoom -> []
    | Rooms r -> r

  let fromTimeAssignment (timeAssignment : TimeAssignment) =
    let time = timeAssignment.Time |> fromTime
    let (TimeAssignmentPenalty penalty) = timeAssignment.Penalty
    Timetabling.Internal.ScheduleAssignment (
      time.Weeks,
      time.Days,
      time.Start,
      time.Length,
      penalty)

  let fromClass ids (cls : Class) =
    let (ClassLimit limit) = cls.Limit
    Timetabling.Internal.Class (
      resolveClass ids cls,
      cls.Parent |> Option.map (resolveClassId ids) |> Option.defaultValue -1,
      limit,
      cls.PossibleRooms |> listOfRoomAssignment |> mapToArray (fromRoomAssignmentDetails ids),
      cls.PossibleTimes |> mapToArray fromTimeAssignment)

  let fromSubpart ids (subpart : Subpart) =
    Timetabling.Internal.Subpart (
      resolveSubpart ids subpart,
      subpart.Classes |> mapToArray (fromClass ids))

  let fromConfiguration ids (config : Config) =
    Timetabling.Internal.CourseConfiguration (
      resolveConfig ids config,
      config.Subparts |> mapToArray (fromSubpart ids))

  let fromCourse ids (course : Course) =
    Timetabling.Internal.Course (
      resolveCourse ids course,
      course.Configurations |> mapToArray (fromConfiguration ids))

  let fromStudent ids (student : Student) =
    Timetabling.Internal.Student (
      resolveStudent ids student,
      student.Courses |> mapToArray (resolveCourseId ids))

  open Timetabling.Internal.Constraints
  type private IConstraint = Timetabling.Internal.IConstraint

  let fromDistribution index (distribution : Distribution) : IConstraint =
    let required, penalty = match distribution.Requirement with
                            | Required -> true, 0
                            | Penalized penalty -> false, penalty
    let distributionType = distribution.Type
    let classes = distribution.Classes |> mapToArray (fun (ClassId id) -> id)
    match distributionType with
    | SameStart -> SameStart(index, required, penalty, classes) :> IConstraint
    | SameTime -> SameTime(index, required, penalty, classes) :> IConstraint
    | DifferentTime -> DifferentTime(index, required, penalty, classes) :> IConstraint
    | SameDays -> SameDays(index, required, penalty, classes) :> IConstraint
    | DifferentDays -> DifferentDays(index, required, penalty, classes) :> IConstraint
    | SameWeeks -> SameWeeks(index, required, penalty, classes) :> IConstraint
    | DifferentWeeks -> DifferentWeeks(index, required, penalty, classes) :> IConstraint
    | SameRoom -> SameRoom(index, required, penalty, classes) :> IConstraint
    | DifferentRoom -> DifferentRoom(index, required, penalty, classes) :> IConstraint
    | Overlap -> Overlap(index, required, penalty, classes) :> IConstraint
    | NotOverlap -> NotOverlap(index, required, penalty, classes) :> IConstraint
    | SameAttendees -> SameAttendees(index, required, penalty, classes) :> IConstraint
    | Precedence -> Precedence(index, required, penalty, classes) :> IConstraint
    | WorkDay(s) -> WorkDay(index, s, required, penalty, classes) :> IConstraint
    | MinGap(g) -> MinGap(index, g, required, penalty, classes) :> IConstraint
    | MaxDays(d) -> MaxDays(index, d, required, penalty, classes) :> IConstraint
    | MaxDayLoad(s) -> MaxDayLoad(index, s, required, penalty, classes) :> IConstraint
    | MaxBreaks(r, s) -> MaxBreaks(index, r, s, required, penalty, classes) :> IConstraint
    | MaxBlock(m, s) -> MaxBlock(index, m, s, required, penalty, classes) :> IConstraint

  let fromProblem (problem : ProblemModel) =
    let ids = IdMapping.fromProblem problem
    (ids, Timetabling.Internal.Problem (
      problem.Name,
      problem.NumberWeeks,
      problem.NumberDays,
      problem.SlotsPerDay,
      problem.Optimization.Time,
      problem.Optimization.Room,
      problem.Optimization.Distribution,
      problem.Optimization.Student,
      problem.Rooms |> mapToArray (fromRoom ids),
      problem.Courses |> mapToArray (fromCourse ids),
      problem.Students |> mapToArray (fromStudent ids),
      problem.Distributions |> mapiToArray fromDistribution))
