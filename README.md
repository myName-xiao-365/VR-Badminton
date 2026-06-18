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
| `Space` | Prepare to receive an opponent smash |

Front-court shots require an upward swing. Rear-court shots require a
downward swing. Move the player onto the cyan positioning marker before
swinging.

## Difficulty

| Level | Opponent stamina | Smash chance | Smash return chance |
| --- | ---: | ---: | ---: |
| N0 | 30 | 10% | 5% |
| N1 | 50 | 25% | 20% |
| N2 | 70 | 50% | 35% |
| N3 | 100 | 75% | 50% |

Changing difficulty resets the score and starts a new match.

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

