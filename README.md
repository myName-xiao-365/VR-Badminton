# VR Badminton

A pixel-style badminton gameplay prototype built from scratch in Unity.

The default input mode is **Sensor**:

- A Windows webcam drives player position and right-hand racket height through MediaPipe Pose.
- A phone browser streams IMU orientation and angular velocity for racket rotation and swing speed.

The in-game input panel can switch back to **Legacy** keyboard/mouse controls at any time.

## Requirements

- Windows 10/11 x64.
- Unity `2022.3.62f3`.
- A webcam visible to Unity `WebCamTexture`.
- Android Chrome on the phone is the primary tested target.
- PC and phone must be on the same LAN for Sensor phone input.

Open:

```text
Assets/Scenes/SampleScene.unity
```

## Package Setup

### MediaPipe Unity Plugin

This project uses homuler MediaPipe Unity Plugin `v0.16.3`.

The package is referenced as a local tarball because Unity package resolution
does not reliably pull the release tarball and required large binaries directly
from GitHub:

```json
"com.github.homuler.mediapipe": "file:../.packages/com.github.homuler.mediapipe-0.16.3.tgz"
```

Download the official release package:

```text
https://github.com/homuler/MediaPipeUnityPlugin/releases/download/v0.16.3/com.github.homuler.mediapipe-0.16.3.tgz
```

Place it here:

```text
.packages/com.github.homuler.mediapipe-0.16.3.tgz
```

The `.packages/` directory is intentionally ignored by Git so the large tarball
is not committed.

### Unity Package Registry

The project currently uses Unity's default package registry:

```text
https://packages.unity.com
```

If your Unity install is configured for another mirror, let Unity resolve the
packages once and inspect `Packages/packages-lock.json` before committing lock
file changes.

## Sensor Mode

Sensor mode is selected by default.

### Camera Input

- The camera path is native Unity only; there is no camera webpage.
- Unity starts `WebCamTexture`, sends frames to MediaPipe Pose, and maps pose landmarks into player movement.
- The left-bottom UI preview shows the webcam image and MediaPipe skeleton.
- MediaPipe inference is intentionally not horizontally mirrored so left/right hand labels remain correct.
- The preview UI is horizontally mirrored separately so it behaves like a familiar selfie view.

Camera controls:

| Body movement | Game effect |
| --- | --- |
| Move left/right | Player moves left/right |
| Move forward/back | Player moves forward/back on court |
| Raise/lower right hand | Racket height changes continuously |

If the camera pose becomes stale, the last player/racket position is held and
camera-dependent updates are ignored until pose tracking returns. The game does
not silently switch to Legacy mode.

### Phone Racket Input

Unity starts a small local HTTP server for the phone page.

Default preferred port:

```text
8092
```

If the port is busy, the server tries the next ports up to `preferredPort + 19`.
The active URL is shown in the in-game UI, for example:

```text
http://192.168.1.23:8092/phone.html
```

Open that exact URL on the phone while Unity Play Mode is running.

Phone page workflow:

1. Tap `Start sensors`.
2. Grant motion/orientation permission if prompted.
3. Record the three static calibration poses shown on the phone page.
4. Hold the phone firmly as the racket handle and swing.

The phone posts frames to:

```text
/racket-frame
```

Unity uses:

- `rotationMatrix` / `orientation` for racket rotation.
- `angularVelocity` / `angularSpeed` for swing detection and power.
- Three-pose calibration on the phone page for stable racket orientation.

## Network And Firewall Setup

The phone must be able to reach the PC over LAN.

Checklist:

- PC and phone are on the same Wi-Fi/LAN.
- Disable phone mobile data temporarily if it keeps routing away from Wi-Fi.
- Do not use a guest Wi-Fi network with client isolation enabled.
- Allow Unity Editor through Windows Defender Firewall on **Private networks**.
- Allow inbound TCP traffic to the phone server port, usually `8092`.
- If Unity picked another port, use the exact URL shown in the game UI.

