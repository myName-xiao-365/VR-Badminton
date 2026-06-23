using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using VRBadminton.Gameplay;
using VRBadminton.Input;

namespace VRBadminton.App
{
    public sealed partial class ShuttleFeedController
    {
        private void CreateInputSources()
        {
            legacyInputSource = new LegacyBadmintonInputSource(
                playerGroundPosition,
                racketMoveSpeed,
                minimumSwingSpeed,
                upwardOutSpeed,
                minimumAngleTravel);
            sensorInputSource = new SensorBadmintonInputSource(
                playerGroundPosition,
                sensorPhonePort,
                sensorAngularSpeedToGameSpeed,
                upwardOutSpeed);
        }

        private void ActivateInputMode(BadmintonInputMode mode)
        {
            activeInputSource?.Stop();
            activeInputMode = mode;
            activeInputSource = mode == BadmintonInputMode.Sensor
                ? sensorInputSource
                : legacyInputSource;
            activeInputSource?.Start();
            inputSnapshot = activeInputSource?.Snapshot ?? BadmintonInputSnapshot.Default();
            hasLastSensorVirtualZ = false;
            inputSourceStarted = true;
            if (mode == BadmintonInputMode.Sensor && screenState == ScreenState.Playing)
            {
                LogSensorHitDebug(
                    "sensor_mode_active",
                    $"phonePort={sensorPhonePort}|depthScale={F(sensorDepthScale)}|backDepthBoost={F(sensorBackcourtDepthBoost)}");
            }
        }

        private Vector3 GroundPositionFromSensor(BadmintonPlayerFrame player)
        {
            float virtualZ = player.VirtualPosition.z;
            float targetZ = playerGroundPosition.z;
            if (!hasLastSensorVirtualZ)
            {
                lastSensorVirtualZ = virtualZ;
                hasLastSensorVirtualZ = true;
            }
            else
            {
                float virtualDeltaZ = virtualZ - lastSensorVirtualZ;
                lastSensorVirtualZ = virtualZ;
                float depthScale = virtualDeltaZ < 0f
                    ? sensorDepthScale * Mathf.Max(1f, sensorBackcourtDepthBoost)
                    : sensorDepthScale;
                targetZ = Mathf.Clamp(
                    playerGroundPosition.z + virtualDeltaZ * depthScale,
                    -6.15f,
                    -1.15f);
            }

            return new Vector3(
                Mathf.Clamp(player.VirtualPosition.x * sensorLateralScale, -2.85f, 2.85f),
                0.55f,
                targetZ);
        }

