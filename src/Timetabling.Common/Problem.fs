namespace Timetabling.Common

// Time
type WeeksPattern = WeeksPattern of uint32

type DaysPattern = DaysPattern of uint32

type Time =
  { Start : int
    Length : int
    Weeks : WeeksPattern
    Days : DaysPattern }

// Room
type RoomId = RoomId of int

type TravelTime = TravelTime of int

type RoomTravelTime = RoomId * TravelTime

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
type SubpartId = Subpartid of int

type Subpart =
  { Id : SubpartId }

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
type Problem =
  { NumberDays : int
    NumberWeeks : int
    SlotsPerDay : int
    Optimization : Optimization
    Rooms : Room list
    Courses : Course list
    Distributions : Distribution list
    Students : Student list }
