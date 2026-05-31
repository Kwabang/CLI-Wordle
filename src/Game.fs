module Wordle.Game

open System
open System.IO
open Wordle.Types
open Wordle.Scoring
open Wordle.WordList
module StatsIO = Wordle.Stats

type private Difficulty =
    | TopPercent of int
    | Hardcore

type private RoundOutcome = {
    Result: RoundResult
    Quit: bool
}

let private rankStatus =
    function
    | Green -> 0
    | Yellow -> 1
    | Gray -> 2

let private mergeUsed (used: Map<char, LetterStatus>) (results: LetterResult list) =
    results
    |> List.fold (fun acc r ->
        let lower = Char.ToLower r.Letter
        match Map.tryFind lower acc with
        | None -> Map.add lower r.Status acc
        | Some prev when rankStatus r.Status < rankStatus prev -> Map.add lower r.Status acc
        | _ -> acc) used

let private isAlphaUpper (c: char) = c >= 'A' && c <= 'Z'

let private validShape (s: string) =
    s.Length = 5 && Seq.forall isAlphaUpper s

let private isAsciiLetter (c: char) =
    (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')

let private hasNonEnglishLetter (s: string) =
    s |> Seq.exists (fun c -> not (isAsciiLetter c))

let private renderFrame
    (history: LetterResult list list)
    (used: Map<char, LetterStatus>)
    (lastError: string option) =
    Tui.clear ()
    Tui.drawTitle ()
    Tui.drawGrid history
    Tui.drawKeyboard used
    match lastError with
    | Some msg -> Console.Out.WriteLine("          " + msg)
    | None -> Console.Out.WriteLine()

type private GuessClassification =
    | EndOfInput
    | NonEnglish
    | BadShape
    | NotInDict
    | Valid of lower: string

let private classifyGuess (allowed: Set<string>) (raw: string) : GuessClassification =
    match Option.ofObj raw with
    | None -> EndOfInput
    | Some s ->
        let trimmed = s.Trim()
        match hasNonEnglishLetter trimmed with
        | true -> NonEnglish
        | false ->
            let norm = trimmed.ToUpperInvariant()
            match validShape norm with
            | false -> BadShape
            | true ->
                let lower = norm.ToLowerInvariant()
                match Set.contains lower allowed with
                | false -> NotInDict
                | true -> Valid lower

let rec private attemptLoop
    (secret: string)
    (allowed: Set<string>)
    (history: LetterResult list list)
    (usedMap: Map<char, LetterStatus>)
    (attempt: int)
    (lastError: string option)
    : RoundOutcome =
    renderFrame history usedMap lastError
    Console.Out.WriteLine(sprintf "          Attempt %d / 6" attempt)
    Console.Out.Write("          > ")
    let raw = Console.In.ReadLine()
    match classifyGuess allowed raw with
    | EndOfInput ->
        Console.Out.WriteLine("(EOF)")
        { Result = Loss; Quit = true }
    | NonEnglish ->
        attemptLoop secret allowed history usedMap attempt (Some "Only English letters (A-Z) allowed.")
    | BadShape ->
        attemptLoop secret allowed history usedMap attempt (Some "Guess must be 5 letters.")
    | NotInDict ->
        attemptLoop secret allowed history usedMap attempt (Some "Not in word list.")
    | Valid lower ->
        let results = scoreGuess secret lower
        let newHistory = history @ [ results ]
        let nextUsed = mergeUsed usedMap results
        match lower = secret, attempt with
        | true, _ ->
            renderFrame newHistory nextUsed None
            Console.Out.WriteLine(sprintf "          You win! (%d attempt(s))" attempt)
            Console.Out.WriteLine()
            { Result = Win attempt; Quit = false }
        | false, 6 ->
            renderFrame newHistory nextUsed None
            Console.Out.WriteLine(sprintf "          You lose. The word was: %s" (secret.ToUpperInvariant()))
            Console.Out.WriteLine()
            { Result = Loss; Quit = false }
        | false, _ ->
            attemptLoop secret allowed newHistory nextUsed (attempt + 1) None

let private playRound (rng: Random) (secretPool: string[]) (allowed: Set<string>) : RoundOutcome =
    let secret = pickRandom rng secretPool
    attemptLoop secret allowed [] Map.empty 1 None

let private wantsReplay (input: string) =
    match Seq.tryHead (input.Trim()) with
    | Some 'y'
    | Some 'Y' -> true
    | _ -> false

let rec private replayLoop
    (rng: Random)
    (secretPool: string[])
    (allowed: Set<string>)
    (statsPath: string)
    (stats: Stats)
    : Stats =
    let outcome = playRound rng secretPool allowed
    let updated = StatsIO.update outcome.Result stats
    StatsIO.save statsPath updated
    Tui.drawStatsBlock updated
    match outcome.Quit with
    | true -> updated
    | false ->
        Console.Out.WriteLine()
        Console.Out.Write("          Play again? (y/n): ")
        match Option.ofObj (Console.In.ReadLine()) with
        | None ->
            Console.Out.WriteLine("          Thanks for playing!")
            updated
        | Some answer ->
            match wantsReplay answer with
            | true -> replayLoop rng secretPool allowed statsPath updated
            | false ->
                Console.Out.WriteLine("          Thanks for playing!")
                updated

let private candidatePaths (filename: string) = [
    Path.Combine(Directory.GetCurrentDirectory(), filename)
    Path.Combine(Directory.GetCurrentDirectory(), "assets", filename)
    Path.Combine(AppContext.BaseDirectory, filename)
    Path.Combine(AppContext.BaseDirectory, "assets", filename)
]

let private tryResolveFile (filename: string) : string option =
    candidatePaths filename |> List.tryFind File.Exists

let private readEmbedded (name: string) : string[] option =
    let asm = System.Reflection.Assembly.GetExecutingAssembly()
    match asm.GetManifestResourceStream name with
    | null -> None
    | stream ->
        use sr = new StreamReader(stream)
        sr.ReadToEnd().Split([| '\n' |])
        |> Array.map (fun s -> s.TrimEnd('\r'))
        |> Some

let private loadWords () : LoadResult =
    match tryResolveFile "words.txt" with
    | Some path -> load path
    | None ->
        match readEmbedded "words.txt" with
        | Some lines -> loadFromLines lines "words.txt"
        | None -> FileMissing

let private loadRankedPool (allowed: Set<string>) : string[] =
    match tryResolveFile "words-ranked.txt" with
    | Some path -> loadRanked path allowed
    | None ->
        match readEmbedded "words-ranked.txt" with
        | Some lines -> loadRankedFromLines lines allowed
        | None -> [||]

let private poolSize (rankedCount: int) (pct: int) =
    max 1 (rankedCount * pct / 100)

let private buildSecretPool (difficulty: Difficulty) (ranked: string[]) (allWords: string[]) : string[] =
    match difficulty, ranked with
    | Hardcore, _ -> allWords
    | TopPercent _, [||] -> allWords
    | TopPercent p, r -> r |> Array.truncate (poolSize r.Length p)

let private parseDifficulty (input: string) : Difficulty option =
    match input.Trim().ToLowerInvariant() with
    | ""
    | "30" -> Some(TopPercent 30)
    | "hardcore"
    | "h" -> Some Hardcore
    | s ->
        match Int32.TryParse s with
        | true, n when n >= 1 && n <= 100 -> Some(TopPercent n)
        | _ -> None

let rec private promptDifficulty (rankedCount: int) (stats: Stats) : Difficulty option =
    Tui.clear ()
    Tui.drawTitle ()
    match stats.Played with
    | 0 -> ()
    | _ ->
        Console.Out.WriteLine("          Your stats so far:")
        Tui.drawStatsBlock stats
        Console.Out.WriteLine()
    Console.Out.WriteLine("          Select difficulty.")
    Console.Out.WriteLine("            - Enter a percentage 1-100 to draw the secret from")
    Console.Out.WriteLine(sprintf "              the top N%% of the ranked list (e.g. 10 = %d words," (poolSize rankedCount 10))
    Console.Out.WriteLine(sprintf "              30 = %d words, 100 = %d words). Press Enter for 30." (poolSize rankedCount 30) rankedCount)
    Console.Out.WriteLine("            - Or type HARDCORE (h) to draw from the full dictionary,")
    Console.Out.WriteLine("              including obscure words not in the ranked list.")
    Console.Out.WriteLine()
    Console.Out.Write("          > ")
    match Option.ofObj (Console.In.ReadLine()) with
    | None -> None
    | Some raw ->
        match parseDifficulty raw with
        | Some d -> Some d
        | None -> promptDifficulty rankedCount stats

let private showStatsResetNotice () =
    Tui.clear ()
    Tui.drawTitle ()
    Console.Out.WriteLine("          stats.json failed its integrity check.")
    Console.Out.WriteLine("          The file looks malformed or has been edited by hand,")
    Console.Out.WriteLine("          so the saved statistics cannot be trusted.")
    Console.Out.WriteLine()
    Console.Out.WriteLine("          Press Enter to reset the counters to zero and overwrite")
    Console.Out.WriteLine("          stats.json with a fresh, signed copy.")
    Console.Out.WriteLine()
    Console.Out.Write("          > ")
    Console.In.ReadLine() |> ignore

let private resolveInitialStats (statsPath: string) : Stats =
    match StatsIO.load statsPath with
    | StatsIO.Existing s -> s
    | StatsIO.Missing -> Stats.zero
    | StatsIO.Reset ->
        showStatsResetNotice ()
        StatsIO.save statsPath Stats.zero
        Stats.zero

let run () : int =
    match loadWords () with
    | FileMissing ->
        eprintfn "Error: words.txt not found."
        1
    | NoValidWords ->
        eprintfn "Error: words.txt contains no valid words."
        2
    | Loaded(words, allowed) ->
        let ranked = loadRankedPool allowed
        let statsPath = Path.Combine(Directory.GetCurrentDirectory(), "stats.json")
        let stats = resolveInitialStats statsPath
        match promptDifficulty ranked.Length stats with
        | None ->
            Console.Out.WriteLine("          Thanks for playing!")
            0
        | Some difficulty ->
            let secretPool = buildSecretPool difficulty ranked words
            let rng = Random.Shared
            replayLoop rng secretPool allowed statsPath stats |> ignore
            0
