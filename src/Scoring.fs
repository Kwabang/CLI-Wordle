module Wordle.Scoring

open Wordle.Types

let private countLetters (s: string) : Map<char, int> =
    s
    |> Seq.fold (fun m c ->
        match Map.tryFind c m with
        | Some n -> Map.add c (n + 1) m
        | None -> Map.add c 1 m) Map.empty

type private PassOne = {
    Greens: Set<int>
    Remaining: Map<char, int>
}

let private decrement (c: char) (m: Map<char, int>) =
    match Map.tryFind c m with
    | Some n -> Map.add c (n - 1) m
    | None -> m

let private markGreens (secret: string) (guess: string) (initial: Map<char, int>) : PassOne =
    [ 0 .. 4 ]
    |> List.fold (fun acc i ->
        match guess.[i] = secret.[i] with
        | true ->
            { Greens = Set.add i acc.Greens
              Remaining = decrement guess.[i] acc.Remaining }
        | false -> acc) { Greens = Set.empty; Remaining = initial }

let private classifyPosition
    (guess: string)
    (greens: Set<int>)
    (i: int)
    (remaining: Map<char, int>)
    : LetterResult * Map<char, int> =
    let c = guess.[i]
    match Set.contains i greens with
    | true -> { Letter = c; Status = Green }, remaining
    | false ->
        match Map.tryFind c remaining with
        | Some n when n > 0 ->
            { Letter = c; Status = Yellow }, Map.add c (n - 1) remaining
        | _ -> { Letter = c; Status = Gray }, remaining

let scoreGuess (secret: string) (guess: string) : LetterResult list =
    let initial = countLetters secret
    let pass1 = markGreens secret guess initial
    let results, _ =
        [ 0 .. 4 ]
        |> List.fold (fun (acc, rem) i ->
            let result, nextRem = classifyPosition guess pass1.Greens i rem
            result :: acc, nextRem) ([], pass1.Remaining)
    List.rev results
