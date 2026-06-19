using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRBadminton.Input;

namespace VRBadminton.Gameplay
{
    public sealed class ShuttleFeedController : MonoBehaviour
    {
        private enum ShotType
        {
            Net,
            Drop,
            Clear,
            Smash,
            Drive,
            Miss,
            Out
        }

        private enum OpponentShotType
        {
            Net,
            Drop,
            Lift,
            Clear,
            Smash
        }

        private enum GameMode
        {
            SinglePlayer,
            Multiplayer
        }

        private const float CourtLengthScale = 0.95f;
        private static readonly int[] PosePreviewBonePairs =
        {
            11, 12,
            11, 13, 13, 15, 15, 17, 15, 19, 15, 21, 17, 19,
            12, 14, 14, 16, 16, 18, 16, 20, 16, 22, 18, 20,
            11, 23, 12, 24, 23, 24,
            23, 25, 25, 27, 27, 29, 27, 31, 29, 31,
            24, 26, 26, 28, 28, 30, 28, 32, 30, 32
        };

        [Header("Feed Timing")]
        [SerializeField] private float firstFeedDelay = 1f;
        [SerializeField] private float delayBetweenFeeds = 1.4f;

        [Header("Incoming Flight")]
        [SerializeField] private float dropShotDuration = 1.25f;
        [SerializeField] private float clearDuration = 2.15f;
        [SerializeField] private float dropShotArcHeight = 1.2f;
        [SerializeField] private float clearArcHeight = 4.2f;
        [SerializeField, Range(0.4f, 1f)] private float speedAfterNet = 0.68f;
        [SerializeField, Range(0.4f, 1f)] private float opponentSmashSpeedBeforeNet = 0.7f;
        [SerializeField, Range(0.1f, 0.8f)] private float opponentSmashSpeedAfterNet = 0.25f;

        [Header("Hit Assist")]
        [SerializeField] private float backcourtPositionInset = 0.45f;
        [SerializeField] private float racketXAlignmentTolerance = 0.6f;
        [SerializeField] private float contactWindow = 0.58f;
        [SerializeField] private float racketFollowSpeed = 14f;
        [SerializeField] private float racketMoveSpeed = 4.8f;

        [Header("Hit Resolver")]
        [SerializeField] private float racketSweetHalfWidth = 0.36f;
        [SerializeField] private float racketSweetHalfHeight = 0.46f;
        [SerializeField] private float racketAssistShell = 0.30f;
        [SerializeField] private float racketMagnetRadius = 0.22f;
        [SerializeField] private float racketPlaneTolerance = 0.34f;
        [SerializeField] private float racketMagnetPlaneTolerance = 0.14f;
        [SerializeField] private float hitBacktrackSeconds = 0.18f;
        [SerializeField] private float hitHistorySeconds = 0.32f;
        [SerializeField, Range(0.1f, 0.9f)] private float minimumHitQuality = 0.42f;
        [SerializeField] private bool showHitDebug;

        [Header("Camera View")]
        [SerializeField] private bool useSwitchStyleCamera = true;
        [SerializeField] private Camera gameplayCameraOverride;
        [SerializeField] private bool forceSwitchCameraPreset = true;
        [SerializeField] private Vector3 switchCameraPosition = new Vector3(0f, 6.9f, -12.1f);
        [SerializeField] private Vector3 switchCameraLookAt = new Vector3(0f, 1.05f, -2.35f);
        [SerializeField] private float switchCameraFieldOfView = 40f;
        [SerializeField, Range(0f, 1f)] private float switchCameraLateralFollow = 0.78f;
        [SerializeField] private float switchCameraMaxLateralOffset = 2.4f;
        [SerializeField, Range(0f, 1f)] private float switchCameraDepthFollow = 0.28f;
        [SerializeField] private float switchCameraMaxDepthOffset = 0.9f;
        [SerializeField] private float switchCameraFollowSpeed = 12f;
        [SerializeField, HideInInspector] private int switchCameraPresetVersion;

        [Header("Input")]
        [SerializeField] private BadmintonInputMode inputMode = BadmintonInputMode.Sensor;
        [SerializeField] private int sensorPhonePort = 8092;
        [SerializeField] private float sensorAngularSpeedToGameSpeed = 4f;
        [SerializeField] private float sensorLateralScale = 1.35f;
        [SerializeField] private float sensorDepthScale = 1.45f;
        [SerializeField] private float sensorBackcourtDepthBoost = 1.32f;
        [SerializeField] private float sensorHandHeightScale = 1.1f;
        [SerializeField] private float sensorHandLateralScale = 0.25f;

        [Header("Opponent")]
        [SerializeField] private float opponentMoveSpeed = 4.2f;
        [SerializeField] private float opponentReachTolerance = 0.22f;
        [SerializeField] private float opponentRacketGroundHeight = 0.72f;
        [SerializeField] private float opponentMaxStamina = 100f;
        [SerializeField] private float opponentRunStaminaPerMeter = 0.35f;
        [SerializeField, Range(0f, 1f)] private float opponentSmashReceiveChance = 0.55f;

        [Header("Mouse Stroke")]
        [SerializeField] private float minimumSwingSpeed = 220f;
        [SerializeField] private float mediumSwingSpeed = 1800f;
        [SerializeField] private float fastSwingSpeed = 3600f;
        [SerializeField] private float upwardOutSpeed = 5600f;
        [SerializeField] private float minimumAngleTravel = 18f;

        private Transform shuttle;
        private Transform landingMarker;
        private Transform playerPositionMarker;
        private Transform racket;
        private Transform racketFace;
        private Transform playerMarker;
        private Transform opponentRacket;
        private TrailRenderer shuttleTrail;

        private Material shuttleWhite;
        private Material shuttleCork;
        private Material markerYellow;
        private Material playerPositionMaterial;
        private Material trailMaterial;
        private Material racketRed;
        private Material racketBlue;
        private Material racketDark;
        private Material racketString;

        private Vector3 lastMousePosition;
        private bool shuttleIncoming;
        private bool incomingFrontCourt;
        private bool incomingOpponentSmash;
        private bool smashReceiveReady;
        private bool swingPending;
        private bool swingUpward;
        private float pendingSwingSpeed;
        private float pendingStartAngle;
        private float pendingSwingTime;
        private float smoothedMouseSpeed;
        private float swingCooldown;
        private Quaternion racketRestRotation;
        private Vector3 playerGroundPosition;
        private bool gestureTracking;
        private float gestureStartAngle;
        private float gestureDirection;
        private bool isBackhand;
        private float currentMouseY;
        private float currentFaceAngle;
        private float displayedPower;
        private float opponentStamina;
        private GUIStyle uiLabelStyle;
        private GameMode gameMode = GameMode.SinglePlayer;
        private int difficultyLevel = 3;
        private float opponentSmashChance = 0.75f;
        private int playerScore;
        private int opponentScore;
        private int rallyWinner;
        private bool playerServing;
        private bool awaitingPlayerServe;
        private bool playerServeGestureReady;
        private float playerServeSpeed;
        private IBadmintonInputSource legacyInputSource;
        private IBadmintonInputSource sensorInputSource;
        private IBadmintonInputSource activeInputSource;
        private BadmintonInputMode activeInputMode;
        private bool inputSourceStarted;
        private BadmintonInputSnapshot inputSnapshot;
        private readonly RacketHitResolver hitResolver = new RacketHitResolver();
        private readonly List<RacketKinematicFrame> racketHistory = new List<RacketKinematicFrame>(40);
        private readonly List<ShuttleKinematicFrame> shuttleHistory = new List<ShuttleKinematicFrame>(40);
        private Vector3 lastRecordedRacketFacePosition;
        private float lastRecordedRacketTime;
        private float pendingSwingStartedAt;
        private RacketHitResult lastHitResult;
        private Camera gameplayCamera;
        private const int SwitchCameraPresetVersion = 10;

        private void Awake()
        {
            ApplySwitchCameraPreset();
            CreateMaterials();
            shuttle = CreateShuttlecock().transform;
            landingMarker = CreateLandingMarker().transform;
            playerPositionMarker = CreatePlayerPositionMarker().transform;
            racket = CreatePixelRacket().transform;
            playerMarker = CreatePlayerMarker().transform;
            opponentRacket = CreateOpponentRacket().transform;
            racketRestRotation = Quaternion.Euler(12f, 0f, -8f);
            racket.rotation = racketRestRotation;
            playerGroundPosition = new Vector3(-0.15f, 0.55f, -2.7f * CourtLengthScale);

            shuttle.gameObject.SetActive(false);
            landingMarker.gameObject.SetActive(false);
            playerPositionMarker.gameObject.SetActive(false);
            lastMousePosition = Vector3.zero;
            currentMouseY = 0.5f;
            currentFaceAngle = Mathf.Lerp(-45f, 120f, currentMouseY);
            opponentStamina = opponentMaxStamina;
            inputSnapshot = BadmintonInputSnapshot.Default();
            lastHitResult = RacketHitResult.Miss("no hit yet", false);
            CreateInputSources();
            ActivateInputMode(inputMode);
        }

