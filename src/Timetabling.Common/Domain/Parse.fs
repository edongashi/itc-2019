namespace Timetabling.Common.Domain
open Timetabling.Common
open System
open System.Xml.Linq

// Types
type ParseError =
  | InvalidAttribute of string
  | InvalidName
  | MissingChildElement of string

type XmlParseError =
  { Element : XElement
    Error : ParseError }

type AttributeParser<'a> = string -> XElement -> Result<'a, XmlParseError>

// Utils
module private XmlUtils =
  let private nameOfString name = name |> XName.op_Implicit

  let tryParseInt (str : string) =
    str |> Int32.TryParse |> function
    | true, num -> Some num
    | false, _ -> None

  let tryParseFloat (str : string) =
    str |> Double.TryParse |> function
    | true, num -> Some num
    | false, _ -> None

  let tryParseBinary (str : string) =
    str.ToCharArray()
    |> List.ofArray
    |> Option.traverseList (function
         | '1' -> Some true
         | '0' -> Some false
         | _ -> None)

  let makeAttrError prop xml =
    { Element = xml
      Error = InvalidAttribute prop }

  let tryElement name (xml : XElement) =
    xml.Element(nameOfString name) |> Option.ofObj

  let element name xml =
    xml
    |> tryElement name
    |> Result.ofOption { Element = xml
                         Error = MissingChildElement name }

  let children name (xml : XElement) =
    nameOfString name |> xml.Elements

  let tagName (xml : XElement) = xml.Name.LocalName

  let tryAttr prop (xml : XElement) =
    let attr = nameOfString prop |> xml.Attribute
    match attr with
    | null -> None
    | attr when attr.Value |> String.IsNullOrEmpty -> None
    | attr -> Some attr.Value

  let attr prop xml =
    xml
    |> tryAttr prop
    |> Result.ofOption (makeAttrError prop xml)

  let private attrWith f =
    fun prop xml ->
      xml
      |> tryAttr prop
      >>= f
      |> Result.ofOption (makeAttrError prop xml)

  let tryNumAttr prop xml = xml |> tryAttr prop >>= tryParseInt

  let tryBinaryAttr prop xml = xml |> tryAttr prop >>= tryParseBinary

  let numAttr = attrWith tryParseInt

  let floatAttr: AttributeParser<float> = attrWith tryParseFloat

  let binaryAttr = attrWith tryParseBinary

  let havingName name (xml : XElement) =
    if tagName xml = name then Ok xml
    else Error { Element = xml
                 Error = InvalidName }

  let ensureName name xml =
    xml |> havingName name |> Result.discard

  let traverseChildren name f xml =
    xml
    |> children name
    |> Result.traverseSeq f

// Parsers
open XmlUtils

