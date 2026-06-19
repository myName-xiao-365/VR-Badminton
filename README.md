# VR Badminton

A pixel-style badminton gameplay prototype built from scratch in Unity.

## Requirements

- Unity `2022.3.62f3c1`
- Open `Assets/Scenes/SampleScene.unity`

## Current Features

- Standard badminton court, net, court markings, and surrounding floor
- Pixel-style shuttlecocks, rackets, and player markers
- Forehand and backhand switching
- Front-court and rear-court positioning guides
- Mouse gesture and racket-angle shot detection
- Drop shots, net shots, clears, drives, and smashes
- Colored shuttle trails:
  - Light yellow: clear
  - Green: drop or net shot
  - Pink: drive
  - Red: smash
- Rally scoring and service ownership
- Selectable 15-point or 21-point matches with deuce rules
- Left service court on odd scores and right service court on even scores
- Player upward-swing serving with power-based depth
- Opponent movement, stamina use, shot selection, and mistakes
- Single-player difficulty levels
- Multiplayer mode placeholder

## Controls

| Input | Action |
| --- | --- |
| `W A S D` | Move the player |
| Mouse vertical position | Control racket-face angle |
| Mouse vertical stroke | Swing the racket |
| `Q` | Switch forehand/backhand |
| `Space` | Defend a smash, jump for a clear, or start the opponent's serve |
| `Esc` | Pause the match |

Front-court shots require an upward swing. Rear-court shots require a
downward swing. Move the player onto the cyan positioning marker before
swinging.

Front-court return type is determined by swing power: a soft upward swing
plays a net shot, while a stronger upward swing lifts to the rear court.
The short cyan ground line shows the racket-face center.
It can be disabled from Settings. The live ground projection is shown for
every shot type.

During a high clear, `Space` performs a short half-body jump. During an
opponent smash, the same key prepares the defensive return.

## Difficulty

| Level | Opponent stamina | Smash chance | Smash return chance |
| --- | ---: | ---: | ---: |
| N0 | 30 | 0% | 5% |
| N1 | 50 | 25% | 20% |
| N2 | 70 | 50% | 35% |
| N3 | 100 | 75% | 50% |
| N4 | 200 | 100% | 75% |
| N5 | 500 | 100% | 100% |

Changing difficulty resets the score and starts a new match.

The default setup is N0 with the 21-point format and the racket guide line
disabled. N0 never smashes or uses a backhand. From the rear court it uses
the full forehand overhead preparation and follow-through to play a drop;
from the front court it plays a forehand net shot.

The opponent is represented by a grounded body marker and an independently
animated racket. It selects forehand or backhand from the incoming contact
side, strikes from the racket face, and jumps for smash opportunities.

Settings provides a fullscreen on/off toggle, an expandable resolution
selector, and an on/off toggle for the racket guide line.

## Main Menu

- Enter Match opens Continue Match or New Match.
- New Match is where mode, difficulty, and score format are selected.
- Match difficulty and score format are locked after the match starts.
- Returning to the main menu preserves the score and server.
- An unfinished rally is rolled back and served again when continuing.
- Beginner Tutorial is currently a placeholder.

The colored dashed line on the floor previews the straight ground projection
from the current hit point to the predicted landing point. Shots that cross
the net below net height fall vertically and lose the rally.

## Scoring

- 15-point matches require a two-point lead and are capped at 21
- 21-point matches require a two-point lead and are capped at 30
- Reaching the cap wins immediately
- Changing the score format resets the match

## Opponent Stamina

- Clear: 5
- Smash: 10
- Net shot, drop shot, or lift: 3
- Movement also consumes stamina based on distance
- At zero stamina, the opponent stops moving and hitting
- Stamina resets at the start of each new rally

## Project Status

This is an early gameplay prototype. Models, animation, physics, UI, AI,
online multiplayer, sound, and VR input will continue to evolve.