        private void UpdateRacketPosition()
        {
            if (inputSnapshot.HasGroundPosition)
            {
                playerGroundPosition = inputSnapshot.GroundPosition;
            }
            else if (inputMode == BadmintonInputMode.Sensor && !inputSnapshot.PlayerStale)
            {
                playerGroundPosition = GroundPositionFromSensor(inputSnapshot.Player);
            }
            else if (inputMode == BadmintonInputMode.Sensor)
            {
                hasLastSensorVirtualZ = false;
            }

            if (awaitingPlayerServe && inputMode == BadmintonInputMode.Legacy)
            {
                float innerX = 0.18f;
                float outerX = 2.48f;
                playerGroundPosition.x = playerServeSide < 0f
                    ? Mathf.Clamp(playerGroundPosition.x, -outerX, -innerX)
                    : Mathf.Clamp(playerGroundPosition.x, innerX, outerX);
                playerGroundPosition.z = Mathf.Clamp(
                    playerGroundPosition.z,
                    -6.25f * CourtLengthScale,
                    -2.08f * CourtLengthScale);
            }
            else
            {
                playerGroundPosition.x = Mathf.Clamp(playerGroundPosition.x, -2.85f, 2.85f);
                playerGroundPosition.z = Mathf.Clamp(playerGroundPosition.z, -6.15f, -1.15f);
            }

            Vector3 targetPosition = racket.position;
            if (inputMode == BadmintonInputMode.Sensor)
            {
                if (!inputSnapshot.PlayerStale && inputSnapshot.Player.RightHand.Visible)
                {
                    float sensorHeight = Mathf.Clamp(
                        0.28f + inputSnapshot.Player.RightHand.Height * sensorHandHeightScale,
                        0.12f,
                        1.65f);
                    float sensorHandOffset = inputSnapshot.Player.RightHand.Relative.x * sensorHandLateralScale;
                    targetPosition = new Vector3(
                        playerGroundPosition.x + sensorHandOffset,
                        sensorHeight + jumpOffset,
                        playerGroundPosition.z);
                }
            }
            else
            {
                float targetHeight = 0.65f;
                if (shuttleIncoming && shuttle.gameObject.activeSelf && shuttle.position.z < -0.25f)
                {
                    targetHeight = Mathf.Clamp(shuttle.position.y - 0.75f, 0.12f, 1.65f);
                }

                float racketSide = isBackhand ? -0.95f : 0.95f;
                targetPosition = new Vector3(
                    playerGroundPosition.x + racketSide,
                    targetHeight + jumpOffset,
                    playerGroundPosition.z);
            }

            racket.position = Vector3.Lerp(
                racket.position,
                targetPosition,
                1f - Mathf.Exp(-racketFollowSpeed * Time.deltaTime));

            swingCooldown = Mathf.Max(0f, swingCooldown - Time.deltaTime);
            if (swingPending)
            {
                pendingSwingTime -= Time.deltaTime;
                if (pendingSwingTime <= 0f)
                {
                    LogSensorHitDebug(
                        "swing_expire",
                        $"pendingAge={F(pendingSwingStartedAt > 0f ? Time.time - pendingSwingStartedAt : 0f)}|{HitResultFields(lastHitResult)}");
                    swingPending = false;
                    pendingSwingPowerSpeed = 0f;
                    pendingSwingStartedAt = 0f;
                }
            }

            float facePitch = Mathf.Lerp(120f, -30f, currentMouseY);
            if (isBackhand)
            {
                facePitch = -facePitch;
            }

            Quaternion targetRotation = racket.rotation;
            bool resetSensorRacketPose = inputMode == BadmintonInputMode.Sensor &&
                inputSnapshot.OpponentServeReady;
            if (inputMode == BadmintonInputMode.Sensor)
            {
                if (resetSensorRacketPose)
                {
                    targetRotation = CalibrateSensorRacketReadyPose();
                }
                else if (!inputSnapshot.RacketStale)
                {
                    targetRotation = SensorRacketRotation(inputSnapshot.Racket.Orientation);
                }
            }
            else
            {
                targetRotation = Quaternion.Euler(
                    facePitch,
                    isBackhand ? 180f : 0f,
                    isBackhand ? 8f : -8f);
            }

            racket.rotation = resetSensorRacketPose
                ? targetRotation
                : Quaternion.Slerp(racket.rotation, targetRotation, 18f * Time.deltaTime);
            racketCenterGuide.position = new Vector3(
                racketFace.position.x,
                0.028f,
                racketFace.position.z);
            racketCenterGuide.rotation = Quaternion.identity;
            racketCenterGuide.gameObject.SetActive(showRacketCenterGuide);
            playerMarker.position = new Vector3(
                playerGroundPosition.x,
                0.55f + jumpOffset,
                playerGroundPosition.z);
            Quaternion bodyRotation = Quaternion.Euler(
                0f,
                isBackhand ? -142f : 18f,
                isBackhand ? 10f : -4f);
            playerMarker.rotation = Quaternion.Slerp(
                playerMarker.rotation,
                bodyRotation,
                10f * Time.deltaTime);
        }