module Parse =
  let private schedule f xml =
    result {
      let! start = xml |> numAttr "start"
      let! length = xml |> numAttr "length"
      let! weeks = xml |> binaryAttr "weeks"
      let! days = xml |> binaryAttr "days"
      return { Start = start
               Length = length
               Weeks = WeeksPattern weeks
               Days = DaysPattern days } |> f
    }

  let travel xml =
    result {
      do! xml |> ensureName "travel"
      let! room = xml |> numAttr "room"
      let! value = xml |> numAttr "value"
      return RoomTravelTime(RoomId room, TravelTime value)
    }

  let unavailable xml =
    xml |> havingName "unavailable" >>= schedule UnavailableTime

  let room xml =
    let traverse name f = xml |> traverseChildren name f
    result {
      let! id = xml |> numAttr "id"
      let! capacity = xml |> numAttr "capacity"
      let! travelTimes = traverse "travel" travel
      let! unavailableTimes = traverse "unavailable" unavailable
      return { Id = RoomId id
               TravelTimes = travelTimes
               UnavailableTimes = unavailableTimes
               Capacity = RoomCapacity capacity }
    }

  let roomAssignment xml =
    result {
      do! xml |> ensureName "room"
      let! id = xml |> numAttr "id"
      let! penalty = xml |> numAttr "penalty"
      return { RoomId = RoomId id
               Penalty = RoomAssignmentPenalty penalty }
    }

  let timeAssignment xml =
    result {
      do! xml |> ensureName "time"
      let! time = xml |> schedule id
      let! penalty = xml |> numAttr "penalty"
      return { Time = time
               Penalty = TimeAssignmentPenalty penalty }
    }

  let classDetails xml =
    let traverse name f = xml |> traverseChildren name f
    result {
      do! xml |> ensureName "class"
      let! id = xml |> numAttr "id"
      let! limit = xml |> numAttr "limit"
      let parent = xml |> tryNumAttr "parent" |> Option.map ClassId
      let! rooms = traverse "room" roomAssignment
      let roomAssignment =
        if List.isEmpty rooms then NoRoom
        else Rooms rooms

      let! times = traverse "time" timeAssignment
      return { Id = ClassId id
               PossibleTimes = times
               PossibleRooms = roomAssignment
               Parent = parent
               Limit = ClassLimit limit }
    }

  let subpart xml =
    let traverse name f = xml |> traverseChildren name f
    result {
      do! xml |> ensureName "subpart"
      let! id = xml |> numAttr "id"
      let! classes = traverse "class" classDetails
      return { Id = SubpartId id
               Classes = classes }
    }

  let config xml =
    let traverse name f = xml |> traverseChildren name f
    result {
      do! xml |> ensureName "config"
      let! id = xml |> numAttr "id"
      let! subparts = traverse "subpart" subpart
      return { Id = ConfigId id
               Subparts = subparts }
    }

  let course xml =
    let traverse name f = xml |> traverseChildren name f
    result {
      do! xml |> ensureName "course"
      let! id = xml |> numAttr "id"
      let! configurations = traverse "config" config
      return { Id = CourseId id
               Configurations = configurations }
    }

  let optimization xml =
    result {
      do! xml |> ensureName "optimization"
      let! time = xml |> numAttr "time"
      let! room = xml |> numAttr "room"
      let! distribution = xml |> numAttr "distribution"
      let! student = xml |> numAttr "student"
      return { Time = time
               Room = room
               Distribution = distribution
               Student = student }
    }

  let rooms xml =
    let traverse name f = xml |> traverseChildren name f
    result {
      do! xml |> ensureName "rooms"
      let! rooms = traverse "room" room
      return rooms
    }

  let courses xml =
    let traverse name f = xml |> traverseChildren name f
    result {
      do! xml |> ensureName "courses"
      let! courses = traverse "course" course
      return courses
    }

  let distributionClass xml =
    result {
      do! xml |> ensureName "class"
      let! id = xml |> numAttr "id"
      return ClassId id
    }

  let requirement xml =
    xml |> tryAttr "required" |> function
    | Some "true" -> Ok Required
    | Some _ -> Error(makeAttrError "required" xml)
    | None -> xml
              |> tryNumAttr "penalty"
              |> Option.map Penalized
              |> Result.ofOption (makeAttrError "penalty" xml)

  let distributionType (str : string) =
    let splitParts (str : string) =
      let index = str.IndexOf("(")
      if index > 0 then
        let name = str.Substring(0, index)
        str
          .Substring(0, str.Length - 1)
          .Substring(index + 1)
          .Split(',')
        |> Seq.map tryParseInt
        |> Option.sequenceSeq
        |> Option.map (fun args -> name, args)
      else Some(str, [])

    option {
      let! name, args = splitParts str
      let len = List.length args
      let at index = args |> List.item index
      return! match name, len with
              | "SameStart", 0 -> Some SameStart
              | "SameTime", 0 -> Some SameTime
              | "DifferentTime", 0 -> Some DifferentTime
              | "SameDays", 0 -> Some SameDays
              | "DifferentDays", 0 -> Some DifferentDays
              | "SameWeeks", 0 -> Some SameWeeks
              | "DifferentWeeks", 0 -> Some DifferentWeeks
              | "SameRoom", 0 -> Some SameRoom
              | "DifferentRoom", 0 -> Some DifferentRoom
              | "Overlap", 0 -> Some Overlap
              | "NotOverlap", 0 -> Some NotOverlap
              | "SameAttendees", 0 -> Some SameAttendees
              | "Precedence", 0 -> Some Precedence
              | "WorkDay", 1 -> WorkDay(at 0) |> Some
              | "MinGap", 1 -> MinGap(at 0) |> Some
              | "MaxDays", 1 -> MaxDays(at 0) |> Some
              | "MaxDayLoad", 1 -> MaxDayLoad(at 0) |> Some
              | "MaxBreaks", 2 -> MaxBreaks(at 0, at 1) |> Some
              | "MaxBlock", 2 -> MaxBlock(at 0, at 1) |> Some
              | _ -> None
    }

  let distribution xml =
    let traverse name f = xml |> traverseChildren name f
    result {
      do! xml |> ensureName "distribution"
      let! typeString = xml |> attr "type"
      let! distributionType =
        typeString
        |> distributionType
        |> Result.ofOption (makeAttrError "type" xml)
      let! classes = traverse "class" distributionClass
      let! requirement = xml |> requirement
      return { Type = distributionType
               Requirement = requirement
               Classes = classes |> List.distinct }
    }

  let distributions xml =
    let traverse name f = xml |> traverseChildren name f
    result {
      do! xml |> ensureName "distributions"
      let! distributions = traverse "distribution" distribution
      return distributions |> List.filter (fun d -> List.length d.Classes > 1)
    }

  let studentCourse xml =
    result {
      do! xml |> ensureName "course"
      let! id = xml |> numAttr "id"
      return CourseId id
    }

  let student xml =
    let traverse name f = xml |> traverseChildren name f
    result {
      do! xml |> ensureName "student"
      let! id = xml |> numAttr "id"
      let! courses = traverse "course" studentCourse
      return { Id = StudentId id
               Courses = courses }
    }

  let students xml =
    let traverse name f = xml |> traverseChildren name f
    result {
      do! xml |> ensureName "students"
      let! students = traverse "student" student
      return students
    }

  let problem xml =
    let maybeMap name f =
      xml
      |> tryElement name
      |> Option.map f
      |> Option.defaultValue (Ok [])

    result {
      let name = xml |> tryAttr "name" |> Option.defaultValue "<unknown>"
      let! numberDays = xml |> numAttr "nrDays"
      let! slotsPerDay = xml |> numAttr "slotsPerDay"
      let! numberWeeks = xml |> numAttr "nrWeeks"
      let! optimization = xml |> element "optimization" >>= optimization
      let! rooms = xml |> element "rooms" >>= rooms
      let! courses = xml |> element "courses" >>= courses
      let! distributions = maybeMap "distributions" distributions
      let! students = maybeMap "students" students
      return { Name = name
               NumberDays = numberDays
               NumberWeeks = numberWeeks
               SlotsPerDay = slotsPerDay
               Optimization = optimization
               Rooms = rooms
               Courses = courses
               Distributions = distributions
               Students = students }
    }

  let solutionStudent xml =
    result {
      do! xml |> ensureName "student"
      let! id = xml |> numAttr "id"
      return StudentId id
    }

  let solutionClass xml =
    let traverse name f = xml |> traverseChildren name f
    result {
      do! xml |> ensureName "class"
      let! id = xml |> numAttr "id"
      let! days = xml |> binaryAttr "days"
      let! weeks = xml |> binaryAttr "weeks"
      let! start = xml |> numAttr "start"
      let room = xml |> tryNumAttr "room"
      let! students = traverse "student" solutionStudent
      return { Id = ClassId id
               Days = DaysPattern days
               Weeks = WeeksPattern weeks
               Start = start
               Room = room |> Option.map RoomId
               Students = students }
    }

  let solution xml =
    let traverse name f = xml |> traverseChildren name f
    result {
      do! xml |> ensureName "solution"
      let! name = xml |> attr "name"
      let! runtime = xml |> floatAttr "runtime"
      let! classes = traverse "class" solutionClass
      return { Name = name
               Runtime = runtime
               Classes = classes }
    }
