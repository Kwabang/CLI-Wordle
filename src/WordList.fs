module Wordle.WordList

open System
open System.IO

type LoadResult =
    | Loaded of words: string[] * allowed: Set<string>
    | FileMissing
    | NoValidWords

let private isLowercaseLetter (c: char) = c >= 'a' && c <= 'z'

let private isValidWord (s: string) =
    s.Length = 5 && Seq.forall isLowercaseLetter s

type private LoadState = {
    Words: string list
    Seen: Set<string>
}

let private emptyState = { Words = []; Seen = Set.empty }

let private foldLine (filename: string) (state: LoadState) (idx: int, raw: string) =
    let lineNum = idx + 1
    let trimmed = raw.Trim()
    match trimmed with
    | "" -> state
    | t when isValidWord t ->
        match Set.contains t state.Seen with
        | true -> state
        | false -> { Words = t :: state.Words; Seen = Set.add t state.Seen }
    | _ ->
        eprintfn "Warning: skipping invalid word at %s:%d: %s" filename lineNum raw
        state

let loadFromLines (lines: string[]) (sourceName: string) : LoadResult =
    let state =
        lines
        |> Array.indexed
        |> Array.fold (foldLine sourceName) emptyState
    match state.Words with
    | [] -> NoValidWords
    | ws -> Loaded(ws |> List.rev |> List.toArray, state.Seen)

let load (path: string) : LoadResult =
    match File.Exists path with
    | false -> FileMissing
    | true -> loadFromLines (File.ReadAllLines path) (Path.GetFileName path)

let pickRandom (rng: Random) (words: string[]) : string =
    words.[rng.Next(words.Length)]

let loadRankedFromLines (lines: string[]) (allowed: Set<string>) : string[] =
    lines
    |> Array.map (fun s -> s.Trim())
    |> Array.filter (fun s -> isValidWord s && Set.contains s allowed)

let loadRanked (path: string) (allowed: Set<string>) : string[] =
    match File.Exists path with
    | false -> [||]
    | true -> loadRankedFromLines (File.ReadAllLines path) allowed