        private Quaternion CalibrateSensorRacketReadyPose()
        {
            Quaternion readyRotation = SensorReadyRacketRotation();
            if (!inputSnapshot.RacketStale)
            {
                sensorRacketRotationOffset =
                    readyRotation * Quaternion.Inverse(inputSnapshot.Racket.Orientation);
                hasSensorRacketRotationOffset = true;
            }

            racketHistory.Clear();
            lastRecordedRacketTime = 0f;
            return readyRotation;
        }

        private Quaternion SensorRacketRotation(Quaternion sensorRotation)
        {
            return hasSensorRacketRotationOffset
                ? sensorRacketRotationOffset * sensorRotation
                : sensorRotation;
        }

        private static Quaternion SensorReadyRacketRotation()
        {
            return Quaternion.Euler(0f, 90f, 0f);
        }

        private void StartPlayerJump()
        {
            jumpActive = true;
            jumpElapsed = 0f;
        }

        private void UpdateJump()
        {
            if (!jumpActive)
            {
                jumpOffset = 0f;
                return;
            }

            jumpElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(jumpElapsed / jumpDuration);
            float riseEnd = (1f - jumpHangFraction) * 0.5f;
            float fallStart = 1f - riseEnd;
            if (t < riseEnd)
            {
                float riseT = Mathf.SmoothStep(0f, 1f, t / riseEnd);
                jumpOffset = riseT * jumpHeight;
            }
            else if (t <= fallStart)
            {
                jumpOffset = jumpHeight;
            }
            else
            {
                float fallT = Mathf.SmoothStep(0f, 1f, (t - fallStart) / riseEnd);
                jumpOffset = (1f - fallT) * jumpHeight;
            }
            if (t >= 1f)
            {
                jumpActive = false;
                jumpOffset = 0f;
            }
        }

        private void UpdatePlayerPositionMarker()
        {
            if (!playerPositionMarker.gameObject.activeSelf ||
                !landingMarker.gameObject.activeSelf)
            {
                return;
            }

            Vector3 landingPosition = landingMarker.position;
            bool frontCourtTarget = Mathf.Abs(landingPosition.z) < 4f * CourtLengthScale;
            Vector3 referencePosition = hasPlayerContactPrediction
                ? playerPredictedContactPoint
                : landingPosition;
            float playerZOffset = frontCourtTarget ? -1.15f : Mathf.Max(0f, backcourtPositionInset);
            float playerXOffset = isBackhand ? 0.95f : -0.95f;
            playerPositionMarker.position = new Vector3(
                referencePosition.x + playerXOffset,
                0.018f,
                referencePosition.z + playerZOffset);
        }

        private void ReadInputSwing()
        {
            if (swingCooldown > 0f)
            {
                return;
            }

            float speed = inputSnapshot.SwingGameSpeed;
            if (awaitingPlayerServe)
            {
                if (inputMode == BadmintonInputMode.Sensor && (inputSnapshot.PlayerStale || inputSnapshot.RacketStale))
                {
                    return;
                }

                if (inputSnapshot.HasSwingGesture &&
                    inputSnapshot.SwingUpward &&
                    speed >= minimumSwingSpeed * 0.45f)
                {
                    playerServeSpeed = speed;
                    playerServeGestureReady = true;
                    LogSensorHitDebug(
                        "serve_swing_accept",
                        $"acceptedSpeed={F(speed)}|acceptedUp={B(inputSnapshot.SwingUpward)}|startAngle={F(inputSnapshot.SwingStartAngle)}");
                }

                return;
            }

            if (!inputSnapshot.HasSwingGesture ||
                !shuttleIncoming ||
                speed < minimumSwingSpeed * 0.45f ||
                (inputMode == BadmintonInputMode.Sensor && (inputSnapshot.PlayerStale || inputSnapshot.RacketStale)))
            {
                return;
            }

            // Resolve after the normal gesture, stale-data, and speed gates so the correction cannot
            // create swings by itself.
            bool resolvedSwingUpward = CurrentInputSwingUpward();
            swingPending = true;
            swingUpward = resolvedSwingUpward;
            pendingSwingSpeed = speed;
            pendingSwingPowerSpeed = inputMode == BadmintonInputMode.Sensor
                ? Mathf.Max(inputSnapshot.SwingGameSpeed, inputSnapshot.SwingPeakGameSpeed)
                : pendingSwingSpeed;
            pendingStartAngle = inputSnapshot.SwingStartAngle;
            pendingSwingTime = contactWindow;
            pendingSwingStartedAt = Time.time;
            swingCooldown = 0.22f;
            LogSensorHitDebug(
                "swing_accept",
                $"acceptedSpeed={F(speed)}|acceptedPowerSpeed={F(pendingSwingPowerSpeed)}|acceptedUp={B(resolvedSwingUpward)}|startAngle={F(inputSnapshot.SwingStartAngle)}");
        }

