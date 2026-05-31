module Wordle.Stats

open System
open System.IO
open System.Text
open System.Text.Json
open System.Security.Cryptography
open Wordle.Types

let private secretKey : byte[] =
    Encoding.UTF8.GetBytes("cs20200-cli-wordle")

let private canonicalMessage (s: Stats) =
    sprintf "played=%d;won=%d;totalAttempts=%d" s.Played s.Won s.TotalAttempts

let private toHex (bytes: byte[]) =
    bytes
    |> Array.map (sprintf "%02x")
    |> String.concat ""

let private fromHex (s: string) : byte[] option =
    match s.Length % 2 with
    | 0 ->
        try
            Array.init (s.Length / 2) (fun i ->
                Convert.ToByte(s.Substring(i * 2, 2), 16))
            |> Some
        with _ -> None
    | _ -> None

let hmacHex (s: Stats) : string =
    let msg = Encoding.UTF8.GetBytes(canonicalMessage s)
    HMACSHA256.HashData(secretKey, msg) |> toHex

let update (result: RoundResult) (s: Stats) : Stats =
    match result with
    | Win n ->
        { Played = s.Played + 1
          Won = s.Won + 1
          TotalAttempts = s.TotalAttempts + n }
    | Loss -> { s with Played = s.Played + 1 }

let private warn () =
    eprintfn "Warning: stats.json failed integrity check; resetting statistics."

let private tryGetProp (root: JsonElement) (name: string) : JsonElement option =
    match root.TryGetProperty name with
    | true, el -> Some el
    | _ -> None

let private tryGetInt (root: JsonElement) (name: string) : int option =
    match tryGetProp root name with
    | Some el when el.ValueKind = JsonValueKind.Number ->
        match el.TryGetInt32() with
        | true, v -> Some v
        | _ -> None
    | _ -> None

let private tryGetStr (root: JsonElement) (name: string) : string option =
    match tryGetProp root name with
    | Some el when el.ValueKind = JsonValueKind.String -> Some(el.GetString())
    | _ -> None

let private verifyHmac (candidate: Stats) (hex: string) : bool =
    match fromHex hex, fromHex (hmacHex candidate) with
    | Some hBytes, Some eBytes ->
        CryptographicOperations.FixedTimeEquals(ReadOnlySpan hBytes, ReadOnlySpan eBytes)
    | _ -> false

let private parseStats (root: JsonElement) : Stats option =
    match root.ValueKind with
    | JsonValueKind.Object ->
        match tryGetInt root "played",
              tryGetInt root "won",
              tryGetInt root "totalAttempts",
              tryGetStr root "hmac" with
        | Some p, Some w, Some t, Some h ->
            let candidate = { Played = p; Won = w; TotalAttempts = t }
            match verifyHmac candidate h with
            | true -> Some candidate
            | false -> None
        | _ -> None
    | _ -> None

type LoadOutcome =
    | Existing of Stats
    | Missing
    | Reset

let load (path: string) : LoadOutcome =
    match File.Exists path with
    | false -> Missing
    | true ->
        try
            let text = File.ReadAllText(path, Encoding.UTF8)
            use doc = JsonDocument.Parse text
            match parseStats doc.RootElement with
            | Some s -> Existing s
            | None ->
                warn ()
                Reset
        with _ ->
            warn ()
            Reset

let save (path: string) (s: Stats) : unit =
    let json =
        sprintf "{\"played\":%d,\"won\":%d,\"totalAttempts\":%d,\"hmac\":\"%s\"}\n"
            s.Played s.Won s.TotalAttempts (hmacHex s)
    let dir =
        match Path.GetDirectoryName path with
        | null
        | "" -> "."
        | d -> d
    let tmp = Path.Combine(dir, Path.GetFileName path + ".tmp")
    let utf8NoBom = UTF8Encoding false
    File.WriteAllText(tmp, json, utf8NoBom)
    match File.Exists path with
    | true -> File.Replace(tmp, path, null)
    | false -> File.Move(tmp, path)
