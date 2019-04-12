module Timetabling.Common.Parse

open System
open System.Xml.Linq
open Utils

// Types
type ParseError =
  | InvalidAttribute of string

// Utils
module private Utils =
  let ofString name = name |> XName.op_Implicit

  let tryParse str =
    str |> Int32.TryParse |> function
    | true, num -> Some num
    | false, _ -> None

  let element prop (xml : XElement) =
    xml.Element(ofString prop) |> optionOfNullable

  let attr prop (xml : XElement) =
    let attr = ofString prop |> xml.Attribute
    match attr with
    | null -> None
    | attr when attr.Value |> String.IsNullOrEmpty -> None
    | attr -> Some attr.Value

  let numAttr prop = attr prop >=> tryParse

  let numGetter xml =
    fun prop ->
      xml |> numAttr prop,
      prop |> sprintf "Attribute '%s' not found." |> InvalidAttribute

// Parsers
let optimization xml =
  let getNum = Utils.numGetter xml
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
