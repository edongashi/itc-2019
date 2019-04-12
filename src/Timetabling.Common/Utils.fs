module Timetabling.Common.Utils

let optionOfNullable x =
  match x with
  | null -> None
  | x -> Some x

let errorOfOption error =
  function
  | Some s -> Ok s
  | None -> Error error
