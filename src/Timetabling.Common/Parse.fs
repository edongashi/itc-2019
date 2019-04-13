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
  let ofString name = name |> XName.op_Implicit

  let parseInt str =
    str |> Int32.TryParse |> function
    | true, num -> Some num
    | false, _ -> None

  let parseBinary (str : string) =
    str.ToCharArray()
    |> List.ofArray
    |> List.map (function
                  | '1' -> Some true
                  | '0' -> Some false
                  | _ -> None)
    |> Option.sequenceList

  let element prop (xml : XElement) =
    xml.Element(ofString prop) |> Option.ofObj

  let attr prop (xml : XElement) =
    let attr = ofString prop |> xml.Attribute
    match attr with
    | null -> None
    | attr when attr.Value |> String.IsNullOrEmpty -> None
    | attr -> Some attr.Value

  let tagName (xml : XElement) = xml.Name.LocalName

  let numAttr prop = attr prop >=> parseInt
  let binaryAttr prop = attr prop >=> parseBinary

  let attributeError prop =
    prop
    |> sprintf "Missing or invalid attribute '%s'."
    |> InvalidAttribute

  let getter f xml =
    fun prop ->
      xml |> f prop,
      { Element = xml
        Error = prop |> attributeError }

  let numGetter xml = getter numAttr xml

  let binaryGetter xml = getter binaryAttr xml

  let havingName name xml =
    if tagName xml = name then Ok xml
    else Error { Element = xml
                 Error = InvalidName }

  let ensureName name xml =
    xml |> havingName name |> Result.discard

// Parsers
open ParseUtils

let private schedule xml =
  let getNum = numGetter xml
  let getBinary = binaryGetter xml
  result {
    let! start = getNum "start"
    let! length = getNum "length"
    let! weeks = getBinary "weeks"
    let! days = getBinary "days"
    return { Start = start
             Length = length
             Weeks = WeeksPattern weeks
             Days = DaysPattern days }
  }

let optimization xml =
  let getNum = numGetter xml
  result {
    let! time = getNum "time"
    let! room = getNum "room"
    let! distribution = getNum "distribution"
    let! student = getNum "student"
    return { Time = time
             Room = room
             Distribution = distribution
             Student = student }
  }

let travel xml =
  let getNum = numGetter xml
  result {
    do! xml |> ensureName "travel"
    let! room = getNum "room"
    let! value = getNum "value"
    return RoomTravelTime(RoomId room, TravelTime value)
  }

let unavailable xml =
  xml |> havingName "unavailable" >>= schedule
