namespace Timetabling.Common
open System.Xml.Linq
open Timetabling.Common.Domain

type SolverInfo =
  { Cores : int
    Technique : string
    Author : string
    Institution : string
    Country : string }

module SolverInfo =
  let defaults =
    { Cores = 1
      Technique = "Guided Simulated Annealing"
      Author = "upfiek"
      Institution = "University of Prishtina"
      Country = "Kosovo" }

type XmlContent =
  | Element of name : string * content : XmlContent list
  | Attribute of name : string * value : obj

module Serialize =
  let private xname s = XName.op_Implicit s

  let rec private serializeRec content =
    match content with
    | Element(name, content) ->
        XElement(xname name, content |> List.map serializeRec |> Array.ofList) :> obj
    | Attribute(name, value) ->
        XAttribute(xname name, value) :> obj

  let pattern p =
    p
    |> List.map (fun b -> if b then "1" else "0")
    |> String.concat ""

  let daysPattern (DaysPattern d) = pattern d
  let weeksPattern (WeeksPattern w) = pattern w

  let element name content =
    Element(name, content)

  let attr name value =
    Attribute(name, value)

  let toXml content =
    content |> serializeRec :?> XElement

  let solution info seed (solution : SolutionModel) =
    let serializeStudent (StudentId id) =
      element "student" [attr "id" id]

    let serializeClass (cls : SolutionClass) =
      let (ClassId id) = cls.Id
      let students = cls.Students |> List.map serializeStudent
      let roomAttr =
        cls.Room
        |> Option.map (fun (RoomId id) -> attr "room" id)
        |> List.ofOption

      element "class" ([
        attr "id" id
        attr "days" (daysPattern cls.Days)
        attr "start" cls.Start
        attr "weeks" (weeksPattern cls.Weeks)
      ] @ roomAttr @ students)

    element "solution" ([
      attr "name" solution.Name
      attr "runtime" (int solution.Runtime)
      attr "cores" info.Cores
      attr "technique" (sprintf "%s (seed %i)" info.Technique seed)
      attr "author" info.Author
      attr "institution" info.Institution
      attr "country" info.Country
    ] @ (solution.Classes |> List.map serializeClass))
