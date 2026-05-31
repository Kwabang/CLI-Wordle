module Wordle.Tui

open System
open Wordle.Types

let private esc = string (char 0x1B)

let private colorOf =
    function
    | Green -> 2
    | Yellow -> 3
    | Gray -> 8

let private cell (c: char) (color: int) =
    sprintf "%s[1;38;5;%dm %c %s[0m" esc color c esc

let private emptyCell () =
    sprintf "%s[38;5;240m . %s[0m" esc esc

let private leftMargin = "          "

let clear () =
    Console.Out.Write(sprintf "%s[2J%s[H" esc esc)

let drawTitle () =
    Console.Out.WriteLine()
    Console.Out.WriteLine(leftMargin + "    W O R D L E")
    Console.Out.WriteLine(leftMargin + " ----------------- ")
    Console.Out.WriteLine()

let private drawRow (history: LetterResult list list) (i: int) =
    Console.Out.Write(leftMargin)
    match List.tryItem i history with
    | Some row ->
        for r in row do
            Console.Out.Write(cell (Char.ToUpper r.Letter) (colorOf r.Status))
    | None ->
        for _ in 0 .. 4 do
            Console.Out.Write(emptyCell ())
    Console.Out.WriteLine()

let drawGrid (history: LetterResult list list) =
    for i in 0 .. 5 do
        drawRow history i
    Console.Out.WriteLine()

let drawKeyboard (used: Map<char, LetterStatus>) =
    let rows = [ "QWERTYUIOP"; "ASDFGHJKL"; "ZXCVBNM" ]
    let indents = [ ""; "  "; "     " ]
    for row, indent in List.zip rows indents do
        Console.Out.Write(leftMargin + indent)
        for c in row do
            let lower = Char.ToLower c
            let color =
                match Map.tryFind lower used with
                | Some s -> colorOf s
                | None -> 255
            Console.Out.Write(cell c color)
        Console.Out.WriteLine()
    Console.Out.WriteLine()

let drawStatsBlock (s: Stats) =
    Console.Out.WriteLine(leftMargin + sprintf "Games played : %d" s.Played)
    let rate =
        match s.Played with
        | 0 -> "0.0"
        | _ -> (100.0 * float s.Won / float s.Played).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
    Console.Out.WriteLine(leftMargin + sprintf "Win rate     : %s%%" rate)
    let avg =
        match s.Won with
        | 0 -> "N/A"
        | _ -> (float s.TotalAttempts / float s.Won).ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
    Console.Out.WriteLine(leftMargin + sprintf "Avg attempts : %s" avg)
