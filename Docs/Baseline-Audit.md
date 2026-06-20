# Baseline Audit

Baseline tag: `stage-base-2026-06-20-8537b4c`

Work branch: `codex/foundation-cleanup`

Unity version: `2022.3.62f3`

## Current Health

- The working baseline was clean at `8537b4c`.
- Unity MCP server was reachable at `mcp-for-unity-server 3.4.2`.
- EditMode tests passed before cleanup: `22 passed, 0 failed`.
- The active scene is `Assets/Scenes/SampleScene.unity`.

## Cleanup Findings

- `ShuttleFeedController` had grown into a single multi-responsibility file.
  The stage cleanup keeps it as the MonoBehaviour entry point but splits methods
  into concern-based partial files.
- Build Settings had no scenes listed. `SampleScene` is now included for Player
  builds.
- Sensor mode used an Editor-only MediaPipe resource path. The Player path now
  uses `StreamingAssets`.
- `Builds/` contains local ignored Player output; only the tracked README and
  gameplay guide are source artifacts.
- MCP for Unity is referenced as `#main`; rely on `Packages/packages-lock.json`
  when reviewing dependency drift.

## Known Limits

- Gameplay HUD is partially migrated to runtime uGUI. Main menu, pause/settings,
  camera preview, and debug overlays still use IMGUI.
- Multiplayer and tutorial entries remain placeholders.
- Sensor validation still requires real webcam and phone hardware on the same
  LAN.
- Phone input defaults to TCP `8093`. If the phone page opens on the PC but not
  on the phone, treat Windows Firewall, VPN, guest Wi-Fi/client isolation,
  wrong adapter IP, or a different active port as the first diagnostics.
- The Unity console may show MCP SkillSync GitHub rate-limit errors; these are
  tool integration errors, not project compile errors.

## Post-Cleanup Verification

- EditMode tests: `29 passed, 0 failed, 0 skipped`.
- Windows Development Player: build succeeded for `StandaloneWindows64` at
  `Builds/StandaloneWindows64/VR Badminton.exe` with `0 errors, 0 warnings`.
- Player StreamingAssets include
  `VR Badminton_Data/StreamingAssets/VRBadminton/MediaPipe/pose_landmarker_lite.bytes`.
- Unity console was cleared and rechecked after verification: `0 log entries`.
- Hardware smoke for webcam and phone Sensor mode remains manual because it
  requires local devices on the same LAN.

## Verification Contract

1. Wait for Unity compilation to finish.
2. Check console errors, ignoring known MCP SkillSync rate-limit noise.
3. Run all EditMode tests.
4. Build a Windows Development Player with `SampleScene`.
5. Launch the Player and verify Sensor mode can start, display the phone URL,
   and switch to Legacy mode.

## Unity Asset Import Note

When new `.cs` files are created from outside the Unity Editor, do not rely on
filesystem refresh alone. Import the new paths through Unity MCP
`manage_asset(action="import")` or an equivalent `AssetDatabase.ImportAsset`
call before treating compiler errors as code problems. This keeps the Unity
assembly graph aligned with files created by external agents.
