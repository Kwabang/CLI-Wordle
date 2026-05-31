module Wordle.Types

type LetterStatus =
    | Green
    | Yellow
    | Gray

type LetterResult = { Letter: char; Status: LetterStatus }

type RoundResult =
    | Win of attemptsUsed: int
    | Loss

type Stats = {
    Played: int
    Won: int
    TotalAttempts: int
}

module Stats =
    let zero = { Played = 0; Won = 0; TotalAttempts = 0 }
