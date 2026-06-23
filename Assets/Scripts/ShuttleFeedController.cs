using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Unity.Profiling;
using UnityEngine;
using VRBadminton.Gameplay;
using VRBadminton.Input;

namespace VRBadminton.App
{
    public sealed partial class ShuttleFeedController : MonoBehaviour
    {
        internal enum ShotType
        {
            Net,
            Drop,
            Clear,
            Smash,
            Drive,
            Miss,
            Out
        }

        internal enum OpponentShotType
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
            BackhandDrop,
            ForehandNet,
            BackhandNet,
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
        private static readonly ProfilerMarker ActiveInputTickMarker =
            new ProfilerMarker("VRBadminton.ActiveInput.Tick");
        private static readonly ProfilerMarker GuiMarker =
            new ProfilerMarker("VRBadminton.GUI.OnGUI");
        private static readonly ProfilerMarker HitResolveMarker =
            new ProfilerMarker("VRBadminton.HitResolver.Resolve");
        private static readonly ProfilerMarker FlightMoveMarker =
            new ProfilerMarker("VRBadminton.Flight.MoveShuttle");
        private static readonly ProfilerMarker HudUpdateMarker =
            new ProfilerMarker("VRBadminton.HUD.Update");
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
        [SerializeField, Range(0.4f, 1f)] private float speedAfterNet = 1f;
        [SerializeField, Range(0.4f, 1f)] private float opponentSmashSpeedBeforeNet = 1f;
        [SerializeField, Range(0.1f, 1f)] private float opponentSmashSpeedAfterNet = 1f;

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
        // Enable only when tuning sensor-mode hit detection; logs are intentionally off for Player baselines.
        [SerializeField] private bool logSensorHitDebug;
        [SerializeField] private bool writeSensorHitLogFile = true;
        [SerializeField] private string sensorHitLogDirectory = "Logs/SensorHit";

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
        [SerializeField] private int sensorPhonePort = 8093;
        [SerializeField] private float sensorAngularSpeedToGameSpeed = 4f;
        [SerializeField] private float sensorLateralScale = 1.35f;
        [SerializeField] private float sensorDepthScale = 1.45f;
        [SerializeField] private float sensorBackcourtDepthBoost = 1.32f;
        [SerializeField] private float sensorHandHeightScale = 1.1f;
        [SerializeField] private float sensorHandLateralScale = 0.25f;

        [Header("Opponent")]
        [SerializeField] private float opponentMoveSpeed = 4.2f;
        [SerializeField] private float opponentReachTolerance = 0.22f;
        [SerializeField] private float opponentRecoverySpeed = 3.6f;
        [SerializeField] private float opponentFrontCourtReadyDepth = 2.8f;
        [SerializeField] private float opponentMaxStamina = 100f;
        [SerializeField] private float opponentRunStaminaPerMeter = 0.35f;
        [SerializeField, Range(0f, 1f)] private float opponentSmashReceiveChance = 0.55f;

        [Header("Mouse Stroke")]
        [SerializeField] private float minimumSwingSpeed = 220f;
        [SerializeField] private float mediumSwingSpeed = 1800f;
        [SerializeField] private float fastSwingSpeed = 3600f;
        [SerializeField] private float upwardOutSpeed = 5600f;
        [SerializeField] private float minimumAngleTravel = 18f;