        private void UpdatePendingSwingPower()
        {
            if (!swingPending ||
                inputMode != BadmintonInputMode.Sensor ||
                inputSnapshot.RacketStale)
            {
                return;
            }

            pendingSwingPowerSpeed = Mathf.Max(
                pendingSwingPowerSpeed,
                inputSnapshot.SwingPeakGameSpeed);
        }

        private void RecordRacketFrame()
        {
            if (racketFace == null)
            {
                return;
            }

            float now = Time.time;
            Vector3 center = racketFace.position;
            float elapsed = lastRecordedRacketTime > 0f
                ? Mathf.Max(0.001f, now - lastRecordedRacketTime)
                : 0f;
            Vector3 velocity = elapsed > 0f
                ? (center - lastRecordedRacketFacePosition) / elapsed
                : Vector3.zero;

            float activeSwingSpeed = Mathf.Max(inputSnapshot.SwingGameSpeed, pendingSwingSpeed);
            // Contact resolution can backtrack through these frames, so store the same corrected
            // swing class used at accept time.
            bool frameSwingUpward = CurrentInputSwingUpward() || swingUpward;
            if (swingPending ||
                inputSnapshot.HasSwingGesture ||
                inputSnapshot.Swing.State == BadmintonSwingState.Prepare ||
                inputSnapshot.Swing.State == BadmintonSwingState.Swing ||
                inputSnapshot.Swing.State == BadmintonSwingState.ImpactCandidate)
            {
                float boost = Mathf.Lerp(
                    1f,
                    13f,
                    Mathf.InverseLerp(minimumSwingSpeed * 0.45f, fastSwingSpeed, activeSwingSpeed));
                velocity += DefaultWorldSwingDirection(frameSwingUpward) * boost;
            }

            racketHistory.Add(new RacketKinematicFrame
            {
                Time = now,
                FaceCenter = center,
                FaceNormal = racketFace.forward,
                FaceRight = racketFace.right,
                FaceUp = racketFace.up,
                FaceVelocity = velocity,
                SwingDirection = DefaultWorldSwingDirection(frameSwingUpward),
                SwingSpeed = activeSwingSpeed,
                FaceAngle = currentFaceAngle,
                TrackingConfidence = CurrentTrackingConfidence(),
                SwingUpward = frameSwingUpward
            });
            PruneRacketHistory(now);
            lastRecordedRacketFacePosition = center;
            lastRecordedRacketTime = now;
        }

        private void RecordShuttleFrame(Vector3 position, Vector3 velocity)
        {
            float now = Time.time;
            shuttleHistory.Add(new ShuttleKinematicFrame
            {
                Time = now,
                Position = position,
                Velocity = velocity
            });
            PruneShuttleHistory(now);
        }

        private void ClearHitHistory()
        {
            racketHistory.Clear();
            shuttleHistory.Clear();
            lastRecordedRacketTime = 0f;
            pendingSwingPowerSpeed = 0f;
            pendingSwingStartedAt = 0f;
            lastHitResult = RacketHitResult.Miss("history cleared", false);
        }

