using Unity.Profiling;
using UnityEngine;

namespace VRBadminton.Input
{
    public sealed class SensorBadmintonInputSource : IBadmintonInputSource
    {
        private const long PlayerStaleMs = 650;
        private const long RacketStaleMs = 420;
        private const long JumpCooldownMs = 700;
        private const float JumpRiseThreshold = 0.035f;
        private const float JumpVelocityThreshold = 0.28f;
        private const float JumpBaselineAlpha = 0.08f;
        private static readonly ProfilerMarker SensorTickMarker =
            new ProfilerMarker("VRBadminton.SensorInput.Tick");

        private readonly PhoneRacketHttpServer phoneServer = new PhoneRacketHttpServer();
        private readonly BadmintonSwingDetector swingDetector = new BadmintonSwingDetector();
        private readonly IBadmintonPoseInputProvider poseInput;
        private readonly IMediaPipeAssetProvider mediaPipeAssetProvider;
        private readonly int preferredPhonePort;
        private readonly float angularSpeedToGameSpeed;
        private readonly float upwardOutSpeed;
        private readonly Vector3 initialGroundPosition;

        private BadmintonInputSnapshot snapshot;
        private BadmintonPlayerFrame latestPlayer = BadmintonPlayerFrame.Default("camera");
        private BadmintonRacketFrame latestRacket = BadmintonRacketFrame.Default("phone");
        private BadmintonSwingSample latestSwing = BadmintonSwingSample.Default();
        private long lastPhoneSequence = -1;
        private long lastReadySequence;
        private long lastJumpAtMs = -JumpCooldownMs;
        private long lastJumpSampleMs;
        private float groundedCenterY;
        private float lastCenterY;
        private bool hasJumpBaseline;

        public SensorBadmintonInputSource(
            Vector3 initialGroundPosition,
            int preferredPhonePort,
            float angularSpeedToGameSpeed,
            float upwardOutSpeed)
            : this(
                initialGroundPosition,
                preferredPhonePort,
                angularSpeedToGameSpeed,
                upwardOutSpeed,
                MediaPipeAssetProviderFactory.CreateDefault())
        {
        }

        internal SensorBadmintonInputSource(
            Vector3 initialGroundPosition,
            int preferredPhonePort,
            float angularSpeedToGameSpeed,
            float upwardOutSpeed,
            IMediaPipeAssetProvider mediaPipeAssetProvider)
        {
            this.initialGroundPosition = initialGroundPosition;
            this.preferredPhonePort = preferredPhonePort;
            this.angularSpeedToGameSpeed = angularSpeedToGameSpeed;
            this.upwardOutSpeed = upwardOutSpeed;
            this.mediaPipeAssetProvider =
                mediaPipeAssetProvider ?? MediaPipeAssetProviderFactory.CreateDefault();
            poseInput = new MediaPipePoseInputProvider(this.mediaPipeAssetProvider);
            snapshot = BadmintonInputSnapshot.Default();
            snapshot.GroundPosition = initialGroundPosition;
            snapshot.Status = "Sensor input idle";
        }

        public string Name => "Sensor";

        public BadmintonInputSnapshot Snapshot => snapshot;

        public void Start()
        {
            phoneServer.Start(preferredPhonePort);
            lastReadySequence = phoneServer.ReadySequence;
            ResetJumpDetection();
            poseInput.Start();
            snapshot.PhoneUrl = phoneServer.Url;
        }

        public void Stop()
        {
            poseInput.Stop();
            phoneServer.Stop();
        }

        public void Dispose()
        {
            Stop();
            phoneServer.Dispose();
            poseInput.Dispose();
        }

