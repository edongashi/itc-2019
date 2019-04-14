namespace Timetabling.Common

module Option =
  let inline private (>>=) r f = Option.bind f r

  let rtn v = Some v

  let traverseList f ls =
    let folder head tail =
      f head >>= (fun h -> tail >>= (fun t -> h :: t |> rtn))
    List.foldBack folder ls (rtn List.empty)

  let sequenceList ls = traverseList id ls

  let traverseSeq f ls = ls |> List.ofSeq |> traverseList f
  let sequenceSeq ls = ls |> List.ofSeq |> sequenceList


module Result =
  let inline private (>>=) r f = Result.bind f r

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