        private void PruneRacketHistory(float now)
        {
            float oldest = now - Mathf.Max(0.05f, hitHistorySeconds);
            while (racketHistory.Count > 0 && racketHistory[0].Time < oldest)
            {
                racketHistory.RemoveAt(0);
            }
        }

        private void PruneShuttleHistory(float now)
        {
            float oldest = now - Mathf.Max(0.05f, hitHistorySeconds);
            while (shuttleHistory.Count > 0 && shuttleHistory[0].Time < oldest)
            {
                shuttleHistory.RemoveAt(0);
            }
        }

        private float CurrentTrackingConfidence()
        {
            if (inputMode != BadmintonInputMode.Sensor)
            {
                return 1f;
            }

            float confidence = 0f;
            if (!inputSnapshot.PlayerStale)
            {
                confidence += 0.42f;
            }

            if (inputSnapshot.Player.RightHand.Visible)
            {
                confidence += 0.24f * Mathf.Clamp01(inputSnapshot.Player.RightHand.Confidence);
            }

            if (!inputSnapshot.RacketStale)
            {
                confidence += 0.34f;
            }

            return Mathf.Clamp01(confidence);
        }

        private RacketHitSettings CurrentHitSettings()
        {
            RacketHitSettings settings = RacketHitSettings.Default();
            settings.SweetHalfWidth = racketSweetHalfWidth;
            settings.SweetHalfHeight = racketSweetHalfHeight;
            settings.AssistShell = Mathf.Max(racketAssistShell, racketXAlignmentTolerance * 0.25f);
            settings.PlaneTolerance = racketPlaneTolerance;
            settings.MagnetRadius = racketMagnetRadius;
            settings.MagnetPlaneTolerance = racketMagnetPlaneTolerance;
            settings.BacktrackSeconds = hitBacktrackSeconds;
            settings.ForwardSeconds = contactWindow;
            settings.MaxSampleGapSeconds = Mathf.Max(0.06f, Time.maximumDeltaTime * 0.5f);
            settings.MinimumQuality = minimumHitQuality;
            settings.MinimumDirectionQuality = 0.32f;
            settings.MinimumFaceQuality = 0.16f;
            if (swingUpward || isBackhand)
            {
                settings.SweetHalfWidth += 0.06f;
                settings.SweetHalfHeight += 0.08f;
                settings.AssistShell += 0.12f;
                settings.MagnetRadius += 0.08f;
                settings.PlaneTolerance += 0.08f;
                settings.MagnetPlaneTolerance += 0.04f;
                settings.BacktrackSeconds += 0.04f;
                settings.ForwardSeconds += 0.08f;
                settings.MinimumQuality = Mathf.Min(settings.MinimumQuality, 0.38f);
                settings.MinimumDirectionQuality = 0.22f;
                settings.MinimumFaceQuality = 0.08f;
            }

            return settings;
        }

        // Gameplay code should call this when it needs the sensor-corrected upward classification.
        private bool CurrentInputSwingUpward()
        {
            return inputSnapshot.SwingUpward || SensorSideMirroredLiftCorrectionActive();
        }

        // Intentionally narrow: only sensor front-court lifts get corrected; serve, backcourt,
        // and legacy paths keep the raw class.
        private bool SensorSideMirroredLiftCorrectionActive()
        {
            if (inputMode != BadmintonInputMode.Sensor ||
                !inputSnapshot.HasSwingGesture ||
                inputSnapshot.SwingUpward ||
                !incomingFrontCourt)
            {
                return false;
            }

            // The sensor swing direction is mirrored across court sides for this front-court
            // lift motion.
            return BadmintonInputMath.IsSideMirroredLiftGesture(
                inputSnapshot.Swing.Direction,
                SensorRacketLateralPosition(),
                currentFaceAngle);
        }

