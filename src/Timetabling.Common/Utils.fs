namespace Timetabling.Common

[<AutoOpen>]
module Utils =
  let tee f x =
    f x
    x

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
