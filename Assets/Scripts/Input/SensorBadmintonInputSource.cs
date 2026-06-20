using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

#if VRBADMINTON_MEDIAPIPE
using Mediapipe;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.Experimental;
#endif

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

    internal interface IBadmintonPoseInputProvider : System.IDisposable
    {
        string Status { get; }
        bool Running { get; }
        Texture PreviewTexture { get; }
        BadmintonPoseLandmark[] PreviewLandmarks { get; }
        bool PreviewFlipHorizontally { get; }
        bool PreviewPoseVisible { get; }
        long PreviewTimestamp { get; }
        void Start();
        void Stop();
        void Tick();
        bool TryGetLatestFrame(out BadmintonPlayerFrame frame);
    }

#if VRBADMINTON_MEDIAPIPE
    internal sealed class MediaPipePoseInputProvider : IBadmintonPoseInputProvider
    {
        private const string ModelAssetPath = "pose_landmarker_lite.bytes";
        private const string ClientId = "unity-camera";
        private const int RequestedWidth = 1280;
        private const int RequestedHeight = 720;
        private const int RequestedFps = 30;
        private const long InferenceIntervalMs = 40;
        private static readonly ProfilerMarker PoseInferenceMarker =
            new ProfilerMarker("VRBadminton.MediaPipePose.Inference");

        private readonly BadmintonPoseLandmarkMapper mapper = new BadmintonPoseLandmarkMapper();
        private readonly IMediaPipeAssetProvider mediaPipeAssetProvider;
        private readonly List<BadmintonPoseLandmark> mappedLandmarks = new List<BadmintonPoseLandmark>(33);
        private BadmintonPoseLandmark[] previewLandmarks = Array.Empty<BadmintonPoseLandmark>();
        private BadmintonPlayerFrame latestFrame = BadmintonPlayerFrame.Default(ClientId);
        private WebCamTexture webCamTexture;
        private TextureFrame textureFrame;
        private PoseLandmarker poseLandmarker;
        private PoseLandmarkerResult poseResult = PoseLandmarkerResult.Alloc(1);
        private string deviceName = "camera";
        private long lastInferenceAtMs;
        private int inferenceCount;
        private float fpsWindowStartedAt;
        private float inferenceFps;
        private bool runtimeInitialized;
        private bool inferenceFlipVertically;

        public string Status { get; private set; } = "MediaPipe camera idle";

        public bool Running { get; private set; }

        public Texture PreviewTexture => webCamTexture;

        public BadmintonPoseLandmark[] PreviewLandmarks => previewLandmarks;

        public bool PreviewFlipHorizontally => true;

        public bool PreviewPoseVisible { get; private set; }

        public long PreviewTimestamp { get; private set; }

        public MediaPipePoseInputProvider()
            : this(MediaPipeAssetProviderFactory.CreateDefault())
        {
        }

        public MediaPipePoseInputProvider(IMediaPipeAssetProvider mediaPipeAssetProvider)
        {
            this.mediaPipeAssetProvider =
                mediaPipeAssetProvider ?? MediaPipeAssetProviderFactory.CreateDefault();
        }

        public void Start()
        {
            Stop();
            mapper.Reset();
            latestFrame = BadmintonPlayerFrame.Default(ClientId);
            previewLandmarks = Array.Empty<BadmintonPoseLandmark>();
            PreviewPoseVisible = false;
            PreviewTimestamp = 0;
            lastInferenceAtMs = 0;
            inferenceCount = 0;
            inferenceFps = 0f;
            inferenceFlipVertically = false;
            fpsWindowStartedAt = Time.realtimeSinceStartup;

            try
            {
                InitializeMediaPipeRuntime();
                PrepareModelAsset();
                StartWebCam();
                CreatePoseLandmarker();
                Running = true;
                Status = $"{deviceName}: waiting for camera frames";
            }
            catch (Exception exception)
            {
                Running = false;
                Status = $"MediaPipe camera error: {exception.Message}";
                StopWebCam();
                DisposePoseLandmarker();
                ShutdownMediaPipeRuntime();
            }
        }

        public void Stop()
        {
            Running = false;
            StopWebCam();
            DisposePoseLandmarker();
            textureFrame?.Dispose();
            textureFrame = null;
            ShutdownMediaPipeRuntime();
        }

        public void Dispose()
        {
            Stop();
        }

        public void Tick()
        {
            if (!Running || webCamTexture == null || poseLandmarker == null)
            {
                return;
            }

            long now = BadmintonInputClock.NowMs();
            if (!webCamTexture.isPlaying)
            {
                Status = $"{deviceName}: camera not playing";
                return;
            }

            if (webCamTexture.width <= 16 || webCamTexture.height <= 16)
            {
                Status = $"{deviceName}: warming up";
                return;
            }

            if (!webCamTexture.didUpdateThisFrame || now - lastInferenceAtMs < InferenceIntervalMs)
            {
                return;
            }

            lastInferenceAtMs = now;
            EnsureTextureFrame(webCamTexture.width, webCamTexture.height);
            UpdateCameraTransform();

            using (PoseInferenceMarker.Auto())
            {
                try
                {
                    textureFrame.ReadTextureOnCPU(
                        webCamTexture,
                        flipHorizontally: false,
                        inferenceFlipVertically);
                    Image image = textureFrame.BuildCPUImage();
                    textureFrame.Release();
                    try
                    {
                        if (poseLandmarker.TryDetectForVideo(image, now, null, ref poseResult))
                        {
                            UpdateLatestFrameFromResult(poseResult, now);
                        }
                        else
                        {
                            MarkPoseLost(now, "no pose");
                        }
                    }
                    finally
                    {
                        image.Dispose();
                    }
                }
                catch (Exception exception)
                {
                    Status = $"{deviceName}: inference error: {exception.Message}";
                    MarkPoseLost(now, "inference_error");
                }
            }
        }

        public bool TryGetLatestFrame(out BadmintonPlayerFrame frame)
        {
            frame = latestFrame;
            return Running;
        }

        public BadmintonPlayerFrame MapLandmarksForPluginAdapter(
            IReadOnlyList<BadmintonPoseLandmark> landmarks,
            long timestampMs)
        {
            latestFrame = mapper.BuildFrame(landmarks, timestampMs, ClientId);
            SetPreviewLandmarks(landmarks, latestFrame.Visible, timestampMs);
            Status = latestFrame.Visible ? StatusForVisibleFrame() : $"{deviceName}: pose lost";
            Running = true;
            return latestFrame;
        }

        private void PrepareModelAsset()
        {
            IEnumerator prepare = mediaPipeAssetProvider.PrepareAssetAsync(ModelAssetPath);
            while (prepare.MoveNext())
            {
            }
        }

        private void InitializeMediaPipeRuntime()
        {
            if (runtimeInitialized)
            {
                return;
            }

            try
            {
                Mediapipe.Logger.MinLogLevel = Mediapipe.Logger.LogLevel.Info;
                Mediapipe.Protobuf.SetLogHandler(Mediapipe.Protobuf.DefaultLogHandler);
                Mediapipe.Glog.Initialize("VRBadminton.MediaPipe");
                runtimeInitialized = true;
            }
            catch (Exception)
            {
                try
                {
                    Mediapipe.Protobuf.ResetLogHandler();
                }
                catch (Exception)
                {
                }

                throw;
            }
        }

        private void ShutdownMediaPipeRuntime()
        {
            if (!runtimeInitialized)
            {
                return;
            }

            try
            {
                Mediapipe.Glog.Shutdown();
            }
            catch (Exception)
            {
            }

            try
            {
                Mediapipe.Protobuf.ResetLogHandler();
            }
            catch (Exception)
            {
            }

            runtimeInitialized = false;
        }

        private void StartWebCam()
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                throw new InvalidOperationException("No webcam device found");
            }

            deviceName = devices[0].name;
            webCamTexture = new WebCamTexture(deviceName, RequestedWidth, RequestedHeight, RequestedFps);
            webCamTexture.Play();
        }

        private void StopWebCam()
        {
            if (webCamTexture == null)
            {
                return;
            }

            if (webCamTexture.isPlaying)
            {
                webCamTexture.Stop();
            }

            UnityEngine.Object.Destroy(webCamTexture);
            webCamTexture = null;
        }

        private void CreatePoseLandmarker()
        {
            BaseOptions baseOptions = new BaseOptions(BaseOptions.Delegate.CPU, modelAssetPath: ModelAssetPath);
            PoseLandmarkerOptions options = new PoseLandmarkerOptions(
                baseOptions,
                runningMode: RunningMode.VIDEO,
                numPoses: 1,
                minPoseDetectionConfidence: 0.5f,
                minPosePresenceConfidence: 0.5f,
                minTrackingConfidence: 0.5f,
                outputSegmentationMasks: false);
            poseLandmarker = PoseLandmarker.CreateFromOptions(options);
        }

        private void DisposePoseLandmarker()
        {
            if (poseLandmarker == null)
            {
                return;
            }

            try
            {
                ((IDisposable)poseLandmarker).Dispose();
            }
            catch (Exception)
            {
            }

            poseLandmarker = null;
        }

        private void EnsureTextureFrame(int width, int height)
        {
            if (textureFrame != null && textureFrame.width == width && textureFrame.height == height)
            {
                return;
            }

            textureFrame?.Dispose();
            textureFrame = new TextureFrame(width, height, TextureFormat.RGBA32);
        }

        private void UpdateCameraTransform()
        {
            // Keep MediaPipe unmirrored so handedness labels remain real; mirror only the preview UI.
            ImageTransformationOptions options = ImageTransformationOptions.Build(
                shouldFlipHorizontally: false,
                isVerticallyFlipped: webCamTexture != null && webCamTexture.videoVerticallyMirrored,
                rotation: RotationAngle.Rotation0);
            inferenceFlipVertically = options.flipVertically;
        }

        private void UpdateLatestFrameFromResult(PoseLandmarkerResult result, long timestampMs)
        {
            if (result.poseLandmarks == null ||
                result.poseLandmarks.Count == 0 ||
                result.poseLandmarks[0].landmarks == null ||
                result.poseLandmarks[0].landmarks.Count == 0)
            {
                MarkPoseLost(timestampMs, "no pose");
                return;
            }

            var landmarks = result.poseLandmarks[0].landmarks;
            mappedLandmarks.Clear();
            for (int i = 0; i < landmarks.Count; i++)
            {
                var landmark = landmarks[i];
                float visibility = landmark.visibility ?? landmark.presence ?? 1f;
                mappedLandmarks.Add(new BadmintonPoseLandmark(
                    landmark.x,
                    // Mapper/gameplay use y-up normalized landmarks; MediaPipe image coordinates are y-down.
                    1f - landmark.y,
                    Mathf.Clamp01(visibility)));
            }

            latestFrame = mapper.BuildFrame(mappedLandmarks, timestampMs, ClientId);
            SetPreviewLandmarks(mappedLandmarks, latestFrame.Visible, timestampMs);
            UpdateFps();
            Status = latestFrame.Visible ? StatusForVisibleFrame() : $"{deviceName}: pose lost ({inferenceFps:0} fps)";
        }

        private void MarkPoseLost(long timestampMs, string reason)
        {
            latestFrame = mapper.BuildFrame(Array.Empty<BadmintonPoseLandmark>(), timestampMs, ClientId);
            SetPreviewLandmarks(Array.Empty<BadmintonPoseLandmark>(), false, timestampMs);
            UpdateFps();
            Status = $"{deviceName}: {reason} ({inferenceFps:0} fps)";
        }

        private void SetPreviewLandmarks(
            IReadOnlyList<BadmintonPoseLandmark> landmarks,
            bool poseVisible,
            long timestampMs)
        {
            if (landmarks == null || landmarks.Count == 0)
            {
                previewLandmarks = Array.Empty<BadmintonPoseLandmark>();
            }
            else
            {
                if (previewLandmarks.Length != landmarks.Count)
                {
                    previewLandmarks = new BadmintonPoseLandmark[landmarks.Count];
                }

                for (int i = 0; i < landmarks.Count; i++)
                {
                    previewLandmarks[i] = landmarks[i];
                }
            }

            PreviewPoseVisible = poseVisible;
            PreviewTimestamp = timestampMs;
        }

        private void UpdateFps()
        {
            inferenceCount++;
            float now = Time.realtimeSinceStartup;
            float elapsed = now - fpsWindowStartedAt;
            if (elapsed < 1f)
            {
                return;
            }

            inferenceFps = inferenceCount / Mathf.Max(0.001f, elapsed);
            inferenceCount = 0;
            fpsWindowStartedAt = now;
        }

        private string StatusForVisibleFrame()
        {
            string calibrated = latestFrame.Calibrated ? "calibrated" : "calibrating";
            string hand = latestFrame.RightHand.Visible
                ? $"hand h={latestFrame.RightHand.Height:0.00}"
                : "no hand";
            return $"{deviceName}: pose {calibrated}, {hand}, {inferenceFps:0} fps";
        }
    }