Windows Firewall manual rule:

1. Open `Windows Defender Firewall with Advanced Security`.
2. Create an inbound rule.
3. Rule type: `Port`.
4. Protocol: `TCP`.
5. Port: `8092` or the active port shown in Unity.
6. Action: `Allow the connection`.
7. Profile: at least `Private`.
8. Name: `VR Badminton Phone Input`.

If the UI shows an address that the phone cannot open:

- Check the PC IP with `ipconfig`.
- Prefer the Wi-Fi adapter IPv4 address.
- Manually try:

```text
http://<PC_WIFI_IPV4>:8092/phone.html
```

If the page opens on the PC but not on the phone, it is almost always firewall,
VPN, guest Wi-Fi/client isolation, wrong adapter IP, or a different active port.

If the page opens but sensors do not run:

- Use Android Chrome first.
- Tap `Start sensors` from a direct user gesture.
- Check browser sensor permissions.
- iOS/Safari may require HTTPS for motion permissions; plain LAN HTTP is not
  the first supported target.

## Legacy Mode

Use the in-game input panel to switch to Legacy.

| Input | Action |
| --- | --- |
| `W A S D` | Move the player |
| Mouse vertical position | Control racket-face angle |
| Mouse vertical stroke | Swing the racket |
| `Q` | Switch forehand/backhand |
| `Space` | Prepare to receive an opponent smash |

Front-court shots require an upward swing. Rear-court shots require a downward
swing. Move the player onto the cyan positioning marker before swinging.

## Current Features

- Standard badminton court, net, court markings, and surrounding floor.
- Pixel-style shuttlecocks, rackets, and player markers.
- Sensor and Legacy input modes.
- Camera pose player tracking.
- Phone IMU racket rotation and swing detection.
- Forehand/backhand inference in Sensor mode and `Q` switching in Legacy mode.
- Front-court and rear-court positioning guides.
- Drop shots, net shots, clears, drives, and smashes.
- Colored shuttle trails:
  - Light yellow: clear
  - Green: drop or net shot
  - Pink: drive
  - Red: smash
- Rally scoring and service ownership.
- Left service court on odd scores and right service court on even scores.
- Player upward-swing serving with power-based depth.
- Opponent movement, stamina use, shot selection, and mistakes.
- Single-player difficulty levels.
- Multiplayer mode placeholder.

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
- Movement also consumes stamina based on distance.
- At zero stamina, the opponent stops moving and hitting.
- Stamina resets at the start of each new rally.

## Verification

This workspace may not have a full standalone .NET SDK available, so `dotnet
build` is not the baseline check.

Recommended checks:

- Open Unity Editor and wait for script compilation.
- Run Unity Test Runner EditMode tests.
- In Play Mode, test Sensor mode with webcam and phone.
- Switch to Legacy and re-test keyboard/mouse behavior.

The current development fallback is compiling the Unity-generated Roslyn
response files under `Library/Bee/artifacts`.

## Troubleshooting

### Camera

- If the UI says `warming up`, wait a few seconds and ensure no other app is
  using the camera.
- If there is no camera device, check Windows camera privacy settings.
- If the skeleton is missing or stale, stand fully in frame with shoulders and
  hips visible.
- If right-hand height feels jumpy, make sure the right hand is visible and not
  crossing behind the torso.

### Phone

- If `Phone URL: unavailable`, Sensor mode has not started or the local server
  failed to bind a port.
- If the phone page cannot open, verify LAN, firewall, port, and IP address.
- If `Sent` increments on the phone but Unity says waiting for phone, check that
  the URL host is the PC running Unity, not `127.0.0.1`.
- If swing speed stays at `0`, check browser motion permission and that the phone
  is not in a restricted browser/webview.

## Project Status

This is an early gameplay prototype. Models, animation, physics, UI, AI, online
multiplayer, sound, and VR input will continue to evolve.
