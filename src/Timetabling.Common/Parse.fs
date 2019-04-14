module Timetabling.Common.Parse

open System
open System.Xml.Linq

// Types
type ParseError =
  | InvalidAttribute of string
  | InvalidFormat of string
  | InvalidName

type XmlParseError =
  { Element : XElement
    Error : ParseError }

// Utils
module private ParseUtils =
  let private nameOfString name = name |> XName.op_Implicit

  let private tryParseInt str =
    str |> Int32.TryParse |> function
    | true, num -> Some num
    | false, _ -> None

  let private tryParseBinary (str : string) =
    str.ToCharArray()
    |> List.ofArray
    |> Option.traverseList (function
         | '1' -> Some true
         | '0' -> Some false
         | _ -> None)

  let private makeAttrError prop =
    prop
    |> sprintf "Missing or invalid attribute '%s'."
    |> InvalidAttribute

  let private makeError prop xml =
    { Element = xml
      Error = prop |> makeAttrError }

  let element prop (xml : XElement) =
    xml.Element(nameOfString prop) |> Option.ofObj

  let attr prop (xml : XElement) =
    let attr = nameOfString prop |> xml.Attribute
    match attr with
    | null -> None
    | attr when attr.Value |> String.IsNullOrEmpty -> None
    | attr -> Some attr.Value

  let children name (xml : XElement) =
    nameOfString name |> xml.Elements

  let tagName (xml : XElement) = xml.Name.LocalName

  let private attrWith f =
    fun prop xml ->
      xml
      |> attr prop
      >>= f
      |> Result.ofOption (makeError prop xml)

  let tryNumAttr prop xml = xml |> attr prop >>= tryParseInt

  let tryBinaryAttr prop xml = xml |> attr prop >>= tryParseBinary

  let numAttr = attrWith tryParseInt

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
open ParseUtils

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
