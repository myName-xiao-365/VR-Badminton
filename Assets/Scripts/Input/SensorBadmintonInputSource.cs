using Unity.Profiling;
using UnityEngine;

namespace VRBadminton.Input
{
    public sealed class SensorBadmintonInputSource : IBadmintonInputSource
    {
        private const long PlayerStaleMs = 650;
        private const long RacketStaleMs = 420;
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

                poseInput.Tick();
                if (poseInput.TryGetLatestFrame(out BadmintonPlayerFrame player))
                {
                    latestPlayer = player;
                }

                bool hasPhoneFrame = phoneServer.TryGetLatestFrame(out BadmintonRacketFrame racket);
                bool newPhoneFrame = hasPhoneFrame && phoneServer.Sequence != lastPhoneSequence;
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
                float faceAngle = racketStale ? snapshot.FaceAngle : BadmintonInputMath.FaceAngleFromRacket(latestRacket);
                float face01 = Mathf.InverseLerp(-45f, 120f, faceAngle);
                float gameSpeed = latestSwing.AngularSpeed * angularSpeedToGameSpeed;
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
                snapshot.SwingStartAngle = faceAngle;
                snapshot.SmashReceiveReady =
                    (!playerStale && latestPlayer.RightHand.Visible && latestPlayer.RightHand.Height >= 0.68f) ||
                    latestSwing.State == BadmintonSwingState.Prepare ||
                    latestSwing.State == BadmintonSwingState.Swing ||
                    latestSwing.State == BadmintonSwingState.ImpactCandidate;
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
    }

}