        public void Tick(BadmintonInputContext context)
        {
            using (SensorTickMarker.Auto())
            {
                long now = BadmintonInputClock.NowMs();
                snapshot.ToggleBackhand = false;
                snapshot.HasSwingGesture = false;
                snapshot.SmashReceiveReady = false;
                snapshot.OpponentServeReady = false;
                snapshot.JumpReady = false;

                poseInput.Tick();
                if (poseInput.TryGetLatestFrame(out BadmintonPlayerFrame player))
                {
                    latestPlayer = player;
                }

                bool hasPhoneFrame = phoneServer.TryGetLatestFrame(out BadmintonRacketFrame racket);
                bool newPhoneFrame = hasPhoneFrame && phoneServer.Sequence != lastPhoneSequence;
                long readySequence = phoneServer.ReadySequence;
                if (readySequence != lastReadySequence)
                {
                    snapshot.OpponentServeReady = true;
                    lastReadySequence = readySequence;
                }

                if (hasPhoneFrame)
                {
                    latestRacket = racket;
                }

                if (newPhoneFrame)
                {
                    lastPhoneSequence = phoneServer.Sequence;
                    latestSwing = swingDetector.Update(latestRacket, now);
                }
                else if (now - latestRacket.Timestamp > RacketStaleMs)
                {
                    latestSwing = BadmintonSwingSample.Default();
                }
                else
                {
                    latestSwing.Impact = false;
                }

                bool playerStale = !latestPlayer.Visible || now - latestPlayer.Timestamp > PlayerStaleMs;
                bool racketStale = !hasPhoneFrame || now - latestRacket.Timestamp > RacketStaleMs;
                bool jumpReady = DetectSmallJump(latestPlayer, playerStale, now);
                float faceAngle = racketStale ? snapshot.FaceAngle : BadmintonInputMath.FaceAngleFromRacket(latestRacket);
                float face01 = Mathf.InverseLerp(-45f, 120f, faceAngle);
                float gameSpeed = latestSwing.AngularSpeed * angularSpeedToGameSpeed;
                float filteredPeakGameSpeed = latestSwing.PeakSpeed * angularSpeedToGameSpeed;
                float rawGameSpeed = latestRacket.AngularSpeed * angularSpeedToGameSpeed;
                float peakGameSpeed = racketStale
                    ? 0f
                    : Mathf.Max(
                        SanitizeGameSpeed(gameSpeed),
                        SanitizeGameSpeed(filteredPeakGameSpeed),
                        SanitizeGameSpeed(rawGameSpeed));
                bool swingReady = !racketStale && newPhoneFrame && latestSwing.Impact;

                snapshot.Player = latestPlayer;
                snapshot.Racket = latestRacket;
                snapshot.Swing = latestSwing;
                snapshot.PlayerStale = playerStale;
                snapshot.RacketStale = racketStale;
                snapshot.HasGroundPosition = false;
                snapshot.GroundPosition = initialGroundPosition;
                snapshot.FaceAngle = faceAngle;
                snapshot.Face01 = face01;
                snapshot.DisplayedPower = Mathf.Lerp(
                    snapshot.DisplayedPower,
                    Mathf.Clamp01(gameSpeed / Mathf.Max(1f, upwardOutSpeed)),
                    1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
                snapshot.HasSwingGesture = swingReady;
                // The Unity player faces into the court, matching a real player facing the screen.
                // The phone demo's forward/back swing axis is therefore mirrored for gameplay classification.
                snapshot.SwingUpward = latestSwing.Direction.y < 0f;
                snapshot.SwingGameSpeed = gameSpeed;
                snapshot.SwingPeakGameSpeed = peakGameSpeed;
                snapshot.SwingStartAngle = faceAngle;
                snapshot.SmashReceiveReady = false;
                snapshot.JumpReady = jumpReady;
                snapshot.Status = StatusLine(playerStale, racketStale);
                snapshot.CameraStatus = poseInput.Status;
                snapshot.PhoneStatus = phoneServer.Status;
                snapshot.CameraUrl = string.Empty;
                snapshot.PhoneUrl = phoneServer.Url;
                snapshot.CameraPreviewTexture = poseInput.PreviewTexture;
                snapshot.CameraPreviewLandmarks = poseInput.PreviewLandmarks;
                snapshot.CameraPreviewFlipHorizontally = poseInput.PreviewFlipHorizontally;
                snapshot.CameraPreviewPoseVisible = poseInput.PreviewPoseVisible;
                snapshot.CameraPreviewTimestamp = poseInput.PreviewTimestamp;
                snapshot.CameraRunning = poseInput.Running;
                snapshot.PhoneConnected = !racketStale;
                snapshot.Calibrated = latestPlayer.Calibrated;
            }
        }

        private void ResetJumpDetection()
        {
            lastJumpAtMs = -JumpCooldownMs;
            lastJumpSampleMs = 0;
            groundedCenterY = 0f;
            lastCenterY = 0f;
            hasJumpBaseline = false;
        }

        private bool DetectSmallJump(
            BadmintonPlayerFrame player,
            bool playerStale,
            long now)
        {
            if (playerStale ||
                !player.Visible ||
                player.Confidence < 0.55f ||
                player.TrackingBasis != "torso")
            {
                hasJumpBaseline = false;
                return false;
            }

            float centerY = player.Center.y;
            if (!hasJumpBaseline)
            {
                groundedCenterY = centerY;
                lastCenterY = centerY;
                lastJumpSampleMs = now;
                hasJumpBaseline = true;
                return false;
            }

            float elapsed = Mathf.Max(0.001f, (now - lastJumpSampleMs) / 1000f);
            float velocityY = (centerY - lastCenterY) / elapsed;
            bool inCooldown = now - lastJumpAtMs < JumpCooldownMs;
            bool jumpReady =
                !inCooldown &&
                centerY - groundedCenterY >= JumpRiseThreshold &&
                velocityY >= JumpVelocityThreshold;

            lastCenterY = centerY;
            lastJumpSampleMs = now;

            if (jumpReady)
            {
                lastJumpAtMs = now;
                return true;
            }

            if (!inCooldown && velocityY < JumpVelocityThreshold * 0.35f)
            {
                groundedCenterY = Mathf.Lerp(groundedCenterY, centerY, JumpBaselineAlpha);
            }

            return false;
        }

        private string StatusLine(bool playerStale, bool racketStale)
        {
            if (playerStale && racketStale)
            {
                return "Sensor input waiting for camera and phone";
            }

            if (playerStale)
            {
                return "Sensor input waiting for camera";
            }

            if (racketStale)
            {
                return "Sensor input waiting for phone";
            }

            return "Sensor input active";
        }

        private static float SanitizeGameSpeed(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Max(0f, value);
        }
    }

}
