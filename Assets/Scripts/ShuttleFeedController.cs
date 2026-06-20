using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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

        private enum OpponentSwingStyle
        {
            ForehandOverhead,
            ForehandDrop,
            BackhandOverhead,
            Net,
            Lift,
            SmashDefense,
            JumpSmash
        }

        private enum GameMode
        {
            SinglePlayer,
            Multiplayer
        }

        private enum ScreenState
        {
            MainMenu,
            ContinueOrNew,
            NewGameSetup,
            Tutorial,
            Playing
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
        [SerializeField] private float clearDuration = 2.35f;
        [SerializeField] private float dropShotArcHeight = 1.65f;
        [SerializeField] private float clearArcHeight = 4.85f;
        [SerializeField, Range(0.4f, 1f)] private float speedAfterNet = 0.68f;
        [SerializeField, Range(0.4f, 1f)] private float opponentSmashSpeedBeforeNet = 0.7f;
        [SerializeField, Range(0.1f, 0.8f)] private float opponentSmashSpeedAfterNet = 0.25f;

        [Header("Hit Assist")]
        [SerializeField] private float backcourtPositionInset = 0.45f;
        [SerializeField] private float racketXAlignmentTolerance = 0.6f;
        [SerializeField] private float contactWindow = 0.58f;
        [SerializeField] private float racketFollowSpeed = 14f;
        [SerializeField] private float racketMoveSpeed = 4.8f;
        [SerializeField] private float jumpHeight = 0.85f;
        [SerializeField] private float jumpDuration = 1.05f;
        [SerializeField, Range(0.1f, 0.5f)] private float jumpHangFraction = 0.3f;

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
        // Keep enabled while tuning sensor-mode hit detection; logs are gated to sensor input only.
        [SerializeField] private bool logSensorHitDebug = true;

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
        private Transform trajectoryGuide;
        private Transform apexProjection;
        private Transform racketCenterGuide;
        private Transform racket;
        private Transform racketFace;
        private Transform playerMarker;
        private Transform opponentPlayer;
        private Transform opponentBody;
        private Transform opponentRacket;
        private Transform opponentRacketFace;
        private TrailRenderer shuttleTrail;

        private Material shuttleWhite;
        private Material shuttleCork;
        private Material markerYellow;
        private Material playerPositionMaterial;
        private Material trajectoryMaterial;
        private Material trailMaterial;
        private Material racketRed;
        private Material racketBlue;
        private Material racketDark;
        private Material racketString;

        private Vector3 lastMousePosition;
        private bool shuttleIncoming;
        private bool incomingFrontCourt;
        private bool incomingHighClear;
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
        private int difficultyLevel;
        private float opponentSmashChance;
        private int playerScore;
        private int opponentScore;
        private int scoreTarget = 21;
        private int scoreCap = 30;
        private int rallyWinner;
        private int matchWinner;
        private bool matchOver;
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
        private int sensorHitLogSequence;
        private Camera gameplayCamera;
        private const int SwitchCameraPresetVersion = 10;
        private float playerServeSide = 1f;
        private bool settingsOpen;
        private bool resolutionOptionsOpen;
        private bool isPaused;
        private bool showRacketCenterGuide;
        private ScreenState screenState = ScreenState.MainMenu;
        private bool hasSavedMatch;
        private bool awaitingOpponentServe;
        private bool opponentServeReady;
        private bool netFaultTriggered;
        private int currentFlightHitter;
        private bool jumpActive;
        private float jumpElapsed;
        private float jumpOffset;
        private bool hasPlayerContactPrediction;
        private Vector3 playerPredictedContactPoint;
        private bool temporarySlowMotionArmed;
        private bool temporarySlowMotionActive;
        private bool temporarySlowMotionEnabled = true;

        private void Awake()
        {
            ApplySwitchCameraPreset();
            CreateMaterials();
            shuttle = CreateShuttlecock().transform;
            landingMarker = CreateLandingMarker().transform;
            playerPositionMarker = CreatePlayerPositionMarker().transform;
            trajectoryGuide = CreateTrajectoryGuide().transform;
            racket = CreatePixelRacket().transform;
            racketCenterGuide = CreateRacketCenterGuide().transform;
            playerMarker = CreatePlayerMarker().transform;
            opponentPlayer = CreateOpponentPlayer().transform;
            opponentRacket = CreateOpponentRacket(opponentPlayer).transform;
            racketRestRotation = Quaternion.Euler(12f, 0f, -8f);
            racket.rotation = racketRestRotation;
            playerGroundPosition = new Vector3(-0.15f, 0.55f, -2.7f * CourtLengthScale);

            shuttle.gameObject.SetActive(false);
            landingMarker.gameObject.SetActive(false);
            playerPositionMarker.gameObject.SetActive(false);
            trajectoryGuide.gameObject.SetActive(false);
            racketCenterGuide.gameObject.SetActive(false);
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
            ConfigureDifficulty(0);
            Time.timeScale = 0f;
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

                if (temporarySlowMotionActive)
                {
                    temporarySlowMotionActive = false;
                    temporarySlowMotionArmed = false;
                    Time.timeScale = 1f;
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

                matchWinner = GetMatchWinner();
                if (matchWinner != 0)
                {
                    matchOver = true;
                    yield break;
                }

                yield return new WaitForSeconds(delayBetweenFeeds);
            }
        }

        private int GetMatchWinner()
        {
            int highestScore = Mathf.Max(playerScore, opponentScore);
            int lead = Mathf.Abs(playerScore - opponentScore);
            bool reachedCap = highestScore >= scoreCap;
            bool wonByTwo = highestScore >= scoreTarget && lead >= 2;
            if (!reachedCap && !wonByTwo)
            {
                return 0;
            }

            return playerScore > opponentScore ? 1 : 2;
        }

        private void Update()
        {
            if (screenState != ScreenState.Playing)
            {
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                SetPaused(!isPaused);
            }

            if (isPaused)
            {
                return;
            }

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

            if (inputSnapshot.SmashReceiveReady || UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                if (awaitingOpponentServe)
                {
                    opponentServeReady = true;
                }
                else if (incomingOpponentSmash)
                {
                    smashReceiveReady = true;
                }
                else if (incomingHighClear && !jumpActive)
                {
                    jumpActive = true;
                    jumpElapsed = 0f;
                }
            }

            UpdateJump();
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
            pendingStartAngle = inputSnapshot.SwingStartAngle;
            pendingSwingTime = contactWindow;
            pendingSwingStartedAt = Time.time;
            swingCooldown = 0.22f;
            LogSensorHitDebug(
                "swing_accept",
                $"acceptedSpeed={F(speed)}|acceptedUp={B(resolvedSwingUpward)}|startAngle={F(inputSnapshot.SwingStartAngle)}");
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

        // Pipe-delimited logs are easier to grep and compare across rallies in Editor.log.
        private void LogSensorHitDebug(string eventName, string details = "")
        {
            if (!logSensorHitDebug || inputMode != BadmintonInputMode.Sensor)
            {
                return;
            }

            string suffix = string.IsNullOrEmpty(details) ? string.Empty : $"|{details}";
            Debug.Log(
                $"VRB_SENSOR_HIT|seq={++sensorHitLogSequence}|event={eventName}|" +
                $"{SensorHitSnapshotFields()}{suffix}");
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
                $"|playerStale={B(inputSnapshot.PlayerStale)}" +
                $"|racketStale={B(inputSnapshot.RacketStale)}" +
                $"|shuttleActive={B(shuttle != null && shuttle.gameObject.activeSelf)}" +
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
                $"|pendingSpeed={F(pendingSwingSpeed)}" +
                $"|faceAngle={F(currentFaceAngle)}" +
                $"|rawEuler={V(inputSnapshot.Racket.RawEuler)}" +
                $"|racketPos={V(racketPosition)}" +
                $"|racketForward={V(racketForward)}" +
                $"|racketRight={V(racketRight)}" +
                $"|racketUp={V(racketUp)}" +
                $"|shuttlePos={V(shuttlePosition)}" +
                $"|shuttleVel={V(shuttleVelocity)}" +
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
                $"|contactPoint={V(result.ContactPoint)}";
        }

        // Field values stay single-token so simple grep/split tooling can parse them reliably.
        private static string LogToken(string value)
        {
            return string.IsNullOrEmpty(value)
                ? "none"
                : value.Replace(' ', '_');
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
            EnsureGuiStyles();
            if (screenState != ScreenState.Playing)
            {
                DrawFrontend();
                return;
            }

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

            if (!isPaused)
            {
                DrawPauseButton();
            }

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
                $"Opponent Stamina  {Mathf.CeilToInt(opponentStamina)}/{Mathf.CeilToInt(opponentMaxStamina)}",
                uiLabelStyle);
            GUI.Label(
                new Rect(18f, 76f, 300f, 24f),
                $"{GetModeLabel()}   N{difficultyLevel}   {scoreTarget} Points",
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

            if (awaitingOpponentServe)
            {
                GUI.color = new Color(1f, 0.86f, 0.25f, 1f);
                GUI.Label(
                    new Rect(Screen.width * 0.5f - 180f, 62f, 360f, 30f),
                    "PRESS SPACE TO START SERVE",
                    uiLabelStyle);
            }

            if (matchOver)
            {
                DrawMatchEnd();
            }

            DrawTemporarySlowMotionToggle();

            if (awaitingPlayerServe)
            {
                GUI.color = new Color(1f, 0.86f, 0.25f, 1f);
                GUI.Label(
                    new Rect(Screen.width * 0.5f - 150f, 62f, 300f, 30f),
                    "YOUR SERVE - SWING UP",
                    uiLabelStyle);
            }

            if (isPaused)
            {
                DrawPauseMenu();
                if (settingsOpen)
                {
                    DrawSettings(false);
                }
            }

            GUI.color = previousColor;
        }

        private void DrawMatchEnd()
        {
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter
            };

            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.Box(
                new Rect(Screen.width * 0.5f - 190f, Screen.height * 0.5f - 80f, 380f, 160f),
                GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(Screen.width * 0.5f - 170f, Screen.height * 0.5f - 60f, 340f, 42f),
                matchWinner == 1 ? "YOU WIN" : "OPPONENT WINS",
                new GUIStyle(uiLabelStyle) { fontSize = 24 });
            if (GUI.Button(
                new Rect(Screen.width * 0.5f - 135f, Screen.height * 0.5f - 5f, 270f, 36f),
                "Restart Same Settings",
                buttonStyle))
            {
                RestartMatch();
            }
            if (GUI.Button(
                new Rect(Screen.width * 0.5f - 135f, Screen.height * 0.5f + 40f, 270f, 32f),
                "Main Menu",
                buttonStyle))
            {
                ReturnToMainMenu();
            }
        }

        private void DrawSettings(bool showToggle = true)
        {
            if (showToggle && !settingsOpen)
            {
                return;
            }

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };

            GUI.color = new Color(0.03f, 0.04f, 0.05f, 0.88f);
            float panelHeight = resolutionOptionsOpen ? 286f : 152f;
            GUI.Box(new Rect(Screen.width - 232f, 56f, 214f, panelHeight), GUIContent.none);
            GUI.color = Color.white;

            bool fullscreen = Screen.fullScreenMode != FullScreenMode.Windowed;
            if (GUI.Button(
                new Rect(Screen.width - 218f, 70f, 186f, 28f),
                fullscreen ? "Fullscreen: ON" : "Fullscreen: OFF",
                buttonStyle))
            {
                if (fullscreen)
                {
                    Screen.fullScreenMode = FullScreenMode.Windowed;
                    Screen.fullScreen = false;
                }
                else
                {
                    Resolution nativeResolution = Screen.currentResolution;
                    Screen.SetResolution(
                        nativeResolution.width,
                        nativeResolution.height,
                        FullScreenMode.FullScreenWindow,
                        nativeResolution.refreshRateRatio);
                    resolutionOptionsOpen = false;
                }
            }

            if (GUI.Button(
                new Rect(Screen.width - 218f, 106f, 186f, 28f),
                $"Resolution: {Screen.width} x {Screen.height}",
                buttonStyle))
            {
                resolutionOptionsOpen = !resolutionOptionsOpen;
            }

            float guideY = 142f;
            if (resolutionOptionsOpen)
            {
                string[] labels =
                {
                    "1280 x 720",
                    "1600 x 900",
                    "1920 x 1080",
                    "2560 x 1440 (2K)"
                };
                Vector2Int[] sizes =
                {
                    new Vector2Int(1280, 720),
                    new Vector2Int(1600, 900),
                    new Vector2Int(1920, 1080),
                    new Vector2Int(2560, 1440)
                };

                for (int i = 0; i < labels.Length; i++)
                {
                    float y = 142f + i * 30f;
                    if (GUI.Button(
                        new Rect(Screen.width - 218f, y, 186f, 25f),
                        labels[i],
                        buttonStyle))
                    {
                        SetWindowResolution(sizes[i].x, sizes[i].y);
                        resolutionOptionsOpen = false;
                    }
                }

                guideY = 266f;
            }

            if (GUI.Button(
                new Rect(Screen.width - 218f, guideY, 186f, 28f),
                showRacketCenterGuide ? "Guide Line: ON" : "Guide Line: OFF",
                buttonStyle))
            {
                showRacketCenterGuide = !showRacketCenterGuide;
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

        private void DrawPauseButton()
        {
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            if (GUI.Button(
                new Rect(Screen.width - 122f, 18f, 104f, 30f),
                "Pause",
                buttonStyle))
            {
                SetPaused(true);
            }
        }

        private void DrawTemporarySlowMotionToggle()
        {
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            if (GUI.Button(
                new Rect(18f, Screen.height - 48f, 166f, 30f),
                temporarySlowMotionEnabled
                    ? "Slow Motion 0.2x: ON"
                    : "Slow Motion 0.2x: OFF",
                buttonStyle))
            {
                temporarySlowMotionEnabled = !temporarySlowMotionEnabled;
                if (!temporarySlowMotionEnabled)
                {
                    temporarySlowMotionArmed = false;
                    temporarySlowMotionActive = false;
                    if (!isPaused)
                    {
                        Time.timeScale = 1f;
                    }
                }
            }
        }

        private static void SetWindowResolution(int width, int height)
        {
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
        }

        private void EnsureGuiStyles()
        {
            uiLabelStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = Color.white }
            };
        }

        private void DrawFrontend()
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(0.025f, 0.035f, 0.045f, 0.96f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

            float leftWidth = Mathf.Clamp(Screen.width * 0.42f, 420f, 680f);
            GUI.color = new Color(0.04f, 0.06f, 0.075f, 1f);
            GUI.DrawTexture(new Rect(0f, 0f, leftWidth, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle titleStyle = new GUIStyle(uiLabelStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 42,
                fontStyle = FontStyle.Bold
            };
            GUIStyle menuButton = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                padding = new RectOffset(16, 16, 8, 8)
            };

            GUI.Label(new Rect(54f, 70f, leftWidth - 90f, 70f), "VR BADMINTON", titleStyle);

            float menuButtonWidth = Mathf.Min(340f, leftWidth - 108f);
            float menuButtonX = (leftWidth - menuButtonWidth) * 0.5f;
            if (GUI.Button(new Rect(menuButtonX, 190f, menuButtonWidth, 46f), "Start Tutorial", menuButton))
            {
                screenState = ScreenState.Tutorial;
            }

            if (GUI.Button(new Rect(menuButtonX, 250f, menuButtonWidth, 46f), "Settings", menuButton))
            {
                settingsOpen = !settingsOpen;
            }

            if (GUI.Button(new Rect(menuButtonX, 310f, menuButtonWidth, 46f), "Quit Game", menuButton))
            {
                QuitGame();
            }

            if (screenState == ScreenState.MainMenu)
            {
                float buttonX = leftWidth + 60f;
                float buttonWidth = Mathf.Max(260f, Screen.width - buttonX - 70f);
                if (GUI.Button(
                    new Rect(buttonX, Screen.height * 0.32f, buttonWidth, Screen.height * 0.34f),
                    "ENTER MATCH",
                    new GUIStyle(GUI.skin.button) { fontSize = 30 }))
                {
                    screenState = ScreenState.ContinueOrNew;
                }
            }
            else if (screenState == ScreenState.ContinueOrNew)
            {
                DrawContinueOrNew(leftWidth);
            }
            else if (screenState == ScreenState.NewGameSetup)
            {
                DrawNewGameSetup(leftWidth);
            }
            else if (screenState == ScreenState.Tutorial)
            {
                DrawTutorialPlaceholder(leftWidth);
            }

            if (settingsOpen)
            {
                DrawSettings(false);
            }

            GUI.color = previousColor;
        }

        private void DrawContinueOrNew(float leftWidth)
        {
            float x = leftWidth + 70f;
            float width = Mathf.Max(300f, Screen.width - x - 80f);
            GUI.Label(
                new Rect(x, 120f, width, 50f),
                "MATCH",
                new GUIStyle(uiLabelStyle) { fontSize = 28 });

            GUI.enabled = hasSavedMatch;
            if (GUI.Button(new Rect(x, 210f, width, 58f), "Continue Match"))
            {
                ContinueMatch();
            }
            GUI.enabled = true;

            if (GUI.Button(new Rect(x, 286f, width, 58f), "New Match"))
            {
                screenState = ScreenState.NewGameSetup;
            }

            if (GUI.Button(new Rect(x, 362f, width, 42f), "Back"))
            {
                screenState = ScreenState.MainMenu;
            }
        }

        private void DrawNewGameSetup(float leftWidth)
        {
            float x = leftWidth + 70f;
            float width = Mathf.Max(340f, Screen.width - x - 80f);
            GUI.Label(new Rect(x, 70f, width, 44f), "NEW MATCH", new GUIStyle(uiLabelStyle) { fontSize = 28 });

            GUI.Label(new Rect(x, 130f, width, 28f), "Mode", uiLabelStyle);
            GUI.color = gameMode == GameMode.SinglePlayer
                ? new Color(1f, 0.82f, 0.22f, 1f)
                : Color.white;
            if (GUI.Button(new Rect(x, 165f, width * 0.48f, 40f), "Single Player"))
            {
                gameMode = GameMode.SinglePlayer;
            }
            GUI.color = gameMode == GameMode.Multiplayer
                ? new Color(1f, 0.82f, 0.22f, 1f)
                : Color.white;
            if (GUI.Button(new Rect(x + width * 0.52f, 165f, width * 0.48f, 40f), "Online (Coming Soon)"))
            {
                gameMode = GameMode.Multiplayer;
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(x, 225f, width, 28f), "Difficulty", uiLabelStyle);
            const int difficultyCount = 6;
            for (int i = 0; i < difficultyCount; i++)
            {
                GUI.color = i == difficultyLevel ? new Color(1f, 0.82f, 0.22f, 1f) : Color.white;
                if (GUI.Button(
                    new Rect(
                        x + i * (width / difficultyCount),
                        260f,
                        width / difficultyCount - 7f,
                        38f),
                    $"N{i}"))
                {
                    ConfigureDifficulty(i);
                }
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(x, 320f, width, 28f), "Score Format", uiLabelStyle);
            GUI.color = scoreTarget == 15
                ? new Color(1f, 0.82f, 0.22f, 1f)
                : Color.white;
            if (GUI.Button(new Rect(x, 355f, width * 0.48f, 40f), "15 Points"))
            {
                scoreTarget = 15;
                scoreCap = 21;
            }
            GUI.color = scoreTarget == 21
                ? new Color(1f, 0.82f, 0.22f, 1f)
                : Color.white;
            if (GUI.Button(new Rect(x + width * 0.52f, 355f, width * 0.48f, 40f), "21 Points"))
            {
                scoreTarget = 21;
                scoreCap = 30;
            }

            GUI.color = Color.white;
            GUI.enabled = gameMode == GameMode.SinglePlayer;
            if (GUI.Button(new Rect(x, 430f, width, 54f), "START NEW MATCH"))
            {
                StartNewMatch();
            }
            GUI.enabled = true;

            if (GUI.Button(new Rect(x, 500f, width, 40f), "Back"))
            {
                screenState = ScreenState.ContinueOrNew;
            }
        }

        private string GetModeLabel()
        {
            return gameMode == GameMode.SinglePlayer
                ? "Single Player"
                : "Online";
        }

        private void DrawTutorialPlaceholder(float leftWidth)
        {
            float x = leftWidth + 70f;
            float width = Mathf.Max(300f, Screen.width - x - 80f);
            GUI.Label(new Rect(x, 150f, width, 50f), "BEGINNER TUTORIAL", new GUIStyle(uiLabelStyle) { fontSize = 28 });
            GUI.Label(new Rect(x, 220f, width, 40f), "Coming soon", new GUIStyle(uiLabelStyle) { fontSize = 18 });
            if (GUI.Button(new Rect(x, 300f, width, 44f), "Back"))
            {
                screenState = ScreenState.MainMenu;
            }
        }

        private void DrawPauseMenu()
        {
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };

            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);

            float panelWidth = 320f;
            float panelHeight = 260f;
            float panelX = (Screen.width - panelWidth) * 0.5f;
            float panelY = (Screen.height - panelHeight) * 0.5f;

            GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.96f);
            GUI.Box(new Rect(panelX, panelY, panelWidth, panelHeight), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(panelX + 20f, panelY + 24f, panelWidth - 40f, 42f),
                "PAUSED",
                new GUIStyle(uiLabelStyle) { fontSize = 28 });

            if (GUI.Button(
                new Rect(panelX + 60f, panelY + 82f, panelWidth - 120f, 38f),
                "Continue",
                buttonStyle))
            {
                SetPaused(false);
            }

            if (GUI.Button(
                new Rect(panelX + 60f, panelY + 132f, panelWidth - 120f, 38f),
                "Settings",
                buttonStyle))
            {
                settingsOpen = !settingsOpen;
            }

            if (GUI.Button(
                new Rect(panelX + 60f, panelY + 182f, panelWidth - 120f, 38f),
                "Main Menu",
                buttonStyle))
            {
                ReturnToMainMenu();
            }
        }

        private void SetPaused(bool paused)
        {
            isPaused = paused;
            settingsOpen = false;
            Time.timeScale = isPaused
                ? 0f
                : temporarySlowMotionActive ? 0.2f : 1f;
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDisable()
        {
            Time.timeScale = 1f;
        }

        private void ConfigureDifficulty(int level)
        {
            difficultyLevel = Mathf.Clamp(level, 0, 5);
            switch (difficultyLevel)
            {
                case 0:
                    opponentMaxStamina = 100f;
                    opponentSmashChance = 0f;
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
                case 3:
                    opponentMaxStamina = 100f;
                    opponentSmashChance = 0.75f;
                    opponentSmashReceiveChance = 0.5f;
                    break;
                case 4:
                    opponentMaxStamina = 200f;
                    opponentSmashChance = 1f;
                    opponentSmashReceiveChance = 0.75f;
                    break;
                default:
                    opponentMaxStamina = 500f;
                    opponentSmashChance = 1f;
                    opponentSmashReceiveChance = 1f;
                    break;
            }

            opponentStamina = opponentMaxStamina;
        }

        private void StartNewMatch()
        {
            if (gameMode != GameMode.SinglePlayer)
            {
                return;
            }

            playerScore = 0;
            opponentScore = 0;
            playerServing = false;
            hasSavedMatch = true;
            BeginGameplay();
        }

        private void ContinueMatch()
        {
            if (!hasSavedMatch)
            {
                return;
            }

            BeginGameplay();
        }

        private void BeginGameplay()
        {
            StopAllCoroutines();
            screenState = ScreenState.Playing;
            isPaused = false;
            settingsOpen = false;
            Time.timeScale = 1f;
            rallyWinner = 0;
            matchWinner = 0;
            matchOver = false;
            ResetRallyVisuals();
            StartCoroutine(GameLoop());
        }

        private void ReturnToMainMenu()
        {
            hasSavedMatch = true;
            StopAllCoroutines();
            isPaused = false;
            settingsOpen = false;
            Time.timeScale = 0f;
            screenState = ScreenState.MainMenu;
            rallyWinner = 0;
            ResetRallyVisuals();
        }

        private void ResetRallyVisuals()
        {
            temporarySlowMotionArmed = false;
            temporarySlowMotionActive = false;
            if (!isPaused && screenState == ScreenState.Playing)
            {
                Time.timeScale = 1f;
            }
            shuttleIncoming = false;
            incomingHighClear = false;
            incomingOpponentSmash = false;
            smashReceiveReady = false;
            awaitingPlayerServe = false;
            awaitingOpponentServe = false;
            opponentServeReady = false;
            swingPending = false;
            pendingSwingStartedAt = 0f;
            hasPlayerContactPrediction = false;
            ClearHitHistory();
            shuttleTrail.emitting = false;
            shuttle.gameObject.SetActive(false);
            landingMarker.gameObject.SetActive(false);
            playerPositionMarker.gameObject.SetActive(false);
            trajectoryGuide.gameObject.SetActive(false);
            HideLandingPrediction();
            jumpActive = false;
            jumpOffset = 0f;
        }

        private void RestartMatch()
        {
            StopAllCoroutines();
            playerScore = 0;
            opponentScore = 0;
            rallyWinner = 0;
            matchWinner = 0;
            matchOver = false;
            playerServing = false;
            opponentStamina = opponentMaxStamina;
            ResetRallyVisuals();
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
                : opponentShot == OpponentShotType.Net ? 1.35f : dropShotArcHeight;

            awaitingOpponentServe = true;
            opponentServeReady = false;
            while (!opponentServeReady)
            {
                yield return null;
            }
            awaitingOpponentServe = false;

            yield return AnimateOpponentHit(start, sourceSide);
            start = opponentRacketFace.position;
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
            trajectoryGuide.gameObject.SetActive(false);
        }

        private IEnumerator PlayerServe()
        {
            currentFlightHitter = 1;
            netFaultTriggered = false;
            float serviceSide = playerScore % 2 == 1 ? -1f : 1f;
            playerServeSide = serviceSide;
            playerGroundPosition = new Vector3(
                serviceSide * 1.55f,
                0.55f,
                -2.05f * CourtLengthScale);

            Vector3 start = new Vector3(
                serviceSide * 1.55f,
                1.05f,
                -2.05f * CourtLengthScale);
            shuttle.position = GetServeContactPosition();
            shuttle.gameObject.SetActive(true);
            shuttleTrail.Clear();
            shuttleTrail.emitting = false;
            awaitingPlayerServe = true;
            playerServeGestureReady = false;
            swingPending = false;

            while (!playerServeGestureReady)
            {
                shuttle.position = GetServeContactPosition();
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
            float arcHeight = Mathf.Lerp(1.55f, 4.35f, power);

            SetTrailColors(
                new Color(1f, 0.9f, 0.42f, 0.82f),
                new Color(1f, 0.82f, 0.25f, 0f));
            shuttleTrail.Clear();
            shuttleTrail.emitting = true;

            Vector3 serveStart = shuttle.position;
            float progress = 0f;
            Vector3 previousPosition = serveStart;
            bool serveUsesClearArc = power >= 0.55f;
            float serveApexT = serveUsesClearArc ? 0.7f : 0.5f;
            const float opponentContactProgress = 0.86f;
            Vector3 contactPoint = EvaluateArc(
                serveStart,
                target,
                opponentContactProgress,
                arcHeight,
                serveApexT);
            float opponentSide = contactPoint.x >= 0f ? 1f : -1f;
            Vector3 readyPosition = GetOpponentReadyPosition(contactPoint, opponentSide);

            ShowLandingPrediction(serveStart, target, serveApexT, serveUsesClearArc);

            while (progress < opponentContactProgress)
            {
                progress = Mathf.Min(1f, progress + Time.deltaTime / duration);
                Vector3 position = EvaluateArc(
                    serveStart,
                    target,
                    progress,
                    arcHeight,
                    serveApexT);
                MoveShuttle(position, ref previousPosition);
                if (netFaultTriggered)
                {
                    yield return ResolveNetFault();
                    yield break;
                }

                MoveOpponentTowards(readyPosition, contactPoint, opponentSide);
                if (opponentStamina <= 0f)
                {
                    break;
                }

                yield return null;
            }

            HideLandingPrediction();
            if (opponentStamina <= 0f ||
                OpponentDistanceTo(readyPosition) > opponentReachTolerance)
            {
                while (progress < 1f)
                {
                    progress = Mathf.Min(1f, progress + Time.deltaTime / duration);
                    Vector3 position = EvaluateArc(
                        serveStart,
                        target,
                        progress,
                        arcHeight,
                        serveApexT);
                    MoveShuttle(position, ref previousPosition);
                    if (netFaultTriggered)
                    {
                        yield return ResolveNetFault();
                        yield break;
                    }
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
                bool backhand = ShouldOpponentUseBackhand(contactPoint);
                PrepareOpponentRacket(
                    contactPoint,
                    opponentSide,
                    backhand,
                    false);
                StartCoroutine(AnimateOpponentSwing(
                    opponentSide,
                    backhand,
                    returnShot,
                    false));
                Vector3 racketContact = opponentRacketFace.position;
                yield return OpponentReturn(racketContact, opponentSide, returnShot);
            }

            shuttleTrail.emitting = false;
            shuttle.gameObject.SetActive(false);
            landingMarker.gameObject.SetActive(false);
            playerPositionMarker.gameObject.SetActive(false);
        }

        private Vector3 GetServeContactPosition()
        {
            float racketSide = isBackhand ? -0.95f : 0.95f;
            Vector3 racketPivot = new Vector3(
                playerGroundPosition.x + racketSide,
                0.65f,
                playerGroundPosition.z);
            Vector3 swingPathDirection =
                Quaternion.Euler(-10f, 0f, 0f) * Vector3.up;
            return racketPivot + swingPathDirection * 1.22f;
        }

        private IEnumerator PlayIncomingShuttle(
            Vector3 start,
            Vector3 target,
            float duration,
            float arcHeight,
            bool isOpponentSmash)
        {
            currentFlightHitter = 2;
            netFaultTriggered = false;
            bool incomingUsesClearArc = arcHeight >= 3f;
            float incomingApexT = incomingUsesClearArc ? 0.7f : 0.5f;
            ShowLandingPrediction(start, target, incomingApexT, incomingUsesClearArc);
            incomingFrontCourt = Mathf.Abs(target.z) < 4f * CourtLengthScale;
            incomingHighClear = arcHeight >= 3f && !isOpponentSmash;
            hasPlayerContactPrediction = incomingHighClear;
            if (hasPlayerContactPrediction)
            {
                float contactProgress = FindDescendingContactProgress(
                    start,
                    target,
                    arcHeight,
                    incomingApexT,
                    2.45f);
                playerPredictedContactPoint = EvaluateArc(
                    start,
                    target,
                    contactProgress,
                    arcHeight,
                    incomingApexT);
            }
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
                Vector3 position = EvaluateArc(
                    start,
                    target,
                    progress,
                    arcHeight,
                    incomingApexT);
                MoveShuttle(position, ref previousPosition);
                if (netFaultTriggered)
                {
                    yield return ResolveNetFault();
                    yield break;
                }

                if (TryHitShuttle(out ShotType shot))
                {
                    shuttleIncoming = false;
                    hasPlayerContactPrediction = false;
                    HideLandingPrediction();
                    playerPositionMarker.gameObject.SetActive(false);
                    yield return ReturnShuttle(shot, previousPosition);
                    yield break;
                }

                yield return null;
            }

            shuttleIncoming = false;
            incomingHighClear = false;
            hasPlayerContactPrediction = false;
            incomingOpponentSmash = false;
            smashReceiveReady = false;
            shuttle.position = target;
            HideLandingPrediction();
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
            if (lastHitResult.Hit || lastHitResult.ConsumeSwing)
            {
                LogSensorHitDebug("hit_resolve", HitResultFields(lastHitResult));
            }

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

            if (shot == ShotType.Miss)
            {
                return false;
            }

            ResetPlayerBackhandAfterHit();
            return true;
        }

        private void ResetPlayerBackhandAfterHit()
        {
            if (isBackhand)
            {
                StartCoroutine(ReturnPlayerToForehand());
            }
        }

        private IEnumerator ReturnPlayerToForehand()
        {
            yield return new WaitForSeconds(0.16f);
            isBackhand = false;
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
            currentFlightHitter = 1;
            netFaultTriggered = false;
            temporarySlowMotionArmed =
                temporarySlowMotionEnabled &&
                shot == ShotType.Clear;
            Vector3 target;
            float duration;
            float arcHeight;
            SetTrailForShot(shot);

            switch (shot)
            {
                case ShotType.Drop:
                    target = new Vector3(Random.Range(-2.2f, 2.2f), 0.09f, 2.65f * CourtLengthScale);
                    duration = 1.1f;
                    arcHeight = 1.35f;
                    break;
                case ShotType.Clear:
                    target = new Vector3(Random.Range(-2.3f, 2.3f), 0.09f, 6.2f * CourtLengthScale);
                    duration = 2f;
                    arcHeight = 4.5f;
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
                    arcHeight = 1.35f;
                    break;
            }

            bool returnUsesClearArc = shot == ShotType.Clear;
            float returnApexT = returnUsesClearArc ? 0.7f : 0.5f;
            ShowLandingPrediction(start, target, returnApexT, returnUsesClearArc);

            float progress = 0f;
            Vector3 previousPosition = start;
            float opponentContactProgress = shot == ShotType.Clear
                ? FindDescendingContactProgress(
                    start,
                    target,
                    arcHeight,
                    returnApexT,
                    2.55f)
                : 0.86f;
            Vector3 opponentContactPoint = EvaluateArc(
                start,
                target,
                opponentContactProgress,
                arcHeight,
                returnApexT);
            float opponentSide = opponentContactPoint.x >= 0f ? 1f : -1f;
            Vector3 opponentReadyPosition = GetOpponentReadyPosition(
                opponentContactPoint,
                opponentSide);
            bool plannedForehandOverhead = false;
            bool plannedOverheadBackhand = false;
            OpponentShotType plannedOpponentShot = OpponentShotType.Net;
            if (shot == ShotType.Clear)
            {
                plannedOpponentShot = ChooseOpponentShot(
                    true,
                    Mathf.Abs(opponentContactPoint.z) < 4f * CourtLengthScale);
                plannedForehandOverhead =
                    plannedOpponentShot == OpponentShotType.Clear ||
                    (difficultyLevel == 0 &&
                        plannedOpponentShot == OpponentShotType.Drop);
                plannedOverheadBackhand =
                    ShouldOpponentUseBackhand(opponentContactPoint);
            }

            while (progress < opponentContactProgress)
            {
                progress = Mathf.Min(1f, progress + Time.deltaTime / duration);
                Vector3 position = EvaluateArc(
                    start,
                    target,
                    progress,
                    arcHeight,
                    returnApexT);
                MoveShuttle(position, ref previousPosition);
                if (netFaultTriggered)
                {
                    yield return ResolveNetFault();
                    yield break;
                }
                if (plannedForehandOverhead && !plannedOverheadBackhand)
                {
                    UpdateOpponentForehandClearPreparation(
                        progress / opponentContactProgress,
                        opponentReadyPosition,
                        opponentContactPoint);
                }
                else
                {
                    MoveOpponentTowards(
                        opponentReadyPosition,
                        opponentContactPoint,
                        opponentSide);
                }
                if (opponentStamina <= 0f)
                {
                    break;
                }

                yield return null;
            }

            HideLandingPrediction();
            bool opponentReached =
                opponentStamina > 0f &&
                OpponentDistanceTo(opponentReadyPosition) <= opponentReachTolerance;
            if (!opponentReached)
            {
                while (progress < 1f)
                {
                    progress = Mathf.Min(1f, progress + Time.deltaTime / duration);
                    Vector3 position = EvaluateArc(
                        start,
                        target,
                        progress,
                        arcHeight,
                        returnApexT);
                    MoveShuttle(position, ref previousPosition);
                    if (netFaultTriggered)
                    {
                        yield return ResolveNetFault();
                        yield break;
                    }
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
                    Vector3 position = EvaluateArc(
                        start,
                        target,
                        progress,
                        arcHeight,
                        returnApexT);
                    MoveShuttle(position, ref previousPosition);
                    if (netFaultTriggered)
                    {
                        yield return ResolveNetFault();
                        yield break;
                    }
                    yield return null;
                }

                rallyWinner = 1;
                yield return new WaitForSeconds(0.45f);
                yield break;
            }

            OpponentShotType opponentShot = shot == ShotType.Clear
                ? plannedOpponentShot
                : ChooseOpponentShot(
                    false,
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

            bool useBackhand = ShouldOpponentUseBackhand(opponentContactPoint);
            bool jumpSmash = opponentShot == OpponentShotType.Smash;
            PrepareOpponentRacket(
                opponentContactPoint,
                opponentSide,
                useBackhand,
                jumpSmash);
            bool forehandClearPrepared =
                plannedForehandOverhead &&
                !plannedOverheadBackhand;
            if (forehandClearPrepared)
            {
                AlignOpponentRacketFace(
                    opponentContactPoint,
                    Quaternion.Euler(0f, 180f, 0f));
                opponentBody.localRotation = Quaternion.identity;
            }
            StartCoroutine(AnimateOpponentSwing(
                opponentSide,
                useBackhand,
                opponentShot,
                shot == ShotType.Smash,
                forehandClearPrepared));
            Vector3 contactStart = opponentRacketFace.position;
            yield return OpponentReturn(contactStart, opponentSide, opponentShot);
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
                    arcHeight = 1.35f;
                    SetTrailColors(
                        new Color(0.25f, 1f, 0.35f, 0.82f),
                        new Color(0.2f, 0.8f, 0.25f, 0f));
                    break;
                case OpponentShotType.Drop:
                    targetDepth = Random.Range(2.7f, 3.35f);
                    duration = 1.35f;
                    arcHeight = 1.65f;
                    SetTrailColors(
                        new Color(0.25f, 1f, 0.35f, 0.82f),
                        new Color(0.2f, 0.8f, 0.25f, 0f));
                    break;
                case OpponentShotType.Smash:
                    targetDepth = Random.Range(3.4f, 5.2f);
                    duration = 0.85f;
                    arcHeight = 0.32f;
                    SetTrailColors(
                        new Color(1f, 0.12f, 0.08f, 0.9f),
                        new Color(0.85f, 0.02f, 0.02f, 0f));
                    break;
                default:
                    targetDepth = Random.Range(5.98f, 6.58f);
                    duration = shot == OpponentShotType.Lift ? 2.4f : 2.25f;
                    arcHeight = shot == OpponentShotType.Lift ? 5.25f : 4.85f;
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
            if (difficultyLevel == 0)
            {
                return fromFrontCourt
                    ? OpponentShotType.Net
                    : OpponentShotType.Drop;
            }

            if (canSmash && opponentStamina >= 10f && Random.value < opponentSmashChance)
            {
                return OpponentShotType.Smash;
            }

            if (fromFrontCourt && opponentStamina >= 3f)
            {
                return Random.value < 0.58f
                    ? OpponentShotType.Lift
                    : OpponentShotType.Net;
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
            while (OpponentDistanceTo(readyPosition) > opponentReachTolerance)
            {
                if (opponentStamina <= 0f)
                {
                    yield break;
                }

                MoveOpponentTowards(readyPosition, contactPoint, sourceSide);
                yield return null;
            }

            bool backhand = ShouldOpponentUseBackhand(contactPoint);
            PrepareOpponentRacket(contactPoint, sourceSide, backhand, false);
            yield return AnimateOpponentSwing(
                sourceSide,
                backhand,
                OpponentShotType.Lift,
                false);
        }

        private IEnumerator AnimateOpponentSwing(
            float sourceSide,
            bool backhand,
            OpponentShotType shot,
            bool receivingSmash,
            bool forehandClearPrepared = false)
        {
            OpponentSwingStyle style = GetOpponentSwingStyle(
                shot,
                backhand,
                receivingSmash);
            if (style == OpponentSwingStyle.ForehandOverhead)
            {
                yield return AnimateOpponentForehandClear(forehandClearPrepared);
                yield break;
            }

            float handSide = backhand ? 1f : -1f;
            Quaternion bodyNeutral = Quaternion.Euler(0f, -10f, 2f);
            Quaternion bodyTurned = Quaternion.Euler(
                0f,
                backhand ? 145f : 24f,
                backhand ? -10f : 5f);
            Vector3 neutralPosition = new Vector3(-0.72f, 0.72f, -0.02f);
            Quaternion neutralRotation = Quaternion.Euler(18f, 180f, 8f);

            Vector3 preparationPosition;
            Vector3 contactPosition;
            Vector3 followPosition;
            Quaternion preparationRotation;
            Quaternion contactRotation;
            Quaternion followRotation;
            float preparationDuration;
            float strikeDuration;
            bool jump = false;

            switch (style)
            {
                case OpponentSwingStyle.BackhandOverhead:
                    // Rear draw: vertical face, horizontal shaft, elbow leading.
                    preparationPosition = new Vector3(handSide * 0.58f, 1.05f, 0.34f);
                    contactPosition = new Vector3(handSide * 0.82f, 1.58f, -0.08f);
                    followPosition = new Vector3(handSide * 0.52f, 1.15f, -0.34f);
                    preparationRotation = Quaternion.Euler(0f, 92f, 90f);
                    contactRotation = Quaternion.Euler(-4f, 0f, 2f);
                    followRotation = Quaternion.Euler(-62f, -8f, -18f);
                    preparationDuration = 0.18f;
                    strikeDuration = 0.2f;
                    break;

                case OpponentSwingStyle.Net:
                    // Flat face carried gently through the shuttle.
                    preparationPosition = new Vector3(handSide * 0.96f, 0.62f, -0.18f);
                    contactPosition = new Vector3(handSide * 1.08f, 0.7f, -0.42f);
                    followPosition = new Vector3(handSide * 1.04f, 0.74f, -0.52f);
                    preparationRotation = Quaternion.Euler(88f, backhand ? 0f : 180f, 0f);
                    contactRotation = Quaternion.Euler(82f, backhand ? 0f : 180f, 0f);
                    followRotation = Quaternion.Euler(76f, backhand ? 0f : 180f, 0f);
                    preparationDuration = 0.12f;
                    strikeDuration = 0.15f;
                    break;

                case OpponentSwingStyle.Lift:
                    // Drop the racket head, then accelerate upward through contact.
                    preparationPosition = new Vector3(handSide * 0.82f, 0.16f, 0.12f);
                    contactPosition = new Vector3(handSide * 0.95f, 0.72f, -0.3f);
                    followPosition = new Vector3(handSide * 0.72f, 1.22f, -0.38f);
                    preparationRotation = Quaternion.Euler(112f, backhand ? 0f : 180f, 8f);
                    contactRotation = Quaternion.Euler(18f, backhand ? 0f : 180f, 2f);
                    followRotation = Quaternion.Euler(-48f, backhand ? 0f : 180f, -8f);
                    preparationDuration = 0.16f;
                    strikeDuration = 0.2f;
                    break;

                case OpponentSwingStyle.SmashDefense:
                    // Block by extending the face to the incoming side.
                    preparationPosition = new Vector3(handSide * 0.78f, 0.72f, -0.08f);
                    contactPosition = new Vector3(handSide * 1.3f, 0.78f, -0.28f);
                    followPosition = new Vector3(handSide * 1.18f, 0.82f, -0.36f);
                    preparationRotation = Quaternion.Euler(6f, backhand ? 0f : 180f, 4f);
                    contactRotation = Quaternion.Euler(-4f, backhand ? 0f : 180f, 0f);
                    followRotation = Quaternion.Euler(-16f, backhand ? 0f : 180f, -4f);
                    preparationDuration = 0.09f;
                    strikeDuration = 0.12f;
                    break;

                case OpponentSwingStyle.JumpSmash:
                    preparationPosition = new Vector3(handSide * 0.48f, 1.28f, 0.3f);
                    contactPosition = new Vector3(handSide * 0.66f, 1.72f, -0.16f);
                    followPosition = new Vector3(handSide * 0.34f, 0.94f, -0.42f);
                    preparationRotation = Quaternion.Euler(-42f, backhand ? 0f : 180f, 18f);
                    contactRotation = Quaternion.Euler(-126f, backhand ? 0f : 180f, 4f);
                    followRotation = Quaternion.Euler(-158f, backhand ? 0f : 180f, -12f);
                    preparationDuration = 0.17f;
                    strikeDuration = 0.16f;
                    jump = true;
                    break;

                case OpponentSwingStyle.ForehandDrop:
                    preparationPosition = new Vector3(handSide * 0.48f, 1.24f, 0.3f);
                    contactPosition = new Vector3(handSide * 0.68f, 1.62f, -0.06f);
                    followPosition = new Vector3(handSide * 0.54f, 1.28f, -0.22f);
                    preparationRotation = Quaternion.Euler(-26f, 180f, 18f);
                    contactRotation = Quaternion.Euler(-76f, 180f, 4f);
                    followRotation = Quaternion.Euler(-96f, 180f, -6f);
                    preparationDuration = 0.16f;
                    strikeDuration = 0.16f;
                    break;

                default:
                    // Raise the hand, draw behind the shoulder, then contact at full reach.
                    preparationPosition = new Vector3(handSide * 0.46f, 1.28f, 0.34f);
                    contactPosition = new Vector3(handSide * 0.68f, 1.68f, -0.08f);
                    followPosition = new Vector3(handSide * 0.38f, 1.02f, -0.36f);
                    preparationRotation = Quaternion.Euler(-28f, 180f, 20f);
                    contactRotation = Quaternion.Euler(-92f, 180f, 4f);
                    followRotation = Quaternion.Euler(-132f, 180f, -14f);
                    preparationDuration = 0.18f;
                    strikeDuration = 0.18f;
                    break;
            }

            yield return AnimateOpponentPose(
                preparationPosition,
                preparationRotation,
                bodyTurned,
                preparationDuration,
                false,
                0f);
            yield return AnimateOpponentPose(
                contactPosition,
                contactRotation,
                bodyTurned,
                strikeDuration,
                jump,
                1f);
            yield return AnimateOpponentPose(
                followPosition,
                followRotation,
                bodyTurned,
                0.12f,
                jump,
                0.35f);

            Vector3 groundedPosition = opponentPlayer.position;
            groundedPosition.y = 0.55f;
            opponentPlayer.position = groundedPosition;
            yield return AnimateOpponentPose(
                neutralPosition,
                neutralRotation,
                bodyNeutral,
                backhand ? 0.3f : 0.22f,
                false,
                0f);
        }

        private OpponentSwingStyle GetOpponentSwingStyle(
            OpponentShotType shot,
            bool backhand,
            bool receivingSmash)
        {
            if (receivingSmash)
            {
                return OpponentSwingStyle.SmashDefense;
            }

            if (shot == OpponentShotType.Smash)
            {
                return OpponentSwingStyle.JumpSmash;
            }

            if (shot == OpponentShotType.Net)
            {
                return OpponentSwingStyle.Net;
            }

            if (shot == OpponentShotType.Lift)
            {
                return OpponentSwingStyle.Lift;
            }

            if (shot == OpponentShotType.Drop && !backhand)
            {
                return difficultyLevel == 0
                    ? OpponentSwingStyle.ForehandOverhead
                    : OpponentSwingStyle.ForehandDrop;
            }

            return backhand
                ? OpponentSwingStyle.BackhandOverhead
                : OpponentSwingStyle.ForehandOverhead;
        }

        private IEnumerator AnimateOpponentForehandClear(bool preparationComplete)
        {
            const float handSide = -1f;

            if (!preparationComplete)
            {
                // 1. Ready: racket upright, weight centered.
                yield return AnimateOpponentPose(
                    new Vector3(handSide * 0.78f, 0.9f, -0.04f),
                    Quaternion.Euler(8f, 180f, 10f),
                    Quaternion.Euler(0f, -8f, 1f),
                    0.1f,
                    false,
                    0f);

                // 2. Turn sideways and raise the elbow.
                yield return AnimateOpponentPose(
                    new Vector3(handSide * 0.5f, 1.2f, 0.18f),
                    Quaternion.Euler(-18f, 180f, 32f),
                    Quaternion.Euler(-5f, 58f, 6f),
                    0.13f,
                    false,
                    0f);

                // 3. Racket head drops behind the shoulder while the elbow stays high.
                yield return AnimateOpponentPose(
                    new Vector3(handSide * 0.26f, 1.08f, 0.56f),
                    Quaternion.Euler(58f, 180f, 94f),
                    Quaternion.Euler(-10f, 88f, 11f),
                    0.18f,
                    false,
                    0f);
            }

            if (!preparationComplete)
            {
                // 4. Drive upward: hips unwind and the arm reaches for the apex.
                yield return AnimateOpponentPose(
                    new Vector3(handSide * 0.56f, 1.52f, 0.08f),
                    Quaternion.Euler(-54f, 180f, 24f),
                    Quaternion.Euler(-4f, 24f, 4f),
                    0.12f,
                    false,
                    0f);

                // 5. Full extension at contact with the body facing forward.
                yield return AnimateOpponentPose(
                    new Vector3(handSide * 0.7f, 1.76f, -0.1f),
                    Quaternion.Euler(0f, 180f, 0f),
                    Quaternion.identity,
                    0.1f,
                    false,
                    0f);
            }

            // 6. Forearm pronation sends the racket across the body.
            yield return AnimateOpponentPose(
                new Vector3(-0.3f, 0.92f, -0.42f),
                Quaternion.Euler(118f, 180f, 24f),
                Quaternion.Euler(9f, 30f, 8f),
                0.2f,
                false,
                0f);

            // 7. Recover balance and return to the central ready position.
            yield return AnimateOpponentPose(
                new Vector3(-0.72f, 0.72f, -0.02f),
                Quaternion.Euler(18f, 180f, 8f),
                Quaternion.Euler(0f, -10f, 2f),
                0.22f,
                false,
                0f);
        }

        private IEnumerator AnimateOpponentPose(
            Vector3 targetPosition,
            Quaternion targetRotation,
            Quaternion targetBodyRotation,
            float duration,
            bool jumping,
            float jumpPhase)
        {
            Vector3 startPosition = opponentRacket.localPosition;
            Quaternion startRotation = opponentRacket.localRotation;
            Quaternion startBodyRotation = opponentBody.localRotation;
            float startHeight = opponentPlayer.position.y;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                opponentRacket.localPosition = Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    t);
                opponentRacket.localRotation = Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    t);
                opponentBody.localRotation = Quaternion.Slerp(
                    startBodyRotation,
                    targetBodyRotation,
                    t);
                if (jumping)
                {
                    Vector3 bodyPosition = opponentPlayer.position;
                    bodyPosition.y = Mathf.Lerp(
                        startHeight,
                        0.55f + 0.82f * jumpPhase,
                        t);
                    opponentPlayer.position = bodyPosition;
                }
                yield return null;
            }
        }

        private Vector3 GetOpponentReadyPosition(Vector3 contactPoint, float sourceSide)
        {
            bool backhand = ShouldOpponentUseBackhand(contactPoint);
            float handSide = backhand ? 1f : -1f;
            return new Vector3(
                contactPoint.x - handSide * 0.72f,
                0.55f,
                contactPoint.z + 0.12f);
        }

        private void MoveOpponentTowards(
            Vector3 readyPosition,
            Vector3 contactPoint,
            float sourceSide)
        {
            Vector3 previousPosition = opponentPlayer.position;
            Vector3 groundedTarget = new Vector3(
                readyPosition.x,
                0.55f,
                readyPosition.z);
            opponentPlayer.position = Vector3.MoveTowards(
                opponentPlayer.position,
                groundedTarget,
                opponentMoveSpeed * Time.deltaTime);
            SpendOpponentRunStamina(previousPosition, opponentPlayer.position);

            bool backhand = ShouldOpponentUseBackhand(contactPoint);
            PositionOpponentRacket(contactPoint, sourceSide, backhand);
            Quaternion bodyTarget = Quaternion.Euler(
                0f,
                backhand ? 138f : 16f,
                backhand ? -9f : 3f);
            opponentBody.localRotation = Quaternion.Slerp(
                opponentBody.localRotation,
                bodyTarget,
                8f * Time.deltaTime);
        }

        private void UpdateOpponentForehandClearPreparation(
            float approachProgress,
            Vector3 readyPosition,
            Vector3 contactPoint)
        {
            Vector3 previousPosition = opponentPlayer.position;
            Vector3 groundedTarget = new Vector3(
                readyPosition.x,
                0.55f,
                readyPosition.z);
            opponentPlayer.position = Vector3.MoveTowards(
                opponentPlayer.position,
                groundedTarget,
                opponentMoveSpeed * Time.deltaTime);
            SpendOpponentRunStamina(previousPosition, opponentPlayer.position);

            approachProgress = Mathf.Clamp01(approachProgress);
            Vector3 readyRacketPosition = new Vector3(-0.78f, 0.9f, -0.04f);
            Quaternion readyRacketRotation = Quaternion.Euler(8f, 180f, 10f);
            Quaternion readyBodyRotation = Quaternion.Euler(0f, 0f, 0f);

            Vector3 sideRacketPosition = new Vector3(-0.5f, 1.2f, 0.18f);
            Quaternion sideRacketRotation = Quaternion.Euler(-18f, 180f, 32f);
            Quaternion sideBodyRotation = Quaternion.Euler(-5f, 58f, 6f);

            Vector3 drawRacketPosition = new Vector3(-0.26f, 1.08f, 0.56f);
            Quaternion drawRacketRotation = Quaternion.Euler(58f, 180f, 94f);
            Quaternion drawBodyRotation = Quaternion.Euler(-10f, 88f, 11f);

            if (approachProgress < 0.38f)
            {
                float t = Mathf.SmoothStep(0f, 1f, approachProgress / 0.38f);
                opponentRacket.localPosition = Vector3.Lerp(
                    readyRacketPosition,
                    sideRacketPosition,
                    t);
                opponentRacket.localRotation = Quaternion.Slerp(
                    readyRacketRotation,
                    sideRacketRotation,
                    t);
                opponentBody.localRotation = Quaternion.Slerp(
                    readyBodyRotation,
                    sideBodyRotation,
                    t);
            }
            else
            {
                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    (approachProgress - 0.38f) / 0.62f);
                opponentRacket.localPosition = Vector3.Lerp(
                    sideRacketPosition,
                    drawRacketPosition,
                    t);
                opponentRacket.localRotation = Quaternion.Slerp(
                    sideRacketRotation,
                    drawRacketRotation,
                    t);
                opponentBody.localRotation = Quaternion.Slerp(
                    sideBodyRotation,
                    drawBodyRotation,
                    t);
            }

            if (approachProgress > 0.92f)
            {
                Quaternion contactRotation = Quaternion.Euler(0f, 180f, 0f);
                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    (approachProgress - 0.92f) / 0.08f);
                Vector3 localContact = opponentPlayer.InverseTransformPoint(contactPoint);
                Vector3 contactPosition =
                    localContact -
                    contactRotation * opponentRacketFace.localPosition;
                opponentRacket.localPosition = Vector3.Lerp(
                    opponentRacket.localPosition,
                    contactPosition,
                    t);
                opponentRacket.localRotation = Quaternion.Slerp(
                    opponentRacket.localRotation,
                    contactRotation,
                    t);
                opponentBody.localRotation = Quaternion.Slerp(
                    drawBodyRotation,
                    Quaternion.identity,
                    t);
            }
        }

        private void AlignOpponentRacketFace(
            Vector3 contactPoint,
            Quaternion racketRotation)
        {
            opponentRacket.localRotation = racketRotation;
            Vector3 localContact = opponentPlayer.InverseTransformPoint(contactPoint);
            opponentRacket.localPosition =
                localContact -
                racketRotation * opponentRacketFace.localPosition;
        }

        private float OpponentDistanceTo(Vector3 readyPosition)
        {
            Vector2 opponentGround = new Vector2(
                opponentPlayer.position.x,
                opponentPlayer.position.z);
            Vector2 targetGround = new Vector2(readyPosition.x, readyPosition.z);
            return Vector2.Distance(opponentGround, targetGround);
        }

        private bool ShouldOpponentUseBackhand(Vector3 contactPoint)
        {
            if (difficultyLevel == 0)
            {
                return false;
            }

            return contactPoint.x > opponentPlayer.position.x;
        }

        private void PositionOpponentRacket(
            Vector3 contactPoint,
            float sourceSide,
            bool backhand)
        {
            float handSide = backhand ? 1f : -1f;
            float desiredHeight = Mathf.Clamp(
                contactPoint.y - opponentPlayer.position.y - 1.35f,
                0.35f,
                1.65f);
            opponentRacket.localPosition = new Vector3(
                handSide * 0.72f,
                desiredHeight,
                -0.02f);
            opponentRacket.localRotation = Quaternion.Euler(
                18f,
                backhand ? 0f : 180f,
                (backhand ? -sourceSide : sourceSide) * 12f);
        }

        private void PrepareOpponentRacket(
            Vector3 contactPoint,
            float sourceSide,
            bool backhand,
            bool jumpSmash)
        {
            PositionOpponentRacket(contactPoint, sourceSide, backhand);
            if (jumpSmash)
            {
                Vector3 bodyPosition = opponentPlayer.position;
                bodyPosition.y = 0.55f;
                opponentPlayer.position = bodyPosition;
            }

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
            markerYellow.color = new Color(
                startColor.r,
                startColor.g,
                startColor.b,
                1f);
            trajectoryMaterial.color = new Color(
                startColor.r * 0.32f,
                startColor.g * 0.32f,
                startColor.b * 0.32f,
                1f);
        }

        private static Vector3 EvaluateArc(
            Vector3 start,
            Vector3 target,
            float t,
            float height,
            float apexT = 0.5f)
        {
            Vector3 position = Vector3.Lerp(start, target, t);
            apexT = Mathf.Clamp(apexT, 0.2f, 0.8f);
            float normalized = t <= apexT
                ? (t - apexT) / apexT
                : (t - apexT) / (1f - apexT);
            position.y += height * (1f - normalized * normalized);
            return position;
        }

        private static float FindDescendingContactProgress(
            Vector3 start,
            Vector3 target,
            float height,
            float apexT,
            float desiredHeight)
        {
            float bestProgress = apexT;
            float bestDifference = float.MaxValue;
            const int sampleCount = 80;
            for (int i = 0; i <= sampleCount; i++)
            {
                float t = Mathf.Lerp(apexT, 0.96f, i / (float)sampleCount);
                float difference = Mathf.Abs(
                    EvaluateArc(start, target, t, height, apexT).y -
                    desiredHeight);
                if (difference < bestDifference)
                {
                    bestDifference = difference;
                    bestProgress = t;
                }
            }

            return bestProgress;
        }

        private void MoveShuttle(Vector3 position, ref Vector3 previousPosition)
        {
            bool crossedNet =
                (previousPosition.z < 0f && position.z >= 0f) ||
                (previousPosition.z > 0f && position.z <= 0f);
            if (crossedNet)
            {
                float denominator = position.z - previousPosition.z;
                float crossingT = Mathf.Abs(denominator) < 0.0001f
                    ? 0f
                    : Mathf.Clamp01(-previousPosition.z / denominator);
                Vector3 crossingPoint = Vector3.Lerp(previousPosition, position, crossingT);
                if (crossingPoint.y < 1.53f)
                {
                    netFaultTriggered = true;
                    crossingPoint.z = currentFlightHitter == 1 ? -0.035f : 0.035f;
                    shuttle.position = crossingPoint;
                    previousPosition = crossingPoint;
                    return;
                }

                if (currentFlightHitter == 1 &&
                    temporarySlowMotionArmed &&
                    !temporarySlowMotionActive)
                {
                    temporarySlowMotionArmed = false;
                    temporarySlowMotionActive = true;
                    Time.timeScale = 0.2f;
                }
                else if (currentFlightHitter == 2 &&
                    temporarySlowMotionActive)
                {
                    temporarySlowMotionActive = false;
                    Time.timeScale = 1f;
                }
            }

            Vector3 direction = position - previousPosition;
            if (direction.sqrMagnitude > 0.00001f)
            {
                shuttle.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            Vector3 velocity = direction / Mathf.Max(Time.deltaTime, 0.001f);
            RecordShuttleFrame(position, velocity);
            shuttle.position = position;
            if (apexProjection.gameObject.activeSelf)
            {
                apexProjection.position = new Vector3(
                    position.x,
                    0.034f,
                    position.z);
                apexProjection.rotation = Quaternion.Euler(0f, 45f, 0f);
            }
            previousPosition = position;
        }

        private IEnumerator ResolveNetFault()
        {
            HideLandingPrediction();
            playerPositionMarker.gameObject.SetActive(false);
            shuttleIncoming = false;
            incomingHighClear = false;
            hasPlayerContactPrediction = false;
            incomingOpponentSmash = false;
            smashReceiveReady = false;
            jumpActive = false;
            jumpOffset = 0f;

            Vector3 start = shuttle.position;
            Vector3 target = new Vector3(start.x, 0.09f, start.z);
            float elapsed = 0f;
            const float dropDuration = 0.38f;
            while (elapsed < dropDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dropDuration);
                shuttle.position = Vector3.Lerp(start, target, t);
                yield return null;
            }

            rallyWinner = currentFlightHitter == 1 ? 2 : 1;
            netFaultTriggered = false;
            yield return new WaitForSeconds(0.35f);
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
            player.transform.localScale = new Vector3(0.55f, 1.1f, 0.4f);
            player.GetComponent<MeshRenderer>().sharedMaterial = racketDark;
            Destroy(player.GetComponent<Collider>());
            return player;
        }

        private GameObject CreateOpponentPlayer()
        {
            GameObject root = new GameObject("Opponent Player");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(
                -1.25f,
                0.55f,
                2.45f * CourtLengthScale);

            GameObject bodyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyObject.name = "Opponent Body";
            bodyObject.transform.SetParent(root.transform, false);
            bodyObject.transform.localScale = new Vector3(0.58f, 1.1f, 0.4f);
            bodyObject.GetComponent<MeshRenderer>().sharedMaterial = racketBlue;
            Destroy(bodyObject.GetComponent<Collider>());
            opponentBody = bodyObject.transform;
            return root;
        }

        private GameObject CreateOpponentRacket(Transform playerRoot)
        {
            GameObject root = new GameObject("Opponent Pixel Racket");
            root.transform.SetParent(playerRoot, false);
            root.transform.localPosition = new Vector3(-0.72f, 0.55f, -0.02f);
            root.transform.localRotation = Quaternion.Euler(18f, 180f, 0f);

            CreateBlock("Grip", root.transform, new Vector3(0f, 0.275f, 0f),
                new Vector3(0.14f, 0.55f, 0.14f), racketDark);
            CreateBlock("Shaft", root.transform, new Vector3(0f, 0.79f, 0f),
                new Vector3(0.07f, 0.48f, 0.07f), racketBlue);

            opponentRacketFace = new GameObject("Opponent Racket Face").transform;
            opponentRacketFace.SetParent(root.transform, false);
            opponentRacketFace.localPosition = new Vector3(0f, 1.35f, 0f);

            CreateBlock("Frame Top", opponentRacketFace, new Vector3(0f, 0.38f, 0f),
                new Vector3(0.58f, 0.09f, 0.09f), racketBlue);
            CreateBlock("Frame Bottom", opponentRacketFace, new Vector3(0f, -0.38f, 0f),
                new Vector3(0.58f, 0.09f, 0.09f), racketBlue);
            CreateBlock("Frame Left", opponentRacketFace, new Vector3(-0.29f, 0f, 0f),
                new Vector3(0.09f, 0.76f, 0.09f), racketBlue);
            CreateBlock("Frame Right", opponentRacketFace, new Vector3(0.29f, 0f, 0f),
                new Vector3(0.09f, 0.76f, 0.09f), racketBlue);

            for (int i = -2; i <= 2; i++)
            {
                CreateBlock($"Vertical String {i + 3}", opponentRacketFace, new Vector3(i * 0.095f, 0f, 0.015f),
                    new Vector3(0.018f, 0.68f, 0.018f), racketString);
            }

            for (int i = -3; i <= 3; i++)
            {
                CreateBlock($"Horizontal String {i + 4}", opponentRacketFace, new Vector3(0f, i * 0.09f, 0.015f),
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

        private GameObject CreateTrajectoryGuide()
        {
            GameObject root = new GameObject("Ground Trajectory Guide");
            root.transform.SetParent(transform, false);

            const int dashCount = 18;
            for (int i = 0; i < dashCount; i++)
            {
                GameObject dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dash.name = $"Trajectory Dash {i + 1:00}";
                dash.transform.SetParent(root.transform, false);
                dash.transform.localScale = new Vector3(0.08f, 0.018f, 0.22f);
                dash.GetComponent<MeshRenderer>().sharedMaterial = trajectoryMaterial;
                Destroy(dash.GetComponent<Collider>());
            }

            GameObject apex = GameObject.CreatePrimitive(PrimitiveType.Cube);
            apex.name = "Apex Projection";
            apex.transform.SetParent(root.transform, false);
            apex.transform.localScale = new Vector3(0.28f, 0.025f, 0.28f);
            apex.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            apex.GetComponent<MeshRenderer>().sharedMaterial = markerYellow;
            Destroy(apex.GetComponent<Collider>());
            apexProjection = apex.transform;

            return root;
        }

        private GameObject CreateRacketCenterGuide()
        {
            GameObject guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guide.name = "Racket Center Ground Guide";
            guide.transform.SetParent(transform, false);
            guide.transform.localScale = new Vector3(0.82f, 0.018f, 0.055f);
            guide.GetComponent<MeshRenderer>().sharedMaterial = playerPositionMaterial;
            Destroy(guide.GetComponent<Collider>());
            return guide;
        }

        private void ShowLandingPrediction(
            Vector3 start,
            Vector3 target,
            float apexT = 0.5f,
            bool showApex = false)
        {
            landingMarker.position = new Vector3(target.x, 0.025f, target.z);
            landingMarker.gameObject.SetActive(true);

            Vector3 groundStart = new Vector3(start.x, 0.02f, start.z);
            Vector3 groundTarget = new Vector3(target.x, 0.02f, target.z);
            Vector3 direction = groundTarget - groundStart;
            float distance = direction.magnitude;
            if (distance < 0.01f)
            {
                trajectoryGuide.gameObject.SetActive(false);
                return;
            }

            trajectoryGuide.gameObject.SetActive(true);
            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            int dashCount = trajectoryGuide.childCount - 1;
            for (int i = 0; i < dashCount; i++)
            {
                float t = (i + 0.5f) / dashCount;
                Transform dash = trajectoryGuide.GetChild(i);
                dash.position = Vector3.Lerp(groundStart, groundTarget, t);
                dash.rotation = rotation;
                dash.localScale = new Vector3(
                    0.08f,
                    0.018f,
                    Mathf.Min(0.28f, distance / (dashCount * 1.65f)));
            }

            apexProjection.gameObject.SetActive(true);
            apexProjection.position = groundStart + Vector3.up * 0.014f;
            apexProjection.rotation = Quaternion.Euler(0f, 45f, 0f);
        }

        private void HideLandingPrediction()
        {
            if (landingMarker != null)
            {
                landingMarker.gameObject.SetActive(false);
            }

            if (trajectoryGuide != null)
            {
                trajectoryGuide.gameObject.SetActive(false);
            }

            if (apexProjection != null)
            {
                apexProjection.gameObject.SetActive(false);
            }
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
            trajectoryMaterial = CreateRuntimeMaterial(
                shader,
                "Trajectory Dark",
                new Color(0.22f, 0.22f, 0.18f));
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