#else
    internal sealed class MediaPipePoseInputProvider : IBadmintonPoseInputProvider
    {
        private readonly BadmintonPoseLandmarkMapper mapper = new BadmintonPoseLandmarkMapper();
        private BadmintonPlayerFrame latestFrame = BadmintonPlayerFrame.Default("camera");
        private BadmintonPoseLandmark[] previewLandmarks = Array.Empty<BadmintonPoseLandmark>();

        public MediaPipePoseInputProvider()
            : this(MediaPipeAssetProviderFactory.CreateDefault())
        {
        }

        public MediaPipePoseInputProvider(IMediaPipeAssetProvider mediaPipeAssetProvider)
        {
        }

        public string Status { get; private set; } =
            "MediaPipe plugin not enabled. Import v0.16.3 and define VRBADMINTON_MEDIAPIPE.";

        public bool Running { get; private set; }

        public Texture PreviewTexture => null;

        public BadmintonPoseLandmark[] PreviewLandmarks => previewLandmarks;

        public bool PreviewFlipHorizontally => false;

        public bool PreviewPoseVisible { get; private set; }

        public long PreviewTimestamp { get; private set; }

        public void Start()
        {
            Running = false;
            Status = "MediaPipe plugin not enabled. Import v0.16.3 and define VRBADMINTON_MEDIAPIPE.";
            latestFrame = BadmintonPlayerFrame.Default("camera");
            previewLandmarks = Array.Empty<BadmintonPoseLandmark>();
            PreviewPoseVisible = false;
            PreviewTimestamp = 0;
        }

        public void Stop()
        {
            Running = false;
        }

        public void Dispose()
        {
            Stop();
        }

        public void Tick()
        {
        }

        public bool TryGetLatestFrame(out BadmintonPlayerFrame frame)
        {
            frame = latestFrame;
            return false;
        }

        public BadmintonPlayerFrame MapLandmarksForPluginAdapter(
            System.Collections.Generic.IReadOnlyList<BadmintonPoseLandmark> landmarks,
            long timestampMs)
        {
            latestFrame = mapper.BuildFrame(landmarks, timestampMs, "camera");
            previewLandmarks = landmarks == null || landmarks.Count == 0
                ? Array.Empty<BadmintonPoseLandmark>()
                : new List<BadmintonPoseLandmark>(landmarks).ToArray();
            PreviewPoseVisible = latestFrame.Visible;
            PreviewTimestamp = timestampMs;
            Status = latestFrame.Visible ? "MediaPipe pose frame mapped" : "MediaPipe pose lost";
            Running = true;
            return latestFrame;
        }
    }
#endif
}
