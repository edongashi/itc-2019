open Timetabling.Common
open System.Xml.Linq

[<EntryPoint>]
let main args =
  let xdoc = XDocument.Load(@"D:\Documents\inst\demo\bet-sum18.xml")
  let x = Parse.problem xdoc.Root
  match x with
  | Ok x -> printfn "Ok! %A" x
  | Error x -> printfn "Error %A" x
  0
