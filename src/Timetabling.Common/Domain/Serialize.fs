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

  let element name content =
    Element(name, content)

  let attr name value =
    Attribute(name, value)

  let toXml name content =
    element name content |> serializeRec :?> XElement

  let solution info seed time (solution : SolutionModel) =
    let serializeStudent (StudentId id) =
      element "student" [attr "id" id]

    let serializeClass (cls : SolutionClass) =
      element "class" ([
        attr "id" cls.Id
      ] @ (cls.Students |> List.map serializeStudent))

    element "solution" ([
      attr "name" solution.Name
      attr "runtime" time
      attr "cores" info.Cores
      attr "technique" (sprintf "%s (seed %i)" info.Technique seed)
      attr "author" info.Author
      attr "institution" info.Institution
      attr "country" info.Country
    ] @ (solution.Classes |> List.map serializeClass))
