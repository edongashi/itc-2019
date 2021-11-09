[<AutoOpen>]
module Timetabling.Common.Monad

open System

type Bind =
    static member (>>=)(source, f: 'T -> _) = Option.bind f source
    static member (>>=)(source, f: 'T -> _) = Result.bind f source
    static member (>>=)(source, f: 'T -> _) = List.collect f source

    static member inline Invoke (source: '``Monad<'T>``) (binder: 'T -> '``Monad<'U>``) : '``Monad<'U>`` =
        let inline call (_mthd: 'M, input: 'I, _output: 'R, f) =
            ((^M or ^I or ^R): (static member (>>=) : _ * _ -> _) input, f)

        call (Unchecked.defaultof<Bind>, source, Unchecked.defaultof<'``Monad<'U>``>, binder)

let inline (>>=) (x: '``Monad<'T>``) (f: 'T -> '``Monad<'U>``) : '``Monad<'U>`` = Bind.Invoke x f
let inline (>=>) (f: 'T -> '``Monad<'U>``) (g: 'U -> '``Monad<'V>``) (x: 'T) : '``Monad<'V>`` = Bind.Invoke(f x) g

type OptionBuilder() =
    member this.Bind(v, f) = v |> Option.bind f
    member this.Return v = Some v
    member this.ReturnFrom o = o
    member this.Zero() = None

type ResultBuilder() =
    member this.Return(x) = Ok x
    member this.ReturnFrom(m: Result<_, _>) = m
    member this.Bind(m, f) = Result.bind f m

    member this.Bind((m, error): Option<'T> * 'E, f) =
        m |> Result.ofOption error |> Result.bind f

    member this.Zero() = None
    member this.Combine(m, f) = Result.bind f m
    member this.Delay(f: unit -> _) = f
    member this.Run(f) = f ()

    member this.TryWith(m, h) =
        try
            this.ReturnFrom(m)
        with
        | e -> h e

    member this.TryFinally(m, compensation) =
        try
            this.ReturnFrom(m)
        finally
            compensation ()

    member this.Using(res: #IDisposable, body) =
        this.TryFinally(
            body res,
            fun () ->
                match res with
                | null -> ()
                | disp -> disp.Dispose()
        )

    member this.While(guard, f) =
        if not (guard ()) then
            Ok()
        else
            do f () |> ignore
            this.While(guard, f)

    member this.For(sequence: seq<_>, body) =
        this.Using(
            sequence.GetEnumerator(),
            fun enum -> this.While(enum.MoveNext, this.Delay(fun () -> body enum.Current))
        )

// Builders
let option = OptionBuilder()
let result = ResultBuilder()
