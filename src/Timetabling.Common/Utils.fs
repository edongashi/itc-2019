module Timetabling.Common.Utils

let optionOfNullable x =
  match x with
  | null -> None
  | x -> Some x

let resultOfOption error =
  function
  | Some s -> Ok s
  | None -> Error error

module Option =
  let (>>=) r f = Option.bind f r
  let rtn v = Some v

  let traverseList f ls =
      let folder head tail =
        f head >>= (fun h -> tail >>= (fun t -> h :: t |> rtn))
      List.foldBack folder ls (rtn List.empty)
  let sequenceList ls = traverseList id ls

module Result =
  let (>>=) r f = Result.bind f r
  let rtn v = Ok v

  let traverseList f ls =
      let folder head tail =
        f head >>= (fun h -> tail >>= (fun t -> h :: t |> rtn))
      List.foldBack folder ls (rtn List.empty)
  let sequenceList ls = traverseList id ls