        // Prefer the actual racket transform because contact tests use world-space racket geometry.
        private float SensorRacketLateralPosition()
        {
            if (racketFace != null)
            {
                return racketFace.position.x;
            }

            if (inputSnapshot.Player.RightHand.Visible)
            {
                return inputSnapshot.Player.VirtualPosition.x + inputSnapshot.Player.RightHand.Relative.x * sensorHandLateralScale;
            }

            return playerGroundPosition.x;
        }

        private void StartSensorHitLogSession(string eventName)
        {
            if (inputMode != BadmintonInputMode.Sensor)
            {
                return;
            }

            sensorHitLogSequence = 0;
            sensorHitLogPath = string.Empty;
            sensorHitLogPathReady = false;
            sensorHitLogFileFailed = false;

            string fileDetail = string.Empty;
            if (writeSensorHitLogFile && TryEnsureSensorHitLogFile(out string path))
            {
                fileDetail = $"logFile={LogToken(path)}|";
            }

            LogSensorHitDebug(
                eventName,
                $"{fileDetail}difficulty={difficultyLevel}|scoreTarget={scoreTarget}|scoreCap={scoreCap}");
        }

        // Pipe-delimited logs are easy to grep and import as key/value columns after a rally.
        private void LogSensorHitDebug(string eventName, string details = "")
        {
            if (inputMode != BadmintonInputMode.Sensor ||
                (!logSensorHitDebug && !writeSensorHitLogFile))
            {
                return;
            }

            string suffix = string.IsNullOrEmpty(details) ? string.Empty : $"|{details}";
            string line =
                $"VRB_SENSOR_HIT|seq={++sensorHitLogSequence}|event={LogToken(eventName)}|" +
                $"{SensorHitSnapshotFields()}{suffix}";

            if (logSensorHitDebug)
            {
                Debug.Log(line);
            }

            if (writeSensorHitLogFile)
            {
                AppendSensorHitLogLine(line);
            }
        }

        private void AppendSensorHitLogLine(string line)
        {
            if (!TryEnsureSensorHitLogFile(out string path))
            {
                return;
            }

            try
            {
                File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception exception)
            {
                if (!sensorHitLogFileFailed)
                {
                    Debug.LogWarning($"Failed to write sensor hit log '{path}': {exception.Message}");
                }

                sensorHitLogFileFailed = true;
            }
        }

        private bool TryEnsureSensorHitLogFile(out string path)
        {
            path = sensorHitLogPath;
            if (sensorHitLogFileFailed)
            {
                return false;
            }

            if (sensorHitLogPathReady && !string.IsNullOrEmpty(sensorHitLogPath))
            {
                path = sensorHitLogPath;
                return true;
            }

            try
            {
                string rootPath = ProjectRootPath();
                string directory = string.IsNullOrWhiteSpace(sensorHitLogDirectory)
                    ? "Logs/SensorHit"
                    : sensorHitLogDirectory;
                string logDirectory = Path.IsPathRooted(directory)
                    ? directory
                    : Path.Combine(rootPath, directory);
                Directory.CreateDirectory(logDirectory);

                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                sensorHitLogPath = Path.Combine(logDirectory, $"sensor-hit-{stamp}.log");
                sensorHitLogPathReady = true;
                path = sensorHitLogPath;
                return true;
            }
            catch (Exception exception)
            {
                if (!sensorHitLogFileFailed)
                {
                    Debug.LogWarning($"Failed to create sensor hit log file: {exception.Message}");
                }

                sensorHitLogFileFailed = true;
                path = string.Empty;
                return false;
            }
        }

        private static string ProjectRootPath()
        {
            DirectoryInfo assetsParent = Directory.GetParent(Application.dataPath);
            return assetsParent != null
                ? assetsParent.FullName
                : Application.dataPath;
        }