        private void Start()
        {
            UpdateSwitchStyleCamera(true);
            ApplyDifficulty(3);
        }

        private void OnDestroy()
        {
            legacyInputSource?.Dispose();
            sensorInputSource?.Dispose();
        }

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
            inputSourceStarted = true;
        }

        private Vector3 GroundPositionFromSensor(BadmintonPlayerFrame player)
        {
            float virtualZ = player.VirtualPosition.z;
            float depthScale = virtualZ < 0f
                ? sensorDepthScale * Mathf.Max(1f, sensorBackcourtDepthBoost)
                : sensorDepthScale;
            return new Vector3(
                Mathf.Clamp(player.VirtualPosition.x * sensorLateralScale, -2.85f, 2.85f),
                0.55f,
                Mathf.Clamp(
                    -2.7f * CourtLengthScale + virtualZ * depthScale,
                    -6.15f,
                    -1.15f));
        }

        private IEnumerator GameLoop()
        {
            yield return new WaitForSeconds(firstFeedDelay);
            while (enabled)
            {
                if (gameMode != GameMode.SinglePlayer)
                {
                    yield return null;
                    continue;
                }

                rallyWinner = 0;
                opponentStamina = opponentMaxStamina;

                if (playerServing)
                {
                    yield return PlayerServe();
                }
                else
                {
                    yield return FeedOneShuttle();
                }

                if (rallyWinner == 1)
                {
                    playerScore++;
                    playerServing = true;
                }
                else if (rallyWinner == 2)
                {
                    opponentScore++;
                    playerServing = false;
                }

                yield return new WaitForSeconds(delayBetweenFeeds);
            }
        }

        private void Update()
        {
            if (!inputSourceStarted || activeInputMode != inputMode)
            {
                ActivateInputMode(inputMode);
            }

            activeInputSource?.Tick(new BadmintonInputContext
            {
                ShuttleIncoming = shuttleIncoming,
                AwaitingPlayerServe = awaitingPlayerServe,
                IncomingOpponentSmash = incomingOpponentSmash,
                ContactWindow = contactWindow
            });
            inputSnapshot = activeInputSource?.Snapshot ?? BadmintonInputSnapshot.Default();
            currentMouseY = Mathf.Clamp01(inputSnapshot.Face01);
            currentFaceAngle = inputSnapshot.FaceAngle;
            displayedPower = inputSnapshot.DisplayedPower;

            if (inputSnapshot.ToggleBackhand)
            {
                isBackhand = !isBackhand;
            }

            if (incomingOpponentSmash && inputSnapshot.SmashReceiveReady)
            {
                smashReceiveReady = true;
            }

            UpdateRacketPosition();
            RecordRacketFrame();
            UpdatePlayerPositionMarker();
            ReadInputSwing();
        }

        private void LateUpdate()
        {
            UpdateSwitchStyleCamera(false);
        }

