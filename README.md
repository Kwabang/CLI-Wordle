# CLI-Wordle

A command-line Wordle clone written in F# on .NET 10. Guess a hidden 5-letter
English word in six tries.

## How to play

### Picking a difficulty

If you have played before, your cumulative stats from `stats.json` are shown
above the difficulty prompt so you can see how you have been doing without
playing a round first. Fresh players (no `stats.json`, or a freshly reset
file) see no stats block above the prompt.

When the game starts it asks you to pick a difficulty:

```
          Select difficulty.
            - Enter a percentage 1-100 to draw the secret from
              the top N% of the ranked list (e.g. 10 = 875 words,
              30 = 2626 words, 100 = 8754 words). Press Enter for 30.
            - Or type HARDCORE (h) to draw from the full dictionary,
              including obscure words not in the ranked list.
```

Accepted input shapes:

| Input                | Secret pool                                                   |
| -------------------- | ------------------------------------------------------------- |
| an integer `1`-`100` | Top `N%` of `assets/words-ranked.txt` (rounded down, min 1).  |
| empty (just Enter)   | Same as `30` — top 30% of the ranked list.                    |
| `hardcore` or `h`    | Full `assets/words.txt` dictionary (14,855 words).            |

Anything else (e.g. `abc`, `0`, `150`) is rejected and you are re-prompted.

So `10` gives you a ~875-word "common words only" secret pool, `30` gives you
the default ~2,626-word pool, `100` gives you all 8,754 ranked words, and
`hardcore` adds the ~6,100 words from the full dictionary that are absent
from the Norvig frequency table (`aahed`, `aalii`, etc.) — `hardcore` is the
only way one of those obscure words is picked as the secret.

The accepted-guess set is always the full `assets/words.txt` dictionary, so
an obscure word is allowed *as a guess* at any difficulty — only the
*secret* the game picks for you is restricted.