        // Include both world-space and racket-local contact geometry so misses can be explained
        // from a single log line.
        private string SensorHitSnapshotFields()
        {
            Vector3 racketPosition = racketFace != null ? racketFace.position : Vector3.zero;
            Vector3 racketForward = racketFace != null ? racketFace.forward : Vector3.forward;
            Vector3 racketRight = racketFace != null ? racketFace.right : Vector3.right;
            Vector3 racketUp = racketFace != null ? racketFace.up : Vector3.up;
            Vector3 shuttlePosition = shuttle != null ? shuttle.position : Vector3.zero;
            bool landingVisible = landingMarker != null && landingMarker.gameObject.activeSelf;
            Vector3 landingPosition = landingVisible ? landingMarker.position : Vector3.zero;
            Vector3 shuttleVelocity = shuttleHistory.Count > 0
                ? shuttleHistory[shuttleHistory.Count - 1].Velocity
                : Vector3.zero;
            Vector3 toShuttle = shuttlePosition - racketPosition;
            float signedPlaneDistance = Vector3.Dot(toShuttle, racketForward);
            float localX = Vector3.Dot(toShuttle, racketRight);
            float localY = Vector3.Dot(toShuttle, racketUp);
            bool mirrorLiftFix = SensorSideMirroredLiftCorrectionActive();
            bool resolvedInputUp = inputSnapshot.SwingUpward || mirrorLiftFix;

            return
                $"time={F(Time.time)}" +
                $"|screen={screenState}" +
                $"|mode={gameMode}" +
                $"|playerScore={playerScore}" +
                $"|opponentScore={opponentScore}" +
                $"|playerServing={B(playerServing)}" +
                $"|awaitPlayerServe={B(awaitingPlayerServe)}" +
                $"|awaitOpponentServe={B(awaitingOpponentServe)}" +
                $"|opponentServeReady={B(opponentServeReady)}" +
                $"|rallyWinner={rallyWinner}" +
                $"|matchWinner={matchWinner}" +
                $"|currentHitter={currentFlightHitter}" +
                $"|playerStale={B(inputSnapshot.PlayerStale)}" +
                $"|racketStale={B(inputSnapshot.RacketStale)}" +
                $"|shuttleActive={B(shuttle != null && shuttle.gameObject.activeSelf)}" +
                $"|shuttleIncoming={B(shuttleIncoming)}" +
                $"|incomingHighClear={B(incomingHighClear)}" +
                $"|swingPending={B(swingPending)}" +
                $"|inputSwingUp={B(inputSnapshot.SwingUpward)}" +
                $"|resolvedInputUp={B(resolvedInputUp)}" +
                $"|mirrorLiftFix={B(mirrorLiftFix)}" +
                $"|pendingSwingUp={B(swingUpward)}" +
                $"|isBackhand={B(isBackhand)}" +
                $"|frontCourt={B(incomingFrontCourt)}" +
                $"|opponentSmash={B(incomingOpponentSmash)}" +
                $"|smashReady={B(smashReceiveReady)}" +
                $"|swingState={inputSnapshot.Swing.State}" +
                $"|swingType={inputSnapshot.Swing.Type}" +
                $"|impact={B(inputSnapshot.Swing.Impact)}" +
                $"|sinceImpactMs={F(inputSnapshot.Swing.SinceImpactMs)}" +
                $"|swingDir={V(inputSnapshot.Swing.Direction)}" +
                $"|angularVel={V(inputSnapshot.Racket.AngularVelocity)}" +
                $"|angularSpeed={F(inputSnapshot.Racket.AngularSpeed)}" +
                $"|gameSpeed={F(inputSnapshot.SwingGameSpeed)}" +
                $"|peakGameSpeed={F(inputSnapshot.SwingPeakGameSpeed)}" +
                $"|pendingSpeed={F(pendingSwingSpeed)}" +
                $"|pendingPowerSpeed={F(pendingSwingPowerSpeed)}" +
                $"|faceAngle={F(currentFaceAngle)}" +
                $"|rawEuler={V(inputSnapshot.Racket.RawEuler)}" +
                $"|playerPos={V(playerGroundPosition)}" +
                $"|sensorVirtualPos={V(inputSnapshot.Player.VirtualPosition)}" +
                $"|sensorRightHand={V(inputSnapshot.Player.RightHand.Relative)}" +
                $"|rightHandVisible={B(inputSnapshot.Player.RightHand.Visible)}" +
                $"|jumpActive={B(jumpActive)}" +
                $"|jumpOffset={F(jumpOffset)}" +
                $"|opponentStamina={F(opponentStamina)}" +
                $"|racketPos={V(racketPosition)}" +
                $"|racketForward={V(racketForward)}" +
                $"|racketRight={V(racketRight)}" +
                $"|racketUp={V(racketUp)}" +
                $"|shuttlePos={V(shuttlePosition)}" +
                $"|shuttleVel={V(shuttleVelocity)}" +
                $"|landingVisible={B(landingVisible)}" +
                $"|landingPos={V(landingPosition)}" +
                $"|predictedContact={B(hasPlayerContactPrediction)}" +
                $"|predictedPoint={V(playerPredictedContactPoint)}" +
                $"|toShuttle={V(toShuttle)}" +
                $"|distance={F(toShuttle.magnitude)}" +
                $"|plane={F(signedPlaneDistance)}" +
                $"|localX={F(localX)}" +
                $"|localY={F(localY)}";
        }

