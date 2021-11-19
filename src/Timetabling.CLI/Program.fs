open Argu
open System.Xml.Linq
open Timetabling.Common
open Timetabling.Common.Domain

type Verb() =
    inherit CliPrefixAttribute(CliPrefix.None)

type Argument =
    | [<ExactlyOnce>] Instance of path: string
    | Solution of path: string
    | Seed of seed: int
    | Config of path: string
    | Timeout of seconds: int
    interface IArgParserTemplate with
        member this.Usage: string =
            match this with
            | Instance _ -> "XML problem path."
            | Solution _ -> "Solution path."
            | Seed _ -> "Seed number."
            | Config _ -> "Custom config path."
            | Timeout _ -> "Solver duration."

let initialize duration solutionGetter seed (p: ProblemModel) =
    let cancellation =
        new System.Threading.CancellationTokenSource()

    if duration > 0 then
        cancellation.CancelAfter(duration * 1000)

    System.Console.CancelKeyPress.Add (fun e ->
        e.Cancel <- true
        printfn "\nStopping solver . . . "
        cancellation.Cancel())

    p
    |> Problem.wrap
    |> fun p -> Solver.solve seed cancellation.Token p (p |> solutionGetter)

let run (args: ParseResults<Argument>) =
    match args.TryGetResult(Config) with
    | Some path -> Timetabling.Internal.Config.Load(System.IO.File.ReadAllText(path))
    | None -> ()

    let duration =
        args.TryGetResult(Timeout)
        |> Option.defaultValue 0

    let problemPath = args.GetResult(Instance)
    let solutionPath = args.TryGetResult(Solution)

    let seed =
        args.TryGetResult(Seed)
        |> Option.defaultValue (Random.nextInt ())

    let root = XDocument.Load(problemPath).Root
    let problem = Parse.problem root

    let getInitialSolution (problem: Problem) =
        match solutionPath with
        | Some path ->
            Problem.parseSolution problem (XDocument.Load(path).Root)
            |> Result.catch (problem |> Problem.initialSolution)
        | None -> problem |> Problem.initialSolution

    match problem with
    | Ok problem ->
        try
            problem
            |> initialize duration getInitialSolution seed
            |> ignore
        with
        | ex -> printfn "%s\n%s" ex.Message ex.StackTrace
    | Error error -> printfn "Error %A" error

    ()

[<EntryPoint>]
let main argv =
    let parser = ArgumentParser.Create<Argument>()

    try
        let result = parser.ParseCommandLine(inputs = argv)
        run result
        0
    with
    | e ->
        printfn "%s" e.Message
        1
