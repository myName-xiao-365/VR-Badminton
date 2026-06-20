# VR Badminton Architecture

This stage keeps the current gameplay behavior intact while separating the code
into assemblies and partial controller modules.

## Assemblies

- `VRBadminton.Input`: keyboard/mouse input, phone IMU HTTP input, MediaPipe
  pose input, and sensor data mapping. This is the only project assembly that
  references `Mediapipe.Runtime`.
- `VRBadminton.Gameplay`: pure gameplay rules and hit resolution logic. This
  includes match rules, difficulty tuning, shuttle flight and trajectory value
  objects, court/rally outcome helpers, opponent strategy/stamina helpers, and
  `RacketHitResolver`.
- `VRBadminton.App`: scene-facing orchestration. `ShuttleFeedController` stays
  as the MonoBehaviour entry point and is split into partial files by concern.

## ShuttleFeedController Partials

- `Input`: input source switching, racket history, hit settings, and sensor
  debug formatting.
- `Camera`: Switch-style camera preset and follow behavior.
- `UI`: remaining IMGUI menus, camera preview, pause/settings overlays, and
  debug panels.
- `Hud`: runtime uGUI HUD for score, opponent stamina, input status, camera
  status, phone status, and Sensor/Legacy switching.
- `Match`: match lifecycle, score flow, pause/menu transitions, and difficulty
  application.
- `Flight`: serving, shuttle arcs, hit resolution entry, return flow, net fault
  handling, and trail colors.
- `Opponent`: opponent decisions, stamina spending, movement, racket poses, and
  swing animation.
- `Factory`: runtime creation of the pixel-style shuttle, rackets, markers,
  guide meshes, and materials.

## Runtime Sensor Assets

Editor mode uses MediaPipe package resources through `LocalResourceManager`.
Windows Player uses `StreamingAssetsResourceManager` and expects:

```text
Assets/StreamingAssets/VRBadminton/MediaPipe/pose_landmarker_lite.bytes
```

The public `IBadmintonInputSource` shape is unchanged. The internal
`IMediaPipeAssetProvider` only selects the MediaPipe model source for the
current runtime.

## Performance Markers

Lightweight `ProfilerMarker` scopes were added for:

- active input source tick
- sensor input tick
- MediaPipe pose inference
- hit resolver calls
- shuttle movement updates
- IMGUI rendering

These markers are for baseline inspection only and do not change gameplay.

## Next-Stage Split

The current architecture keeps `ShuttleFeedController` as the composition root:
it owns Unity object references, lifecycle methods, and coroutine entry points.
The next-stage cleanup moved these previously inline rules into Gameplay:

- `ShuttleTrajectoryPlanner` / `ShuttleTrajectory`: current runtime arc shape
  and descending contact sampling.
- `CourtFaultResolver`: rally winner resolution for net and landing faults.
- `RallyOutcomeResolver`: score/service/match-winner state transitions.
- `OpponentStrategy`: pure opponent shot choice wrapper.
- `OpponentStaminaModel`: shot and run stamina costs.

Scene execution remains in App: transforms, trails, markers, camera preview,
animations, and coroutine timing stay outside pure logic so gameplay behavior
does not drift during structural cleanup.