        // Keep result fields compact so they can be paired with the preceding input snapshot
        // in the log.
        private static string HitResultFields(RacketHitResult result)
        {
            return
                $"hit={B(result.Hit)}" +
                $"|consume={B(result.ConsumeSwing)}" +
                $"|shot={result.Shot}" +
                $"|reason={LogToken(result.Reason)}" +
                $"|contactTime={F(result.ContactTime)}" +
                $"|quality={F(result.Quality)}" +
                $"|spatialQ={F(result.SpatialQuality)}" +
                $"|timingQ={F(result.TimingQuality)}" +
                $"|directionQ={F(result.DirectionQuality)}" +
                $"|faceQ={F(result.FaceQuality)}" +
                $"|powerQ={F(result.PowerQuality)}" +
                $"|sweetSpot={F(result.SweetSpot01)}" +
                $"|assist={B(result.AssistUsed)}" +
                $"|magnet={B(result.MagnetUsed)}" +
                $"|resultSwingUp={B(result.SwingUpward)}" +
                $"|resultSpeed={F(result.SwingSpeed)}" +
                $"|resultFaceAngle={F(result.FaceAngle)}" +
                $"|contactPoint={V(result.ContactPoint)}" +
                $"|contactFaceNormal={V(result.ContactFaceNormal)}" +
                $"|contactFaceRight={V(result.ContactFaceRight)}" +
                $"|contactFaceUp={V(result.ContactFaceUp)}" +
                $"|contactFaceVelocity={V(result.ContactFaceVelocity)}" +
                $"|contactSwingDirection={V(result.ContactSwingDirection)}" +
                $"|contactLocalX={F(result.ContactLocalX)}" +
                $"|contactLocalY={F(result.ContactLocalY)}" +
                $"|contactPlane={F(result.ContactPlaneDistance)}" +
                $"|contactTracking={F(result.ContactTrackingConfidence)}";
        }

        // Field values stay single-token so simple grep/split tooling can parse them reliably.
        private static string LogToken(string value)
        {
            return string.IsNullOrEmpty(value)
                ? "none"
                : value
                    .Replace(' ', '_')
                    .Replace('\t', '_')
                    .Replace('\r', '_')
                    .Replace('\n', '_')
                    .Replace('|', '/');
        }

        private static string B(bool value)
        {
            return value ? "1" : "0";
        }

        private static string F(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string V(Vector3 value)
        {
            return $"{F(value.x)},{F(value.y)},{F(value.z)}";
        }

        private static Vector3 DefaultWorldSwingDirection(bool upward)
        {
            Vector3 vertical = upward ? Vector3.up : Vector3.down;
            return (Vector3.forward * 0.74f + vertical * 0.67f).normalized;
        }

    }
}