        private void OnValidate()
        {
            ApplySwitchCameraPreset();
            if (!Application.isPlaying)
            {
                gameplayCamera = null;
                UpdateSwitchStyleCamera(true);
            }
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

            playerGroundPosition.x = Mathf.Clamp(playerGroundPosition.x, -2.85f, 2.85f);
            playerGroundPosition.z = Mathf.Clamp(playerGroundPosition.z, -6.15f, -1.15f);

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
                        sensorHeight,
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
                    targetHeight,
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
                    swingPending = false;
                    pendingSwingStartedAt = 0f;
                }
            }

            float facePitch = Mathf.Lerp(120f, -30f, currentMouseY);
            if (isBackhand)
            {
                facePitch = -facePitch;
            }

            Quaternion targetRotation = racket.rotation;
            if (inputMode == BadmintonInputMode.Sensor)
            {
                if (!inputSnapshot.RacketStale)
                {
                    targetRotation = inputSnapshot.Racket.Orientation;
                }
            }
            else
            {
                targetRotation = Quaternion.Euler(
                    facePitch,
                    isBackhand ? 180f : 0f,
                    isBackhand ? 8f : -8f);
            }

            racket.rotation = Quaternion.Slerp(racket.rotation, targetRotation, 18f * Time.deltaTime);
            playerMarker.position = new Vector3(
                playerGroundPosition.x,
                0.55f,
                playerGroundPosition.z);
            playerMarker.rotation = Quaternion.identity;
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
            float playerZOffset = frontCourtTarget ? -1.15f : Mathf.Max(0f, backcourtPositionInset);
            float playerXOffset = isBackhand ? 0.95f : -0.95f;
            playerPositionMarker.position = new Vector3(
                landingPosition.x + playerXOffset,
                0.018f,
                landingPosition.z + playerZOffset);
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

            swingPending = true;
            swingUpward = inputSnapshot.SwingUpward;
            pendingSwingSpeed = speed;
            pendingStartAngle = inputSnapshot.SwingStartAngle;
            pendingSwingTime = contactWindow;
            pendingSwingStartedAt = Time.time;
            swingCooldown = 0.22f;
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
                velocity += DefaultWorldSwingDirection(inputSnapshot.SwingUpward || swingUpward) * boost;
            }

            racketHistory.Add(new RacketKinematicFrame
            {
                Time = now,
                FaceCenter = center,
                FaceNormal = racketFace.forward,
                FaceRight = racketFace.right,
                FaceUp = racketFace.up,
                FaceVelocity = velocity,
                SwingDirection = DefaultWorldSwingDirection(inputSnapshot.SwingUpward || swingUpward),
                SwingSpeed = activeSwingSpeed,
                FaceAngle = currentFaceAngle,
                TrackingConfidence = CurrentTrackingConfidence(),
                SwingUpward = inputSnapshot.SwingUpward || swingUpward
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

        private static Vector3 DefaultWorldSwingDirection(bool upward)
        {
            Vector3 vertical = upward ? Vector3.up : Vector3.down;
            return (Vector3.forward * 0.74f + vertical * 0.67f).normalized;
        }

        private void UpdateSwitchStyleCamera(bool immediate)
        {
            if (!useSwitchStyleCamera)
            {
                return;
            }

            ApplySwitchCameraPreset();

            if (gameplayCamera == null)
            {
                gameplayCamera = ResolveGameplayCamera();
                if (gameplayCamera == null)
                {
                    return;
                }
            }

            float racketX = racketFace != null ? racketFace.position.x : playerGroundPosition.x;
            float followSourceX = playerGroundPosition.x * 0.68f + racketX * 0.32f;
            float followX = Mathf.Clamp(
                followSourceX * Mathf.Max(0.78f, switchCameraLateralFollow),
                -switchCameraMaxLateralOffset,
                switchCameraMaxLateralOffset);
            float racketZ = racketFace != null ? racketFace.position.z : playerGroundPosition.z;
            float followSourceZ = playerGroundPosition.z * 0.82f + racketZ * 0.18f;
            float defaultPlayerZ = -2.7f * CourtLengthScale;
            float followZ = Mathf.Clamp(
                (followSourceZ - defaultPlayerZ) * switchCameraDepthFollow,
                -switchCameraMaxDepthOffset,
                switchCameraMaxDepthOffset);
            Vector3 targetPosition = switchCameraPosition + new Vector3(followX, 0f, followZ);
            Vector3 lookAt = switchCameraLookAt + new Vector3(followX * 0.9f, 0f, followZ * 0.72f);
            float blend = immediate
                ? 1f
                : 1f - Mathf.Exp(-switchCameraFollowSpeed * Time.deltaTime);

            Transform cameraTransform = gameplayCamera.transform;
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, targetPosition, blend);
            Vector3 lookDirection = lookAt - cameraTransform.position;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                cameraTransform.rotation = Quaternion.Slerp(
                    cameraTransform.rotation,
                    Quaternion.LookRotation(lookDirection.normalized, Vector3.up),
                    blend);
            }

            gameplayCamera.orthographic = false;
            gameplayCamera.fieldOfView = switchCameraFieldOfView;
        }

        private Camera ResolveGameplayCamera()
        {
            if (gameplayCameraOverride != null)
            {
                return gameplayCameraOverride;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera;
            }

            Camera[] cameras = FindObjectsOfType<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].enabled && cameras[i].gameObject.activeInHierarchy)
                {
                    return cameras[i];
                }
            }

            return null;
        }

        private void ApplySwitchCameraPreset()
        {
            if (!forceSwitchCameraPreset && switchCameraPresetVersion >= SwitchCameraPresetVersion)
            {
                return;
            }

            switchCameraPosition = new Vector3(0f, 6.9f, -12.1f);
            switchCameraLookAt = new Vector3(0f, 1.05f, -2.35f);
            switchCameraFieldOfView = 40f;
            switchCameraLateralFollow = 0.78f;
            switchCameraMaxLateralOffset = 2.4f;
            switchCameraDepthFollow = 0.28f;
            switchCameraMaxDepthOffset = 0.9f;
            switchCameraFollowSpeed = 12f;
            switchCameraPresetVersion = SwitchCameraPresetVersion;
        }

        private void OnGUI()
        {
            const float barWidth = 34f;
            float barHeight = Mathf.Min(360f, Screen.height * 0.58f);
            float x = Screen.width - 78f;
            float y = (Screen.height - barHeight) * 0.5f;

            Color previousColor = GUI.color;
            GUI.color = new Color(0.03f, 0.04f, 0.05f, 0.82f);
            GUI.Box(new Rect(x - 14f, y - 36f, barWidth + 28f, barHeight + 72f), GUIContent.none);

            GUI.color = new Color(0.16f, 0.18f, 0.2f, 1f);
            GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), Texture2D.whiteTexture);

            float powerHeight = barHeight * displayedPower;
            GUI.color = Color.Lerp(
                new Color(0.25f, 0.85f, 0.35f, 1f),
                new Color(1f, 0.25f, 0.08f, 1f),
                displayedPower);
            GUI.DrawTexture(
                new Rect(x + 5f, y + barHeight - powerHeight, barWidth - 10f, powerHeight),
                Texture2D.whiteTexture);

            float indicatorY = y + (1f - currentMouseY) * barHeight;
            GUI.color = new Color(1f, 0.82f, 0.08f, 1f);
            GUI.DrawTexture(new Rect(x - 7f, indicatorY - 3f, barWidth + 14f, 6f), Texture2D.whiteTexture);

            GUI.color = Color.white;
            uiLabelStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(x - 24f, y - 30f, barWidth + 48f, 24f), "立拍", uiLabelStyle);
            GUI.Label(new Rect(x - 24f, y + barHeight + 6f, barWidth + 48f, 24f), "平拍", uiLabelStyle);
            GUI.Label(
                new Rect(x - 44f, y + barHeight + 30f, barWidth + 88f, 24f),
                inputMode == BadmintonInputMode.Sensor
                    ? "Sensor racket"
                    : isBackhand ? "Backhand [Q]" : "Forehand [Q]",
                uiLabelStyle);

            GUI.color = new Color(0.03f, 0.04f, 0.05f, 0.82f);
            GUI.Box(new Rect(Screen.width * 0.5f - 90f, 18f, 180f, 48f), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(Screen.width * 0.5f - 84f, 24f, 168f, 34f),
                $"{playerScore}  :  {opponentScore}",
                new GUIStyle(uiLabelStyle) { fontSize = 24 });

            DrawModeAndDifficulty();
            DrawInputStatus();
            DrawCameraPreview();
            DrawHitDebug();

            float staminaRatio = opponentMaxStamina <= 0f
                ? 0f
                : Mathf.Clamp01(opponentStamina / opponentMaxStamina);
            GUI.color = new Color(0.03f, 0.04f, 0.05f, 0.82f);
            GUI.Box(new Rect(18f, 18f, 244f, 54f), GUIContent.none);
            GUI.color = new Color(0.16f, 0.18f, 0.2f, 1f);
            GUI.DrawTexture(new Rect(30f, 46f, 220f, 12f), Texture2D.whiteTexture);
            GUI.color = new Color(0.15f, 0.78f, 0.95f, 1f);
            GUI.DrawTexture(new Rect(30f, 46f, 220f * staminaRatio, 12f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(28f, 20f, 224f, 24f),
                $"Opponent Stamina  {Mathf.CeilToInt(opponentStamina)}/100",
                uiLabelStyle);

            if (incomingOpponentSmash)
            {
                GUI.color = smashReceiveReady
                    ? new Color(0.25f, 1f, 0.4f, 1f)
                    : new Color(1f, 0.35f, 0.18f, 1f);
                GUI.Label(
                    new Rect(Screen.width * 0.5f - 130f, 24f, 260f, 30f),
                    smashReceiveReady
                        ? "READY - SWING UP"
                        : inputMode == BadmintonInputMode.Sensor
                            ? "RAISE HAND / PREPARE SWING"
                            : "PRESS SPACE TO RECEIVE",
                    uiLabelStyle);
            }

            if (awaitingPlayerServe)
            {
                GUI.color = new Color(1f, 0.86f, 0.25f, 1f);
                GUI.Label(
                    new Rect(Screen.width * 0.5f - 150f, 62f, 300f, 30f),
                    "YOUR SERVE - SWING UP",
                    uiLabelStyle);
            }

            GUI.color = previousColor;
        }

        private void DrawModeAndDifficulty()
        {
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };

            float panelX = 18f;
            float panelY = 82f;
            GUI.color = new Color(0.03f, 0.04f, 0.05f, 0.86f);
            GUI.Box(new Rect(panelX, panelY, 244f, 112f), GUIContent.none);
            GUI.color = Color.white;

            if (GUI.Button(new Rect(panelX + 12f, panelY + 10f, 104f, 28f), "Single", buttonStyle))
            {
                SetGameMode(GameMode.SinglePlayer);
            }

            if (GUI.Button(new Rect(panelX + 128f, panelY + 10f, 104f, 28f), "Online", buttonStyle))
            {
                SetGameMode(GameMode.Multiplayer);
            }

            for (int i = 0; i < 4; i++)
            {
                Color previous = GUI.color;
                GUI.color = i == difficultyLevel
                    ? new Color(1f, 0.82f, 0.22f, 1f)
                    : Color.white;
                if (GUI.Button(
                    new Rect(panelX + 12f + i * 56f, panelY + 54f, 48f, 28f),
                    $"N{i}",
                    buttonStyle))
                {
                    ApplyDifficulty(i);
                }

                GUI.color = previous;
            }

            GUI.Label(
                new Rect(panelX + 12f, panelY + 84f, 220f, 22f),
                gameMode == GameMode.SinglePlayer ? "Single Player" : "Online - Coming Soon",
                uiLabelStyle);

            if (gameMode == GameMode.Multiplayer)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.72f);
                GUI.Box(
                    new Rect(Screen.width * 0.5f - 180f, Screen.height * 0.5f - 45f, 360f, 90f),
                    GUIContent.none);
                GUI.color = Color.white;
                GUI.Label(
                    new Rect(Screen.width * 0.5f - 170f, Screen.height * 0.5f - 20f, 340f, 40f),
                    "ONLINE MODE - COMING SOON",
                    new GUIStyle(uiLabelStyle) { fontSize = 20 });
            }
        }

        private void DrawInputStatus()
        {
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            GUIStyle statusStyle = new GUIStyle(uiLabelStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                wordWrap = true
            };

            float panelX = 18f;
            float panelY = 204f;
            float panelWidth = 328f;
            float panelHeight = 154f;
            GUI.color = new Color(0.03f, 0.04f, 0.05f, 0.86f);
            GUI.Box(new Rect(panelX, panelY, panelWidth, panelHeight), GUIContent.none);

            Color previous = GUI.color;
            GUI.color = inputMode == BadmintonInputMode.Sensor
                ? new Color(1f, 0.82f, 0.22f, 1f)
                : Color.white;
            if (GUI.Button(new Rect(panelX + 12f, panelY + 10f, 144f, 28f), "Sensor", buttonStyle))
            {
                inputMode = BadmintonInputMode.Sensor;
                ActivateInputMode(inputMode);
            }

            GUI.color = inputMode == BadmintonInputMode.Legacy
                ? new Color(1f, 0.82f, 0.22f, 1f)
                : Color.white;
            if (GUI.Button(new Rect(panelX + 172f, panelY + 10f, 144f, 28f), "Legacy", buttonStyle))
            {
                inputMode = BadmintonInputMode.Legacy;
                ActivateInputMode(inputMode);
            }

            GUI.color = previous;
            GUI.Label(
                new Rect(panelX + 12f, panelY + 46f, panelWidth - 24f, 20f),
                inputSnapshot.Status,
                statusStyle);
            GUI.Label(
                new Rect(panelX + 12f, panelY + 68f, panelWidth - 24f, 20f),
                $"Camera: {inputSnapshot.CameraStatus}",
                statusStyle);
            GUI.Label(
                new Rect(panelX + 12f, panelY + 90f, panelWidth - 24f, 20f),
                $"Phone: {inputSnapshot.PhoneStatus}",
                statusStyle);
            GUI.Label(
                new Rect(panelX + 12f, panelY + 112f, panelWidth - 24f, 34f),
                string.IsNullOrEmpty(inputSnapshot.PhoneUrl)
                    ? "Phone URL: unavailable"
                    : $"Phone URL: {inputSnapshot.PhoneUrl}",
                statusStyle);
        }

        private void DrawCameraPreview()
        {
            if (inputMode != BadmintonInputMode.Sensor)
            {
                return;
            }

            const float margin = 18f;
            float panelWidth = Mathf.Clamp(Screen.width * 0.24f, 220f, 360f);
            panelWidth = Mathf.Min(panelWidth, Screen.width - margin * 2f);
            float panelHeight = panelWidth * 9f / 16f;
            float panelY = Screen.height - panelHeight - margin;

            if (panelY < 372f)
            {
                panelHeight = Mathf.Min(panelHeight, Mathf.Max(96f, Screen.height - 372f - margin));
                panelWidth = panelHeight * 16f / 9f;
                panelY = Screen.height - panelHeight - margin;
            }

            Rect panelRect = new Rect(margin, panelY, panelWidth, panelHeight);
            Color previousColor = GUI.color;
            Matrix4x4 previousMatrix = GUI.matrix;

            GUI.color = new Color(0.03f, 0.04f, 0.05f, 0.88f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);

            Texture preview = inputSnapshot.CameraPreviewTexture;
            if (preview == null || preview.width <= 16 || preview.height <= 16)
            {
                GUI.color = new Color(0.82f, 0.86f, 0.9f, 0.82f);
                GUI.Label(
                    new Rect(panelRect.x + 10f, panelRect.y + panelRect.height * 0.5f - 10f, panelRect.width - 20f, 20f),
                    "Camera warming up",
                    uiLabelStyle);
                GUI.color = previousColor;
                GUI.matrix = previousMatrix;
                return;
            }

            Rect imageRect = FitRect(panelRect, preview.width / Mathf.Max(1f, (float)preview.height));
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(
                imageRect,
                preview,
                PreviewTexCoords(inputSnapshot.CameraPreviewFlipHorizontally),
                true);

            DrawPosePreviewSkeleton(
                imageRect,
                inputSnapshot.CameraPreviewLandmarks,
                inputSnapshot.CameraPreviewFlipHorizontally);

            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private void DrawHitDebug()
        {
            if (!showHitDebug)
            {
                return;
            }

            GUIStyle debugStyle = new GUIStyle(uiLabelStyle)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 12,
                wordWrap = true
            };
            float panelWidth = 300f;
            float panelHeight = 112f;
            float panelX = Screen.width - panelWidth - 18f;
            float panelY = 18f;
            Color previous = GUI.color;
            GUI.color = new Color(0.03f, 0.04f, 0.05f, 0.84f);
            GUI.Box(new Rect(panelX, panelY, panelWidth, panelHeight), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(panelX + 12f, panelY + 10f, panelWidth - 24f, panelHeight - 20f),
                $"Hit: {(lastHitResult.Hit ? lastHitResult.Shot.ToString() : "Miss")}  {lastHitResult.Reason}\n" +
                $"Q {lastHitResult.Quality:0.00}  S {lastHitResult.SpatialQuality:0.00}  T {lastHitResult.TimingQuality:0.00}  D {lastHitResult.DirectionQuality:0.00}\n" +
                $"Face {lastHitResult.FaceQuality:0.00}  Power {lastHitResult.PowerQuality:0.00}  Assist {lastHitResult.AssistUsed}  Magnet {lastHitResult.MagnetUsed}",
                debugStyle);
            GUI.color = previous;
        }

        private void DrawPosePreviewSkeleton(
            Rect imageRect,
            BadmintonPoseLandmark[] landmarks,
            bool flipHorizontally)
        {
            if (!inputSnapshot.CameraPreviewPoseVisible ||
                landmarks == null ||
                landmarks.Length < 33)
            {
                return;
            }

            for (int i = 0; i < PosePreviewBonePairs.Length; i += 2)
            {
                int from = PosePreviewBonePairs[i];
                int to = PosePreviewBonePairs[i + 1];
                if (!IsPreviewLandmarkVisible(landmarks, from) || !IsPreviewLandmarkVisible(landmarks, to))
                {
                    continue;
                }

                DrawGuiLine(
                    LandmarkToPreviewPoint(landmarks[from], imageRect, flipHorizontally),
                    LandmarkToPreviewPoint(landmarks[to], imageRect, flipHorizontally),
                    new Color(0.15f, 1f, 0.88f, 0.92f),
                    2f);
            }

            for (int i = 0; i < landmarks.Length; i++)
            {
                if (!IsPreviewLandmarkVisible(landmarks, i))
                {
                    continue;
                }

                Vector2 point = LandmarkToPreviewPoint(landmarks[i], imageRect, flipHorizontally);
                float size = i == BadmintonPoseLandmarkMapper.RightWrist ||
                             i == BadmintonPoseLandmarkMapper.RightIndex ||
                             i == BadmintonPoseLandmarkMapper.RightPinky ||
                             i == BadmintonPoseLandmarkMapper.RightThumb
                    ? 5f
                    : 3.5f;
                GUI.color = i == BadmintonPoseLandmarkMapper.RightWrist ||
                            i == BadmintonPoseLandmarkMapper.RightIndex ||
                            i == BadmintonPoseLandmarkMapper.RightPinky ||
                            i == BadmintonPoseLandmarkMapper.RightThumb
                    ? new Color(1f, 0.82f, 0.2f, 0.96f)
                    : new Color(1f, 1f, 1f, 0.92f);
                GUI.DrawTexture(new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size), Texture2D.whiteTexture);
            }
        }

        private static Rect FitRect(Rect bounds, float aspect)
        {
            float safeAspect = Mathf.Max(0.01f, aspect);
            float boundsAspect = bounds.width / Mathf.Max(1f, bounds.height);
            if (safeAspect > boundsAspect)
            {
                float height = bounds.width / safeAspect;
                return new Rect(bounds.x, bounds.y + (bounds.height - height) * 0.5f, bounds.width, height);
            }

            float width = bounds.height * safeAspect;
            return new Rect(bounds.x + (bounds.width - width) * 0.5f, bounds.y, width, bounds.height);
        }

        private static bool IsPreviewLandmarkVisible(BadmintonPoseLandmark[] landmarks, int index)
        {
            return index >= 0 &&
                   index < landmarks.Length &&
                   landmarks[index].Visibility >= 0.25f;
        }

        private static Vector2 LandmarkToPreviewPoint(
            BadmintonPoseLandmark landmark,
            Rect imageRect,
            bool flipHorizontally)
        {
            float x = Mathf.Clamp01(landmark.X);
            float y = Mathf.Clamp01(landmark.Y);
            if (flipHorizontally)
            {
                x = 1f - x;
            }

            // Preview uses GUI y-down coordinates, while gameplay landmarks are normalized y-up.
            y = 1f - y;

            return new Vector2(
                imageRect.x + x * imageRect.width,
                imageRect.y + y * imageRect.height);
        }

        private static Rect PreviewTexCoords(bool flipHorizontally)
        {
            return new Rect(
                flipHorizontally ? 1f : 0f,
                0f,
                flipHorizontally ? -1f : 1f,
                1f);
        }

        private static void DrawGuiLine(Vector2 start, Vector2 end, Color color, float width)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length <= 0.01f)
            {
                return;
            }

            Color previousColor = GUI.color;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
            GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, length, width), Texture2D.whiteTexture);
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private void SetGameMode(GameMode mode)
        {
            if (gameMode == mode)
            {
                return;
            }

            gameMode = mode;
            RestartMatch();
        }

        private void ApplyDifficulty(int level)
        {
            difficultyLevel = Mathf.Clamp(level, 0, 3);
            switch (difficultyLevel)
            {
                case 0:
                    opponentMaxStamina = 30f;
                    opponentSmashChance = 0.1f;
                    opponentSmashReceiveChance = 0.05f;
                    break;
                case 1:
                    opponentMaxStamina = 50f;
                    opponentSmashChance = 0.25f;
                    opponentSmashReceiveChance = 0.2f;
                    break;
                case 2:
                    opponentMaxStamina = 70f;
                    opponentSmashChance = 0.5f;
                    opponentSmashReceiveChance = 0.35f;
                    break;
                default:
                    opponentMaxStamina = 100f;
                    opponentSmashChance = 0.75f;
                    opponentSmashReceiveChance = 0.5f;
                    break;
            }

            RestartMatch();
        }

        private void RestartMatch()
        {
            StopAllCoroutines();
            playerScore = 0;
            opponentScore = 0;
            rallyWinner = 0;
            playerServing = false;
            opponentStamina = opponentMaxStamina;
            shuttleIncoming = false;
            incomingOpponentSmash = false;
            smashReceiveReady = false;
            awaitingPlayerServe = false;
            swingPending = false;
            pendingSwingStartedAt = 0f;
            ClearHitHistory();
            shuttleTrail.emitting = false;
            shuttle.gameObject.SetActive(false);
            landingMarker.gameObject.SetActive(false);
            playerPositionMarker.gameObject.SetActive(false);

            StartCoroutine(GameLoop());
        }

        private IEnumerator FeedOneShuttle()
        {
            OpponentShotType opponentShot = ChooseOpponentShot(false, true);
            SpendOpponentStamina(opponentShot);
            bool incomingClear =
                opponentShot == OpponentShotType.Clear ||
                opponentShot == OpponentShotType.Lift;
            float sourceSide = opponentScore % 2 == 1 ? 1f : -1f;

            Vector3 start = new Vector3(
                sourceSide * (1.55f + Random.Range(-0.06f, 0.06f)),
                Random.Range(1.45f, 1.6f),
                (2.05f + Random.Range(-0.06f, 0.06f)) * CourtLengthScale);

            float targetDepth = incomingClear
                ? Random.Range(5.98f, 6.58f)
                : Random.Range(2.2f, 3.15f);
            Vector3 target = new Vector3(
                -sourceSide * Random.Range(0.9f, 2.45f),
                0.09f,
                -targetDepth * CourtLengthScale);

            float duration = incomingClear ? clearDuration : dropShotDuration;
            float arcHeight = incomingClear
                ? clearArcHeight
                : opponentShot == OpponentShotType.Net ? 0.85f : dropShotArcHeight;

            yield return AnimateOpponentHit(start, sourceSide);
            if (incomingClear)
            {
                SetTrailColors(
                    new Color(1f, 0.9f, 0.42f, 0.82f),
                    new Color(1f, 0.82f, 0.25f, 0f));
            }
            else
            {
                SetTrailColors(
                    new Color(0.25f, 1f, 0.35f, 0.82f),
                    new Color(0.2f, 0.8f, 0.25f, 0f));
            }

            shuttle.position = start;
            shuttle.gameObject.SetActive(true);
            shuttleTrail.Clear();
            shuttleTrail.emitting = true;
            yield return PlayIncomingShuttle(start, target, duration, arcHeight, false);

            shuttleTrail.emitting = false;
            shuttle.gameObject.SetActive(false);
            landingMarker.gameObject.SetActive(false);
            playerPositionMarker.gameObject.SetActive(false);
        }

        private IEnumerator PlayerServe()
        {
            float serviceSide = playerScore % 2 == 1 ? -1f : 1f;
            playerGroundPosition = new Vector3(
                serviceSide * 1.55f,
                0.55f,
                -2.05f * CourtLengthScale);

            Vector3 start = new Vector3(
                serviceSide * 1.55f,
                1.05f,
                -2.05f * CourtLengthScale);
            shuttle.position = start;
            shuttle.gameObject.SetActive(true);
            shuttleTrail.Clear();
            shuttleTrail.emitting = false;
            awaitingPlayerServe = true;
            playerServeGestureReady = false;
            swingPending = false;

            while (!playerServeGestureReady)
            {
                shuttle.position = racketFace.position + racketFace.forward * 0.18f;
                yield return null;
            }

            awaitingPlayerServe = false;
            float power = Mathf.Clamp01(
                Mathf.InverseLerp(minimumSwingSpeed, fastSwingSpeed, playerServeSpeed));
            float targetDepth = Mathf.Lerp(2.45f, 5.9f, power);
            Vector3 target = new Vector3(
                -serviceSide * Random.Range(0.85f, 2.2f),
                0.09f,
                targetDepth * CourtLengthScale);
            float duration = Mathf.Lerp(1.5f, 1.9f, power);
            float arcHeight = Mathf.Lerp(1.35f, 3.25f, power);

            SetTrailColors(
                new Color(1f, 0.9f, 0.42f, 0.82f),
                new Color(1f, 0.82f, 0.25f, 0f));
            shuttleTrail.Clear();
            shuttleTrail.emitting = true;

            Vector3 serveStart = shuttle.position;
            float progress = 0f;
            Vector3 previousPosition = serveStart;
            const float opponentContactProgress = 0.86f;
            Vector3 contactPoint = EvaluateArc(
                serveStart,
                target,
                opponentContactProgress,
                arcHeight);
            float opponentSide = contactPoint.x >= 0f ? 1f : -1f;
            Vector3 readyPosition = GetOpponentReadyPosition(contactPoint, opponentSide);

            landingMarker.position = new Vector3(target.x, 0.025f, target.z);
            landingMarker.gameObject.SetActive(true);

            while (progress < opponentContactProgress)
            {
                progress = Mathf.Min(1f, progress + Time.deltaTime / duration);
                Vector3 position = EvaluateArc(serveStart, target, progress, arcHeight);
                MoveShuttle(position, ref previousPosition);

                Vector3 previousOpponentPosition = opponentRacket.position;
                opponentRacket.position = Vector3.MoveTowards(
                    opponentRacket.position,
                    readyPosition,
                    opponentMoveSpeed * Time.deltaTime);
                SpendOpponentRunStamina(previousOpponentPosition, opponentRacket.position);
                if (opponentStamina <= 0f)
                {
                    break;
                }

                yield return null;
            }

            landingMarker.gameObject.SetActive(false);
            if (opponentStamina <= 0f ||
                Vector3.Distance(opponentRacket.position, readyPosition) > opponentReachTolerance)
            {
                while (progress < 1f)
                {
                    progress = Mathf.Min(1f, progress + Time.deltaTime / duration);
                    Vector3 position = EvaluateArc(serveStart, target, progress, arcHeight);
                    MoveShuttle(position, ref previousPosition);
                    yield return null;
                }

                rallyWinner = 1;
                yield return new WaitForSeconds(0.45f);
            }
            else
            {
                OpponentShotType returnShot = ChooseOpponentShot(false, true);
                if (!CanOpponentAfford(returnShot))
                {
                    rallyWinner = 1;
                    yield return new WaitForSeconds(0.45f);
                    yield break;
                }

                SpendOpponentStamina(returnShot);
                yield return AnimateOpponentSwing(opponentSide);
                yield return OpponentReturn(previousPosition, opponentSide, returnShot);
            }

            shuttleTrail.emitting = false;
            shuttle.gameObject.SetActive(false);
            landingMarker.gameObject.SetActive(false);
            playerPositionMarker.gameObject.SetActive(false);
        }

        private IEnumerator PlayIncomingShuttle(
            Vector3 start,
            Vector3 target,
            float duration,
            float arcHeight,
            bool isOpponentSmash)
        {
            landingMarker.position = new Vector3(target.x, 0.025f, target.z);
            landingMarker.gameObject.SetActive(true);
            incomingFrontCourt = Mathf.Abs(target.z) < 4f * CourtLengthScale;
            incomingOpponentSmash = isOpponentSmash;
            if (!isOpponentSmash)
            {
                smashReceiveReady = false;
            }
            playerPositionMarker.gameObject.SetActive(true);
            UpdatePlayerPositionMarker();
            shuttleIncoming = true;
            swingPending = false;
            pendingSwingStartedAt = 0f;
            ClearHitHistory();

            float progress = 0f;
            Vector3 previousPosition = start;
            RecordShuttleFrame(start, Vector3.zero);
            while (progress < 1f)
            {
                float movementScale = isOpponentSmash
                    ? opponentSmashSpeedBeforeNet
                    : 1f;
                if (previousPosition.z <= 0f)
                {
                    movementScale = isOpponentSmash
                        ? opponentSmashSpeedAfterNet
                        : speedAfterNet;
                }
                progress = Mathf.Min(1f, progress + Time.deltaTime * movementScale / duration);
                Vector3 position = EvaluateArc(start, target, progress, arcHeight);
                MoveShuttle(position, ref previousPosition);

                if (TryHitShuttle(out ShotType shot))
                {
                    shuttleIncoming = false;
                    landingMarker.gameObject.SetActive(false);
                    playerPositionMarker.gameObject.SetActive(false);
                    yield return ReturnShuttle(shot, previousPosition);
                    yield break;
                }

                yield return null;
            }

            shuttleIncoming = false;
            incomingOpponentSmash = false;
            smashReceiveReady = false;
            shuttle.position = target;
            playerPositionMarker.gameObject.SetActive(false);
            rallyWinner = 2;
            yield return new WaitForSeconds(0.45f);
        }

        private bool TryHitShuttle(out ShotType shot)
        {
            shot = ShotType.Net;
            if (!swingPending)
            {
                return false;
            }

            RacketHitContext context = new RacketHitContext
            {
                Now = Time.time,
                SwingStartedAt = pendingSwingStartedAt > 0f ? pendingSwingStartedAt : Time.time,
                SwingExpiresAt = pendingSwingStartedAt > 0f ? pendingSwingStartedAt + contactWindow : Time.time,
                SwingPending = swingPending,
                SwingUpward = swingUpward,
                SwingSpeed = pendingSwingSpeed,
                SwingStartAngle = pendingStartAngle,
                IncomingFrontCourt = incomingFrontCourt,
                IncomingOpponentSmash = incomingOpponentSmash,
                SmashReceiveReady = smashReceiveReady,
                IsBackhand = isBackhand,
                MinimumSwingSpeed = minimumSwingSpeed,
                MediumSwingSpeed = mediumSwingSpeed,
                FastSwingSpeed = fastSwingSpeed
            };

            lastHitResult = hitResolver.Resolve(
                racketHistory,
                shuttleHistory,
                context,
                CurrentHitSettings());

            if (lastHitResult.ConsumeSwing)
            {
                swingPending = false;
                pendingSwingStartedAt = 0f;
            }

            if (!lastHitResult.Hit)
            {
                return false;
            }

            pendingSwingSpeed = lastHitResult.SwingSpeed;
            pendingStartAngle = lastHitResult.FaceAngle;
            swingUpward = lastHitResult.SwingUpward;
            shot = MapResolvedShot(lastHitResult.Shot);
            if (incomingOpponentSmash)
            {
                incomingOpponentSmash = false;
                smashReceiveReady = false;
            }

            return shot != ShotType.Miss;
        }

        private static ShotType MapResolvedShot(RacketResolvedShot shot)
        {
            switch (shot)
            {
                case RacketResolvedShot.Net:
                    return ShotType.Net;
                case RacketResolvedShot.Drop:
                    return ShotType.Drop;
                case RacketResolvedShot.Clear:
                    return ShotType.Clear;
                case RacketResolvedShot.Smash:
                    return ShotType.Smash;
                case RacketResolvedShot.Drive:
                    return ShotType.Drive;
                case RacketResolvedShot.Out:
                    return ShotType.Out;
                default:
                    return ShotType.Miss;
            }
        }

        private IEnumerator ReturnShuttle(ShotType shot, Vector3 start)
        {
            Vector3 target;
            float duration;
            float arcHeight;
            SetTrailForShot(shot);

            switch (shot)
            {
                case ShotType.Drop:
                    target = new Vector3(Random.Range(-2.2f, 2.2f), 0.09f, 2.65f * CourtLengthScale);
                    duration = 1.1f;
                    arcHeight = 0.9f;
                    break;
                case ShotType.Clear:
                    target = new Vector3(Random.Range(-2.3f, 2.3f), 0.09f, 6.2f * CourtLengthScale);
                    duration = 1.8f;
                    arcHeight = 3.8f;
                    break;
                case ShotType.Smash:
                    target = new Vector3(Random.Range(-2.25f, 2.25f), 0.09f, 4.7f * CourtLengthScale);
                    duration = Mathf.Lerp(
                        0.82f,
                        0.42f,
                        Mathf.InverseLerp(minimumSwingSpeed, fastSwingSpeed, pendingSwingSpeed));
                    arcHeight = 0.18f;
                    break;
                case ShotType.Drive:
                    target = new Vector3(Random.Range(-2.3f, 2.3f), 0.09f, 6.45f * CourtLengthScale);
                    duration = 0.82f;
                    arcHeight = 0.55f;
                    break;
                case ShotType.Out:
                    target = new Vector3(Random.Range(-3.8f, 3.8f), 0.09f, 8.25f * CourtLengthScale);
                    duration = 1.25f;
                    arcHeight = 1.5f;
                    break;
                default:
                    target = new Vector3(Random.Range(-1.9f, 1.9f), 0.09f, 2.35f * CourtLengthScale);
                    duration = 1.05f;
                    arcHeight = 1.05f;
                    break;
            }

            landingMarker.position = new Vector3(target.x, 0.025f, target.z);
            landingMarker.gameObject.SetActive(true);

            float progress = 0f;
            Vector3 previousPosition = start;
            const float opponentContactProgress = 0.86f;
            Vector3 opponentContactPoint = EvaluateArc(
                start,
                target,
                opponentContactProgress,
                arcHeight);
            float opponentSide = opponentContactPoint.x >= 0f ? 1f : -1f;
            Vector3 opponentReadyPosition = GetOpponentReadyPosition(
                opponentContactPoint,
                opponentSide);

            while (progress < opponentContactProgress)
            {
                progress = Mathf.Min(1f, progress + Time.deltaTime / duration);
                Vector3 position = EvaluateArc(start, target, progress, arcHeight);
                MoveShuttle(position, ref previousPosition);
                Vector3 previousOpponentPosition = opponentRacket.position;
                opponentRacket.position = Vector3.MoveTowards(
                    opponentRacket.position,
                    opponentReadyPosition,
                    opponentMoveSpeed * Time.deltaTime);
                SpendOpponentRunStamina(previousOpponentPosition, opponentRacket.position);
                if (opponentStamina <= 0f)
                {
                    break;
                }

                yield return null;
            }

            landingMarker.gameObject.SetActive(false);
            bool opponentReached =
                opponentStamina > 0f &&
                Vector3.Distance(opponentRacket.position, opponentReadyPosition) <= opponentReachTolerance;
            if (!opponentReached)
            {
                while (progress < 1f)
                {
                    progress = Mathf.Min(1f, progress + Time.deltaTime / duration);
                    Vector3 position = EvaluateArc(start, target, progress, arcHeight);
                    MoveShuttle(position, ref previousPosition);
                    yield return null;
                }

                rallyWinner = 1;
                yield return new WaitForSeconds(0.45f);
                yield break;
            }

            if (shot == ShotType.Smash && Random.value > opponentSmashReceiveChance)
            {
                while (progress < 1f)
                {
                    progress = Mathf.Min(1f, progress + Time.deltaTime / duration);
                    Vector3 position = EvaluateArc(start, target, progress, arcHeight);
                    MoveShuttle(position, ref previousPosition);
                    yield return null;
                }

                rallyWinner = 1;
                yield return new WaitForSeconds(0.45f);
                yield break;
            }

            OpponentShotType opponentShot = ChooseOpponentShot(
                shot == ShotType.Clear,
                Mathf.Abs(previousPosition.z) < 4f * CourtLengthScale);
            if (!CanOpponentAfford(opponentShot))
            {
                rallyWinner = 1;
                yield return new WaitForSeconds(0.45f);
                yield break;
            }

            SpendOpponentStamina(opponentShot);
            if (opponentShot == OpponentShotType.Smash)
            {
                incomingOpponentSmash = true;
                smashReceiveReady = false;
            }

            yield return AnimateOpponentSwing(opponentSide);
            yield return OpponentReturn(previousPosition, opponentSide, opponentShot);
        }

        private IEnumerator OpponentReturn(
            Vector3 start,
            float sourceSide,
            OpponentShotType shot)
        {
            float targetDepth;
            float duration;
            float arcHeight;
            bool isSmash = shot == OpponentShotType.Smash;

            switch (shot)
            {
                case OpponentShotType.Net:
                    targetDepth = Random.Range(2.15f, 2.9f);
                    duration = 1.15f;
                    arcHeight = 0.9f;
                    SetTrailColors(
                        new Color(0.25f, 1f, 0.35f, 0.82f),
                        new Color(0.2f, 0.8f, 0.25f, 0f));
                    break;
                case OpponentShotType.Drop:
                    targetDepth = Random.Range(2.7f, 3.35f);
                    duration = 1.35f;
                    arcHeight = 1.35f;
                    SetTrailColors(
                        new Color(0.25f, 1f, 0.35f, 0.82f),
                        new Color(0.2f, 0.8f, 0.25f, 0f));
                    break;
                case OpponentShotType.Smash:
                    targetDepth = Random.Range(3.4f, 5.2f);
                    duration = 0.85f;
                    arcHeight = 0.12f;
                    SetTrailColors(
                        new Color(1f, 0.12f, 0.08f, 0.9f),
                        new Color(0.85f, 0.02f, 0.02f, 0f));
                    break;
                default:
                    targetDepth = Random.Range(5.98f, 6.58f);
                    duration = shot == OpponentShotType.Lift ? 2.2f : 2.05f;
                    arcHeight = shot == OpponentShotType.Lift ? 4.5f : 4f;
                    SetTrailColors(
                        new Color(1f, 0.9f, 0.42f, 0.82f),
                        new Color(1f, 0.82f, 0.25f, 0f));
                    break;
            }

            Vector3 target = new Vector3(
                -sourceSide * Random.Range(0.85f, 2.45f),
                0.09f,
                -targetDepth * CourtLengthScale);

            yield return PlayIncomingShuttle(start, target, duration, arcHeight, isSmash);
        }

        private OpponentShotType ChooseOpponentShot(bool canSmash, bool fromFrontCourt)
        {
            if (canSmash && opponentStamina >= 10f && Random.value < opponentSmashChance)
            {
                return OpponentShotType.Smash;
            }

            if (fromFrontCourt && opponentStamina >= 3f && Random.value < 0.45f)
            {
                return OpponentShotType.Lift;
            }

            if (opponentStamina >= 5f && Random.value < 0.34f)
            {
                return OpponentShotType.Clear;
            }

            if (opponentStamina >= 3f)
            {
                return Random.value < 0.5f
                    ? OpponentShotType.Net
                    : OpponentShotType.Drop;
            }

            if (opponentStamina >= 3f)
            {
                return OpponentShotType.Lift;
            }

            return OpponentShotType.Net;
        }

        private void SpendOpponentStamina(OpponentShotType shot)
        {
            float cost;
            switch (shot)
            {
                case OpponentShotType.Clear:
                    cost = 5f;
                    break;
                case OpponentShotType.Smash:
                    cost = 10f;
                    break;
                case OpponentShotType.Net:
                case OpponentShotType.Drop:
                case OpponentShotType.Lift:
                    cost = 3f;
                    break;
                default:
                    cost = 0f;
                    break;
            }

            opponentStamina = Mathf.Max(0f, opponentStamina - cost);
        }

        private void SpendOpponentRunStamina(Vector3 from, Vector3 to)
        {
            Vector2 fromGround = new Vector2(from.x, from.z);
            Vector2 toGround = new Vector2(to.x, to.z);
            float distance = Vector2.Distance(fromGround, toGround);
            opponentStamina = Mathf.Max(
                0f,
                opponentStamina - distance * opponentRunStaminaPerMeter);
        }

        private bool CanOpponentAfford(OpponentShotType shot)
        {
            float cost;
            switch (shot)
            {
                case OpponentShotType.Clear:
                    cost = 5f;
                    break;
                case OpponentShotType.Smash:
                    cost = 10f;
                    break;
                default:
                    cost = 3f;
                    break;
            }

            return opponentStamina >= cost;
        }

        private IEnumerator AnimateOpponentHit(Vector3 contactPoint, float sourceSide)
        {
            Vector3 readyPosition = GetOpponentReadyPosition(contactPoint, sourceSide);
            while (Vector3.Distance(opponentRacket.position, readyPosition) > opponentReachTolerance)
            {
                if (opponentStamina <= 0f)
                {
                    yield break;
                }

                Vector3 previousOpponentPosition = opponentRacket.position;
                opponentRacket.position = Vector3.MoveTowards(
                    opponentRacket.position,
                    readyPosition,
                    opponentMoveSpeed * Time.deltaTime);
                SpendOpponentRunStamina(previousOpponentPosition, opponentRacket.position);
                yield return null;
            }

            yield return AnimateOpponentSwing(sourceSide);
        }

        private IEnumerator AnimateOpponentSwing(float sourceSide)
        {
            opponentRacket.rotation = Quaternion.Euler(18f, 180f, sourceSide * 12f);

            float elapsed = 0f;
            const float swingDuration = 0.24f;
            while (elapsed < swingDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / swingDuration);
                float swing = Mathf.Sin(t * Mathf.PI) * 68f;
                opponentRacket.rotation = Quaternion.Euler(
                    18f - swing,
                    180f,
                    sourceSide * 12f);
                yield return null;
            }
        }

        private Vector3 GetOpponentReadyPosition(Vector3 contactPoint, float sourceSide)
        {
            return new Vector3(
                contactPoint.x - sourceSide * 0.18f,
                opponentRacketGroundHeight,
                contactPoint.z + 0.08f);
        }

        private void SetTrailForShot(ShotType shot)
        {
            switch (shot)
            {
                case ShotType.Clear:
                    SetTrailColors(
                        new Color(1f, 0.9f, 0.42f, 0.82f),
                        new Color(1f, 0.82f, 0.25f, 0f));
                    break;
                case ShotType.Smash:
                    SetTrailColors(
                        new Color(1f, 0.12f, 0.08f, 0.9f),
                        new Color(0.85f, 0.02f, 0.02f, 0f));
                    break;
                case ShotType.Drive:
                    SetTrailColors(
                        new Color(1f, 0.28f, 0.68f, 0.88f),
                        new Color(0.95f, 0.12f, 0.55f, 0f));
                    break;
                default:
                    SetTrailColors(
                        new Color(0.25f, 1f, 0.35f, 0.82f),
                        new Color(0.2f, 0.8f, 0.25f, 0f));
                    break;
            }
        }

        private void SetTrailColors(Color startColor, Color endColor)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(startColor, 0f),
                    new GradientColorKey(startColor, 0.4f),
                    new GradientColorKey(endColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(startColor.a, 0f),
                    new GradientAlphaKey(startColor.a * 0.72f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
            shuttleTrail.colorGradient = gradient;
        }

        private static Vector3 EvaluateArc(Vector3 start, Vector3 target, float t, float height)
        {
            Vector3 position = Vector3.Lerp(start, target, t);
            position.y += 4f * height * t * (1f - t);
            return position;
        }

        private void MoveShuttle(Vector3 position, ref Vector3 previousPosition)
        {
            Vector3 direction = position - previousPosition;
            if (direction.sqrMagnitude > 0.00001f)
            {
                shuttle.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            Vector3 velocity = direction / Mathf.Max(Time.deltaTime, 0.001f);
            RecordShuttleFrame(position, velocity);
            shuttle.position = position;
            previousPosition = position;
        }

        private GameObject CreatePixelRacket()
        {
            GameObject root = new GameObject("Pixel Racket");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(0.8f, 0.65f, -2.7f * CourtLengthScale);

            CreateBlock("Grip", root.transform, new Vector3(0f, 0.275f, 0f),
                new Vector3(0.14f, 0.55f, 0.14f), racketDark);
            CreateBlock("Shaft", root.transform, new Vector3(0f, 0.79f, 0f),
                new Vector3(0.07f, 0.48f, 0.07f), racketRed);

            racketFace = new GameObject("Racket Face").transform;
            racketFace.SetParent(root.transform, false);
            racketFace.localPosition = new Vector3(0f, 1.35f, 0f);

            CreateBlock("Frame Top", racketFace, new Vector3(0f, 0.38f, 0f),
                new Vector3(0.58f, 0.09f, 0.09f), racketRed);
            CreateBlock("Frame Bottom", racketFace, new Vector3(0f, -0.38f, 0f),
                new Vector3(0.58f, 0.09f, 0.09f), racketRed);
            CreateBlock("Frame Left", racketFace, new Vector3(-0.29f, 0f, 0f),
                new Vector3(0.09f, 0.76f, 0.09f), racketRed);
            CreateBlock("Frame Right", racketFace, new Vector3(0.29f, 0f, 0f),
                new Vector3(0.09f, 0.76f, 0.09f), racketRed);

            for (int i = -2; i <= 2; i++)
            {
                CreateBlock($"Vertical String {i + 3}", racketFace, new Vector3(i * 0.095f, 0f, 0.015f),
                    new Vector3(0.018f, 0.68f, 0.018f), racketString);
            }

            for (int i = -3; i <= 3; i++)
            {
                CreateBlock($"Horizontal String {i + 4}", racketFace, new Vector3(0f, i * 0.09f, 0.015f),
                    new Vector3(0.5f, 0.018f, 0.018f), racketString);
            }

            return root;
        }

        private GameObject CreatePlayerMarker()
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Cube);
            player.name = "Player Marker";
            player.transform.SetParent(transform, false);
            player.transform.localScale = new Vector3(0.55f, 1.1f, 0.55f);
            player.GetComponent<MeshRenderer>().sharedMaterial = racketDark;
            Destroy(player.GetComponent<Collider>());
            return player;
        }

        private GameObject CreateOpponentRacket()
        {
            GameObject root = new GameObject("Opponent Pixel Racket");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(-1.55f, 0.8f, 2.05f * CourtLengthScale);
            root.transform.rotation = Quaternion.Euler(18f, 180f, 0f);

            CreateBlock("Grip", root.transform, new Vector3(0f, 0.275f, 0f),
                new Vector3(0.14f, 0.55f, 0.14f), racketDark);
            CreateBlock("Shaft", root.transform, new Vector3(0f, 0.79f, 0f),
                new Vector3(0.07f, 0.48f, 0.07f), racketBlue);

            Transform face = new GameObject("Opponent Racket Face").transform;
            face.SetParent(root.transform, false);
            face.localPosition = new Vector3(0f, 1.35f, 0f);

            CreateBlock("Frame Top", face, new Vector3(0f, 0.38f, 0f),
                new Vector3(0.58f, 0.09f, 0.09f), racketBlue);
            CreateBlock("Frame Bottom", face, new Vector3(0f, -0.38f, 0f),
                new Vector3(0.58f, 0.09f, 0.09f), racketBlue);
            CreateBlock("Frame Left", face, new Vector3(-0.29f, 0f, 0f),
                new Vector3(0.09f, 0.76f, 0.09f), racketBlue);
            CreateBlock("Frame Right", face, new Vector3(0.29f, 0f, 0f),
                new Vector3(0.09f, 0.76f, 0.09f), racketBlue);

            for (int i = -2; i <= 2; i++)
            {
                CreateBlock($"Vertical String {i + 3}", face, new Vector3(i * 0.095f, 0f, 0.015f),
                    new Vector3(0.018f, 0.68f, 0.018f), racketString);
            }

            for (int i = -3; i <= 3; i++)
            {
                CreateBlock($"Horizontal String {i + 4}", face, new Vector3(0f, i * 0.09f, 0.015f),
                    new Vector3(0.5f, 0.018f, 0.018f), racketString);
            }

            return root;
        }

        private GameObject CreateShuttlecock()
        {
            GameObject root = new GameObject("Shuttlecock");
            root.transform.SetParent(transform, false);

            CreateBlock("Cork", root.transform, new Vector3(0f, 0f, 0.13f),
                new Vector3(0.14f, 0.14f, 0.12f), shuttleCork);
            CreateBlock("White Band", root.transform, new Vector3(0f, 0f, 0.045f),
                new Vector3(0.16f, 0.16f, 0.055f), shuttleWhite);
            CreateBlock("Feather Core", root.transform, new Vector3(0f, 0f, -0.055f),
                new Vector3(0.1f, 0.1f, 0.16f), shuttleWhite);
            CreateBlock("Feather Top", root.transform, new Vector3(0f, 0.09f, -0.16f),
                new Vector3(0.08f, 0.13f, 0.18f), shuttleWhite);
            CreateBlock("Feather Bottom", root.transform, new Vector3(0f, -0.09f, -0.16f),
                new Vector3(0.08f, 0.13f, 0.18f), shuttleWhite);
            CreateBlock("Feather Left", root.transform, new Vector3(-0.09f, 0f, -0.16f),
                new Vector3(0.13f, 0.08f, 0.18f), shuttleWhite);
            CreateBlock("Feather Right", root.transform, new Vector3(0.09f, 0f, -0.16f),
                new Vector3(0.13f, 0.08f, 0.18f), shuttleWhite);

            GameObject trailAnchor = new GameObject("Trail");
            trailAnchor.transform.SetParent(root.transform, false);
            trailAnchor.transform.localPosition = new Vector3(0f, 0f, -0.25f);
            shuttleTrail = trailAnchor.AddComponent<TrailRenderer>();
            shuttleTrail.time = 0.42f;
            shuttleTrail.minVertexDistance = 0.035f;
            shuttleTrail.startWidth = 0.085f;
            shuttleTrail.endWidth = 0f;
            shuttleTrail.material = trailMaterial;
            shuttleTrail.startColor = new Color(1f, 0.95f, 0.55f, 0.75f);
            shuttleTrail.endColor = new Color(1f, 1f, 1f, 0f);
            shuttleTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shuttleTrail.receiveShadows = false;
            root.transform.localScale = Vector3.one * 0.9f;
            return root;
        }

        private GameObject CreateLandingMarker()
        {
            GameObject marker = new GameObject("Landing Marker");
            marker.transform.SetParent(transform, false);

            MeshFilter meshFilter = marker.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = marker.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = CreateRingMesh(0.42f, 0.06f, 48);
            meshRenderer.sharedMaterial = markerYellow;

            GameObject center = new GameObject("Landing Center");
            center.transform.SetParent(marker.transform, false);
            center.transform.localPosition = new Vector3(0f, 0.002f, 0f);
            MeshFilter centerFilter = center.AddComponent<MeshFilter>();
            MeshRenderer centerRenderer = center.AddComponent<MeshRenderer>();
            centerFilter.sharedMesh = CreateDiscMesh(0.1f, 24);
            centerRenderer.sharedMaterial = markerYellow;
            return marker;
        }

        private GameObject CreatePlayerPositionMarker()
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Player Position Marker";
            marker.transform.SetParent(transform, false);
            marker.transform.localScale = new Vector3(0.55f, 0.025f, 0.55f);
            marker.GetComponent<MeshRenderer>().sharedMaterial = playerPositionMaterial;
            Destroy(marker.GetComponent<Collider>());
            return marker;
        }

        private static void CreateBlock(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localScale = localScale;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            Destroy(block.GetComponent<Collider>());
        }

        private static Mesh CreateRingMesh(float radius, float thickness, int segments)
        {
            Vector3[] vertices = new Vector3[segments * 2];
            int[] triangles = new int[segments * 6];
            float innerRadius = radius - thickness;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = radial * innerRadius;
                vertices[i * 2 + 1] = radial * radius;
                int next = (i + 1) % segments;
                int triangle = i * 6;
                triangles[triangle] = i * 2;
                triangles[triangle + 1] = next * 2 + 1;
                triangles[triangle + 2] = i * 2 + 1;
                triangles[triangle + 3] = i * 2;
                triangles[triangle + 4] = next * 2;
                triangles[triangle + 5] = next * 2 + 1;
            }

            Mesh mesh = new Mesh { name = "Landing Marker Ring" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateDiscMesh(float radius, int segments)
        {
            Vector3[] vertices = new Vector3[segments + 1];
            int[] triangles = new int[segments * 3];

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                int next = (i + 1) % segments;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = next + 1;
                triangles[i * 3 + 2] = i + 1;
            }

            Mesh mesh = new Mesh { name = "Landing Marker Center" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void CreateMaterials()
        {
            Shader shader = Shader.Find("Standard");
            shuttleWhite = CreateRuntimeMaterial(shader, "Shuttle White", new Color(0.96f, 0.97f, 0.94f));
            shuttleCork = CreateRuntimeMaterial(shader, "Shuttle Cork", new Color(0.93f, 0.88f, 0.72f));
            markerYellow = CreateRuntimeMaterial(shader, "Landing Marker Yellow", new Color(1f, 0.78f, 0.04f));
            playerPositionMaterial = CreateRuntimeMaterial(
                shader,
                "Player Position Cyan",
                new Color(0.05f, 0.85f, 0.9f));
            racketRed = CreateRuntimeMaterial(shader, "Racket Red", new Color(0.82f, 0.08f, 0.1f));
            racketBlue = CreateRuntimeMaterial(shader, "Opponent Racket Blue", new Color(0.08f, 0.32f, 0.85f));
            racketDark = CreateRuntimeMaterial(shader, "Racket Grip", new Color(0.05f, 0.06f, 0.08f));
            racketString = CreateRuntimeMaterial(shader, "Racket Strings", new Color(0.92f, 0.94f, 0.9f));

            Shader trailShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (trailShader == null)
            {
                trailShader = Shader.Find("Sprites/Default");
            }

            trailMaterial = CreateRuntimeMaterial(trailShader, "Shuttle Trail", Color.white);
            trailMaterial.renderQueue = 3000;
        }

        private static Material CreateRuntimeMaterial(Shader shader, string name, Color color)
        {
            return new Material(shader)
            {
                name = name,
                color = color
            };
        }
    }
}