`assets/words-ranked.txt` is built by intersecting the full 5-letter word
list with Peter Norvig's English-word frequency table
(https://norvig.com/ngrams/count_1w.txt, derived from Google's web n-grams)
and sorting by descending frequency.

### The goal

The selected pool produces one random 5-letter secret word at the start of
every round. You have six attempts to figure out what it is.

### Entering a guess

The game shows a prompt:

```
          Attempt 1 / 6
          > 
```

Type any 5-letter English word and press Enter. Case does not matter
(`crane`, `CRANE`, and `  Crane  ` all work; the input is trimmed and
upper-cased internally).

If your input is not accepted, you stay on the same attempt number and are
re-prompted. The three rejection messages you may see are:

| Message                              | Meaning                                              |
| ------------------------------------ | ---------------------------------------------------- |
| `Guess must be 5 letters.`           | All characters are A–Z letters but the length is not exactly 5. |
| `Only English letters (A-Z) allowed.`| The input contains a non-A-Z character: digits, symbols, or non-English letters (한글, 中文, accents, emoji, Turkish dotless `ı`, etc.). |
| `Not in word list.`                  | The word is a well-formed 5-letter A–Z string but is not in the dictionary. |

### Reading the feedback

After every accepted guess the game redraws the screen with your past
attempts in a 6×5 grid and a QWERTY keyboard whose keys are tinted by the
best status you have seen for each letter:

| Tile color   | Meaning                                                |
| ------------ | ------------------------------------------------------ |
| Green        | Right letter in the right position.                    |
| Yellow       | Right letter, but in a different position.             |
| Dark gray    | Letter is not in the secret word at this occurrence.   |
| Bright white | Letter has not been used yet (keyboard cells only).    |

Letter multiplicity follows standard Wordle "no double credit" rules. For
example, if the secret is `apple` and you guess `puppy`, only one of the
three `p`s in your guess is rewarded (the position-3 `p` is green and
consumes the second `p` of `apple`; the position-1 `p` is yellow against
the remaining `p`; the position-4 `p` is gray).

### Winning, losing, and replaying

- If your guess matches the secret, the game prints
  `You win! (N attempt(s))` and ends the round.
- If you use all six attempts without finding the word, the game prints
  `You lose. The word was: <SECRET>` and ends the round.
- Pressing Ctrl-D (EOF) during a guess forfeits the current round as a
  loss and exits the program.

After every round the game shows a stats block:

```
          Games played : 3
          Win rate     : 66.7%
          Avg attempts : 3.50
```

`Win rate` is `100 × won / played`, formatted with one decimal. `Avg attempts`
is `totalAttempts / won` formatted with two decimals, or `N/A` if you have
not won any round yet.

The stats persist between runs in a `stats.json` file in the working
directory. The file carries an HMAC-SHA256 integrity tag. If the file is
missing, malformed, or its HMAC does not match, the game prints
`Warning: stats.json failed integrity check; resetting statistics.` to
standard error and shows a notice screen explaining that the file looks
malformed or was edited by hand. Pressing Enter on that screen immediately
resets the counters to zero and overwrites `stats.json` with a fresh,
signed copy, then continues to the difficulty prompt.

Finally the game asks:

```
          Play again? (y/n): 
```

Answer `y` or `Y` to start a fresh round (a new secret word is drawn and the
per-round keyboard colors are reset; only the stats carry over). Any other
answer, including EOF, prints `Thanks for playing!` and exits cleanly.

### Example session

```
              W O R D L E
           -----------------

           C  R  A  N  E
           B  R  A  V  E
           .  .  .  .  .
           .  .  .  .  .
           .  .  .  .  .
           .  .  .  .  .

           Q  W  E  R  T  Y  U  I  O  P
             A  S  D  F  G  H  J  K  L
                Z  X  C  V  B  N  M

          You win! (2 attempt(s))

          Games played : 1
          Win rate     : 100.0%
          Avg attempts : 2.00

          Play again? (y/n): n
          Thanks for playing!
```

(Letters in the grid and on the keyboard are colored according to the rules
above; the plain text shown here is what you would see in a terminal
without ANSI color support.)

## How to install and run

You need the .NET 10 SDK
(https://dotnet.microsoft.com/download/dotnet/10.0) and a terminal that
understands ANSI 256-color escape codes (modern Linux/macOS terminals,
Windows Terminal, or Windows 10+ console).

From the root of this repository:

```sh
dotnet run --project Wordle.fsproj
```

On the first launch packages are restored and the project is built; later
launches start immediately. To build once and then run the produced
executable directly:

```sh
dotnet build -c Release
dotnet bin/Release/net10.0/Wordle.dll
```

Both word-list files are also embedded into the compiled assembly as
resources, so the DLL can run without `words.txt` / `words-ranked.txt` on
disk. For each file the program first checks the filesystem (`./<file>`,
`./assets/<file>`, `<exe-dir>/<file>`, `<exe-dir>/assets/<file>`) and falls
back to the embedded copy if no file is found. If you want to override the
embedded word list, drop a file with the same name next to the DLL and it
will be picked up instead.

### Single-file native build

If you want one self-contained executable with no dependency on `dotnet`
being installed on the target machine, use `dotnet publish` with single-file
and self-contained options:

```sh
# Linux x64
dotnet publish -c Release -r linux-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeAllContentInSingleFile=true \
    -p:EnableCompressionInSingleFile=true

# macOS Apple Silicon (arm64)
dotnet publish -c Release -r osx-arm64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeAllContentInSingleFile=true \
    -p:EnableCompressionInSingleFile=true

# macOS Intel (x64)
dotnet publish -c Release -r osx-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeAllContentInSingleFile=true \
    -p:EnableCompressionInSingleFile=true

# Windows x64
dotnet publish -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeAllContentInSingleFile=true \
    -p:EnableCompressionInSingleFile=true
```

The result is a single binary at
`bin/Release/net10.0/<rid>/publish/Wordle` (or `Wordle.exe` on Windows)
that bundles the .NET 10 runtime, every assembly, and the embedded word
lists into one file. Copy that one file to the target machine and run it
directly:

```sh
./Wordle
```

No additional files, no installed runtime, no `dotnet` command — just the
single binary.

No external NuGet packages are required; the program uses only the .NET 10
base class library.

## Project layout

```
CLI-Wordle/
├── README.md
├── LICENSE
├── .gitignore
├── Wordle.fsproj
├── src/
│   ├── Types.fs       discriminated unions and record types
│   ├── WordList.fs    load and validate words.txt
│   ├── Scoring.fs     two-pass scoring (greens then yellows/grays)
│   ├── Stats.fs       read/update/persist stats.json with HMAC-SHA256
│   ├── Tui.fs         full-screen TUI: title, 6x5 grid, QWERTY keyboard
│   ├── Game.fs        attempt loop, round loop, replay loop
│   └── Program.fs     entry point; delegates to Game.run
└── assets/
    ├── words.txt          14,855 5-letter words (public valid-Wordle-words list)
    └── words-ranked.txt   8,754 words ranked by English-corpus frequency
                           (words.txt ∩ Norvig count_1w, descending by count)
```

The word list comes from a public valid-Wordle-words gist
(https://gist.github.com/dracos/dd0668f281e685bad51479e5acaadb93) that
mirrors the dictionary used by the original Wordle web game. Every word in
`assets/words.txt` is an accepted guess at every difficulty; whether it can
also be picked as the secret depends on the difficulty (see "Picking a
difficulty" above).

## Changes to the requirements after the proposal

The final implementation differs from the proposal in three ways. The
underlying game logic (scoring, validation, win/loss conditions, statistics
persistence, replay loop) is unchanged.

1. **Full-screen TUI rendering instead of scrolling output.** The proposal
   describes a scrolling output model in which each accepted guess prints a
   colored feedback row and a 26-cell A–Z used-letters strip on new lines
   underneath the previous attempts (Requirements 8 and 10, Section 4.3). The
   final implementation clears the terminal at the start of every attempt and
   redraws a fixed-position 6x5 grid of past guesses together with a QWERTY
   keyboard whose keys are colored by their best-known status. This is the
   visual model used by the original Wordle web game and makes the per-round
   state much easier to scan than a long scrolling log. Justification: the
   proposal repeatedly stresses that the player should not have to scroll back
   to see previously-tried letters, which a fixed-position TUI satisfies more
   directly than the scrolling 26-cell strip. The QWERTY layout replaces the
   alphabetical A–Z strip because it is what players actually expect to see in
   a Wordle clone; the per-letter coloring rule (Green > Yellow > Gray > Unused)
   is preserved exactly.
2. **Distinct error message for non-English input.** Requirement 6 in the
   proposal collapses all non-`A`-`Z` input into the single
   `Guess must be 5 letters.` message. The final implementation adds one extra
   classification: if the trimmed input contains any non-A-Z character
   (digits, symbols, Korean, Chinese, accented Latin, emoji, Turkish dotless
   `ı`, etc.), the message `Only English letters (A-Z) allowed.` is shown
   instead. Justification: the original message is confusing when the user
   types a 5-character non-letter input (e.g. `12345` or `한국어무`) and is
   told the input is not 5 *letters*, and Turkish dotless `ı` would actually
   be uppercased to `I` by `ToUpperInvariant` and silently slip through the
   original A-Z range check. The attempt counter is still not incremented in
   either case, so the failure mode required by Requirement 6 is preserved.
3. **Difficulty selector restricts the secret pool.** Requirement 3 in the
   proposal draws the secret uniformly at random from the entire loaded word
   list. The final implementation asks the player to pick a difficulty at
   startup: an integer percentage `1`-`100` restricts the secret pool to
   the top `N%` of `assets/words-ranked.txt` (a frequency-ranked subset of
   the full 5-letter word list, sorted by Norvig's count_1w table), and the
   special answer `HARDCORE` draws from the full dictionary, including the
   obscure words that are not in Norvig's frequency table. The secret is
   then drawn uniformly at random from the selected subset. Justification:
   the full word list contains many obscure words (e.g. `aahed`, `aalii`)
   that make a 10% game frustrating; restricting the secret pool to common
   English words turns the easier tiers into a real Wordle experience while
   preserving the uniform-random selection rule for the chosen subset.
   `HARDCORE` preserves the proposal behavior exactly. The accepted-guess
   set is still the full dictionary at every difficulty, so this change does
   not restrict what the player can type.

All other text strings (`Attempt N / 6`, `> ` prompt, `Guess must be 5
letters.`, `Not in word list.`, `You win! (N attempt(s))`,
`You lose. The word was: <SECRET>`, the three-line statistics block with the
exact F1/F2 formatting, the `Play again? (y/n):` replay prompt, and
`Thanks for playing!`) are emitted verbatim as required, only with a fixed
left margin so they line up with the grid. The ANSI 256-color indices for
Green (2), Yellow (3), Gray (8), and Unused (255) and the per-cell escape
sequence format from Requirement 8 are preserved exactly. The `stats.json`
file format and the HMAC-SHA256 integrity check from Requirements 13–16 are
preserved exactly.

## Use of Large Language Models

Anthropic's Claude Code was used to scaffold the project
structure: the `Wordle.fsproj` file, the per-module split under `src/`, the
`assets/` and `.gitignore` setup, and the README layout. The main game code
(`Types.fs`, `WordList.fs`, `Scoring.fs`, `Stats.fs`, `Tui.fs`, `Game.fs`,
`Program.fs`) was then written by hand against the requirements document.
For a few parts that were harder to implement from scratch — the HMAC-SHA256
integrity tag and atomic write in `Stats.fs`, the ANSI 256-color escape-code
emission in `Tui.fs`, and the screen-clear / cursor-home control sequence —
Claude's help was used to fill in the .NET API details.
