namespace Timetabling.Common

[<AutoOpen>]
module Utils =
  let tee f x =
    f x
    x

  open System

  let inline sqrt x = Math.Sqrt(x)
  let inline sqrtn x = Math.Sqrt(float x)
  let inline square x = Math.Pow(x, 2.0)
  let inline min0 x = Math.Max(0.0, x)
  let inline min1 x = Math.Max(1.0, x)
  let inline abs (x : float) = Math.Abs(x)

module Option =
  let inline private (>>=) x f = Option.bind f x

  let rtn v = Some v

  let traverseList f ls =
    let folder head tail =
      f head >>= (fun h -> tail >>= (fun t -> h :: t |> rtn))
    List.foldBack folder ls (rtn List.empty)

  let sequenceList ls = traverseList id ls

  let traverseSeq f ls = ls |> List.ofSeq |> traverseList f
  let sequenceSeq ls = ls |> List.ofSeq |> sequenceList


module Result =
  let inline private (>>=) x f = Result.bind f x

  let rtn v = Ok v

  let traverseList f ls =
    let folder head tail =
      f head >>= (fun h -> tail >>= (fun t -> h :: t |> rtn))
    List.foldBack folder ls (rtn List.empty)

  let sequenceList ls = traverseList id ls

  let traverseSeq f ls = ls |> List.ofSeq |> traverseList f
  let sequenceSeq ls = ls |> List.ofSeq |> sequenceList

  let ofOption error value =
    match value with
    | Some v -> Ok v
    | None -> Error error

  let discard value = value |> Result.map ignore