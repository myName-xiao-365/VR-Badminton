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
  status, phone status, and Sensor/Legacy switching. The HUD implementation is
  isolated from the IMGUI file.
- `Match`: match lifecycle, score flow, pause/menu transitions, and difficulty
  application.
- `Flight`: serving, shuttle arcs, hit resolution entry, return flow, net fault
  handling, and trail colors.
- `Opponent`: opponent decisions, stamina spending, movement, racket poses, and
  swing animation. Movement and preparation pose execution are now delegated to
  an App service.
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
- `CourtFaultResolver`: rally winner resolution for net faults currently used by
  runtime flow.
- `MatchState`: score/service/match-winner state transitions.
- `OpponentDecision`: pure opponent shot choice logic.
- `OpponentStaminaModel`: shot and run stamina costs.

Scene execution remains in App: transforms, trails, markers, camera preview,
animations, and coroutine timing stay outside pure logic so gameplay behavior
does not drift during structural cleanup.

## App Services

The App assembly now keeps small scene-facing helpers beside the controller:

- `ShuttleFeedRuntimeHud`: owns the runtime uGUI hierarchy and text updates.
- `OpponentPoseAnimator`: owns opponent racket/body pose interpolation.
- `OpponentMovementRunner`: owns opponent ground movement, preparation pose
  blending, racket face alignment, and run stamina spending.
- `ShuttleFlightRunner`: owns per-frame shuttle movement, net crossing checks,
  slow-motion transitions, and shuttle history callbacks.
- `ShuttleReturnPlanner`: owns scene-facing return-shot target, duration,
  arc-height, and trail-palette choices for existing player/opponent shots.

`ShuttleFeedController` still coordinates lifecycle and coroutines, but these
helpers keep reusable execution details out of the large partial files.

## Input Services

`SensorBadmintonInputSource` owns input fusion between camera pose and phone
racket frames. `MediaPipePoseInputProvider` is file-isolated and keeps the
MediaPipe-enabled and fallback implementations behind the same internal
`IBadmintonPoseInputProvider` boundary.
