open Argu
open System.Xml.Linq
open Timetabling.Common

type Verb() = inherit CliPrefixAttribute(CliPrefix.None)

type Argument =
  | [<ExactlyOnce>] Instance of path : string
  interface IArgParserTemplate with
      member this.Usage : string =
          match this with
          | Instance _ -> "XML problem path."

let initialize (problem : Problem) =
  problem
  |> ProblemWrapper.wrap
  |> ProblemWrapper.initialSolution
  |> Solution.stats
  |> printfn "%A"

let run (args : ParseResults<Argument>) =
  let file = args.GetResult(Instance)
  let root = XDocument.Load(file).Root
  let problem = Parse.problem root
  match problem with
  | Ok problem ->
      try
        problem |> initialize |> ignore
      with ex -> printfn "%s\n%s" ex.Message ex.StackTrace
      printfn "Parsed successfully"
  | Error error -> printfn "Error %A" error
  ()

[<EntryPoint>]
let main argv =
  let parser = ArgumentParser.Create<Argument>()
  try
      let result = parser.ParseCommandLine(inputs = argv)
      run result
      0
  with e ->
      printfn "%s" e.Message
      1