        [Header("Sensor Return Intent")]
        [SerializeField, Range(0.5f, 1.25f)]
        private float sensorReturnPowerExponent = 1.05f;
        [SerializeField, Range(0.5f, 1f)]
        private float sensorAssistPowerRetention = 0.88f;
        [SerializeField, Range(0.5f, 1f)]
        private float sensorMagnetPowerRetention = 0.74f;
        [SerializeField, Range(5f, 45f)]
        private float sensorMaxAimYawDegrees = 30f;

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
        private float pendingSwingPowerSpeed;
        private float pendingStartAngle;
        private float pendingSwingTime;
        private float smoothedMouseSpeed;
        private float swingCooldown;
        private Quaternion racketRestRotation;
        private Quaternion sensorRacketRotationOffset = Quaternion.identity;
        private bool hasSensorRacketRotationOffset;
        private float lastSensorVirtualZ;
        private bool hasLastSensorVirtualZ;
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
        private string sensorHitLogPath;
        private bool sensorHitLogPathReady;
        private bool sensorHitLogFileFailed;
        private Camera gameplayCamera;
        private const int SwitchCameraPresetVersion = 10;
        private float playerServeSide = 1f;
        private bool settingsOpen;
        private bool resolutionOptionsOpen;
        private bool isPaused;
        private ShuttleFeedRuntimeHud runtimeHud;
        private readonly OpponentPoseAnimator opponentPoseAnimator = new OpponentPoseAnimator();
        private readonly OpponentMovementRunner opponentMovementRunner = new OpponentMovementRunner();
        private readonly ShuttleReturnPlanner shuttleReturnPlanner = new ShuttleReturnPlanner();
        private readonly ShuttleFlightRunner shuttleFlightRunner = new ShuttleFlightRunner();
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
        private bool temporarySlowMotionEnabled;
        private bool opponentReturningToCenter;

        private void Awake()
        {
            ApplySwitchCameraPreset();
            CreateMaterials();
            CreateMinecraftBackground();
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
            CreateRuntimeHud();
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
            DestroyRuntimeHud();
            legacyInputSource?.Dispose();
            sensorInputSource?.Dispose();
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

            using (ActiveInputTickMarker.Auto())
            {
                activeInputSource?.Tick(new BadmintonInputContext
                {
                    ShuttleIncoming = shuttleIncoming,
                    AwaitingPlayerServe = awaitingPlayerServe,
                    IncomingOpponentSmash = incomingOpponentSmash,
                    ContactWindow = contactWindow
                });
            }
            inputSnapshot = activeInputSource?.Snapshot ?? BadmintonInputSnapshot.Default();
            currentMouseY = Mathf.Clamp01(inputSnapshot.Face01);
            currentFaceAngle = inputSnapshot.FaceAngle;
            displayedPower = inputSnapshot.DisplayedPower;

            if (inputSnapshot.ToggleBackhand)
            {
                isBackhand = !isBackhand;
            }

            bool spacePressed = UnityEngine.Input.GetKeyDown(KeyCode.Space);
            bool smashReceiveRequested =
                spacePressed ||
                (inputMode == BadmintonInputMode.Sensor
                    ? inputSnapshot.HasSwingGesture
                    : inputSnapshot.SmashReceiveReady);
            bool jumpRequested = spacePressed || inputSnapshot.JumpReady;
            if (awaitingOpponentServe)
            {
                if (inputSnapshot.OpponentServeReady || spacePressed)
                {
                    opponentServeReady = true;
                    LogSensorHitDebug(
                        "opponent_serve_ready",
                        $"viaSensor={B(inputSnapshot.OpponentServeReady)}|viaSpace={B(spacePressed)}");
                }
            }
            else
            {
                if (inputSnapshot.JumpReady && !jumpActive)
                {
                    StartPlayerJump();
                }

                if (incomingOpponentSmash && smashReceiveRequested)
                {
                    smashReceiveReady = true;
                }
                else if (incomingHighClear && !jumpActive && jumpRequested)
                {
                    StartPlayerJump();
                }
            }

            UpdateJump();
            UpdateRacketPosition();
            RecordRacketFrame();
            UpdatePlayerPositionMarker();
            UpdateOpponentReturnToCenter();
            ReadInputSwing();
            UpdatePendingSwingPower();
        }

        private void LateUpdate()
        {
            UpdateSwitchStyleCamera(false);
            UpdateRuntimeHud();
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
    }
}
