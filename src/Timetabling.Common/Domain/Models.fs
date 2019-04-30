namespace Timetabling.Common.Domain

// Time
type WeeksPattern = WeeksPattern of bool list

type DaysPattern = DaysPattern of bool list

type Time =
  { Start : int
    Length : int
    Weeks : WeeksPattern
    Days : DaysPattern }

// Room
type RoomId = RoomId of int

type TravelTime = TravelTime of int

type RoomTravelTime = RoomTravelTime of RoomId * TravelTime

type UnavailableTime = UnavailableTime of Time

type RoomCapacity = RoomCapacity of int

type Room =
  { Id : RoomId
    TravelTimes : RoomTravelTime list
    UnavailableTimes : UnavailableTime list
    Capacity : RoomCapacity }

// Room assignment
type RoomAssignmentPenalty = RoomAssignmentPenalty of int

type RoomAssignmentDetails =
  { RoomId : RoomId
    Penalty : RoomAssignmentPenalty }

type RoomAssignment =
  | NoRoom
  | Rooms of RoomAssignmentDetails list

// Time assignment
type TimeAssignmentPenalty = TimeAssignmentPenalty of int

type TimeAssignment =
  { Time : Time
    Penalty : TimeAssignmentPenalty }

// Class
type ClassId = ClassId of int

type ClassLimit = ClassLimit of int

type Class =
  { Id : ClassId
    PossibleTimes : TimeAssignment list
    PossibleRooms : RoomAssignment
    Parent : ClassId option
    Limit : ClassLimit }

// Subpart
type SubpartId = SubpartId of int

type Subpart =
  { Id : SubpartId
    Classes : Class list }

// Config
type ConfigId = ConfigId of int

type Config =
  { Id : ConfigId
    Subparts : Subpart list }

// Course
type CourseId = CourseId of int

type Course =
  { Id : CourseId
    Configurations : Config list }

// Student
type StudentId = StudentId of int

type Student =
  { Id : StudentId
    Courses : CourseId list }

// Distribution
type DistributionType =
  | SameStart
  | SameTime
  | DifferentTime
  | SameDays
  | DifferentDays
  | SameWeeks
  | DifferentWeeks
  | SameRoom
  | DifferentRoom
  | Overlap
  | NotOverlap
  | SameAttendees
  | Precedence
  | WorkDay of S : int
  | MinGap of G : int
  | MaxDays of D : int
  | MaxDayLoad of S : int
  | MaxBreaks of R : int * S : int
  | MaxBlock of M : int * S : int

type DistributionRequirement =
  | Required
  | Penalized of int

type Distribution =
  { Type : DistributionType
    Requirement : DistributionRequirement
    Classes : ClassId list }

// Optmization
type Optimization =
  { Time : int
    Room : int
    Distribution : int
    Student : int }

// Problem
type ProblemModel =
  { Name : string
    NumberDays : int
    NumberWeeks : int
    SlotsPerDay : int
    Optimization : Optimization
    Rooms : Room list
    Courses : Course list
    Distributions : Distribution list
    Students : Student list }

module Room =
  let id (r : Room) =
    let (RoomId id) = r.Id
    id

module Class =
  let id (c : Class) =
    let (ClassId id) = c.Id
    id

module Subpart =
  let id (s : Subpart) =
    let (SubpartId id) = s.Id
    id

  let classes (s : Subpart) =
    s.Classes

module Config =
  let id (c : Config) =
    let (ConfigId id) = c.Id
    id

  let subparts (c : Config) =
    c.Subparts

module Course =
  let id (c : Course) =
    let (CourseId id) = c.Id
    id

  let configurations (c : Course) =
    c.Configurations

module Student =
  let id (s : Student) =
    let (StudentId id) = s.Id
    id
