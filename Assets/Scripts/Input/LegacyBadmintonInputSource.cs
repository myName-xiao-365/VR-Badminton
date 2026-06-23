using UnityEngine;

namespace VRBadminton.Input
{
    public sealed class LegacyBadmintonInputSource : IBadmintonInputSource
    {
        private readonly float moveSpeed;
        private readonly float minimumSwingSpeed;
        private readonly float upwardOutSpeed;
        private readonly float minimumAngleTravel;

        private BadmintonInputSnapshot snapshot;
        private Vector3 playerGroundPosition;
        private Vector3 lastMousePosition;
        private float smoothedMouseSpeed;
        private bool gestureTracking;
        private float gestureStartAngle;
        private float gestureDirection;
        private float face01 = 0.5f;
        private float faceAngle = 37.5f;

        public LegacyBadmintonInputSource(
            Vector3 initialGroundPosition,
            float moveSpeed,
            float minimumSwingSpeed,
            float upwardOutSpeed,
            float minimumAngleTravel)
        {
            playerGroundPosition = initialGroundPosition;
            this.moveSpeed = moveSpeed;
            this.minimumSwingSpeed = minimumSwingSpeed;
            this.upwardOutSpeed = upwardOutSpeed;
            this.minimumAngleTravel = minimumAngleTravel;
            snapshot = BadmintonInputSnapshot.Default();
            snapshot.Status = "Legacy keyboard/mouse ready";
            snapshot.CameraStatus = "Legacy mode";
            snapshot.PhoneStatus = "Legacy mode";
        }

        public string Name => "Legacy";

        public BadmintonInputSnapshot Snapshot => snapshot;

        public void Start()
        {
            lastMousePosition = UnityEngine.Input.mousePosition;
            face01 = Mathf.Clamp01(UnityEngine.Input.mousePosition.y / Mathf.Max(1f, Screen.height));
            faceAngle = Mathf.Lerp(-45f, 120f, face01);
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
            Stop();
        }

        public void Tick(BadmintonInputContext context)
        {
            snapshot.ToggleBackhand = UnityEngine.Input.GetKeyDown(KeyCode.Q);
            snapshot.SmashReceiveReady = UnityEngine.Input.GetKeyDown(KeyCode.Space);
            snapshot.HasSwingGesture = false;
            snapshot.Swing = BadmintonSwingSample.Default();
            snapshot.SwingPeakGameSpeed = 0f;

            float horizontal = UnityEngine.Input.GetAxisRaw("Horizontal");
            float vertical = UnityEngine.Input.GetAxisRaw("Vertical");
            Vector3 movement = new Vector3(horizontal, 0f, vertical);
            if (movement.sqrMagnitude > 1f)
            {
                movement.Normalize();
            }

            playerGroundPosition += movement * (moveSpeed * Time.deltaTime);
            playerGroundPosition.x = Mathf.Clamp(playerGroundPosition.x, -2.85f, 2.85f);
            playerGroundPosition.z = Mathf.Clamp(playerGroundPosition.z, -6.15f, -1.15f);

            ReadMouseSwing(context);

            BadmintonPlayerFrame player = BadmintonPlayerFrame.Default("legacy-player");
            player.Timestamp = BadmintonInputClock.NowMs();
            player.Visible = true;
            player.Calibrated = true;
            player.Confidence = 1f;
            player.TrackingBasis = "legacy";
            player.VirtualPosition = new Vector3(
                Mathf.InverseLerp(-2.85f, 2.85f, playerGroundPosition.x) * 2f - 1f,
                0f,
                Mathf.InverseLerp(-6.15f, -1.15f, playerGroundPosition.z) * 2f - 1f);
            player.RightHand = new BadmintonRightHand
            {
                Visible = true,
                Source = "legacy_mouse",
                Confidence = 1f,
                Image = new Vector2(0.5f, 1f - face01),
                Relative = new Vector2(0f, face01),
                Height = face01
            };
            player.MotionState = movement.sqrMagnitude > 0.001f ? "legacy_move" : "ready";
            player.Posture = "legacy";

            snapshot.Player = player;
            snapshot.PlayerStale = false;
            snapshot.Racket = BadmintonRacketFrame.Default("legacy-racket");
            snapshot.RacketStale = false;
            snapshot.HasGroundPosition = true;
            snapshot.GroundPosition = playerGroundPosition;
            snapshot.Face01 = face01;
            snapshot.FaceAngle = faceAngle;
            snapshot.DisplayedPower = Mathf.Lerp(
                snapshot.DisplayedPower,
                Mathf.Clamp01(smoothedMouseSpeed / upwardOutSpeed),
                1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            snapshot.Status = "Legacy keyboard/mouse active";
            snapshot.CameraStatus = "WASD player";
            snapshot.PhoneStatus = "Mouse racket";
            snapshot.Calibrated = true;
            snapshot.CameraRunning = true;
            snapshot.PhoneConnected = true;
        }

        private void ReadMouseSwing(BadmintonInputContext context)
        {
            Vector3 mousePosition = UnityEngine.Input.mousePosition;
            float deltaY = mousePosition.y - lastMousePosition.y;
            lastMousePosition = mousePosition;
            face01 = Mathf.Clamp01(mousePosition.y / Mathf.Max(1f, Screen.height));
            faceAngle = Mathf.Lerp(-45f, 120f, face01);

            float instantaneousSpeed = Mathf.Abs(deltaY) / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
            smoothedMouseSpeed = Mathf.Lerp(
                smoothedMouseSpeed,
                instantaneousSpeed,
                1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));

            if (!gestureTracking)
            {
                if (Mathf.Abs(deltaY) > 1.5f)
                {
                    gestureTracking = true;
                    gestureStartAngle = faceAngle;
                    gestureDirection = Mathf.Sign(deltaY);
                }

                return;
            }

            if (Mathf.Sign(deltaY) != gestureDirection && Mathf.Abs(deltaY) > 2f)
            {
                gestureTracking = false;
                return;
            }

            float angleTravel = Mathf.Abs(faceAngle - gestureStartAngle);
            if (angleTravel < minimumAngleTravel)
            {
                return;
            }

            float speed = smoothedMouseSpeed;
            gestureTracking = false;
            if (speed < minimumSwingSpeed * 0.45f)
            {
                return;
            }

            bool upward = gestureDirection > 0f;
            if (context.AwaitingPlayerServe && !upward)
            {
                return;
            }

            snapshot.HasSwingGesture = true;
            snapshot.SwingUpward = upward;
            snapshot.SwingGameSpeed = speed;
            snapshot.SwingPeakGameSpeed = speed;
            snapshot.SwingStartAngle = gestureStartAngle;
            snapshot.Swing = new BadmintonSwingSample
            {
                State = BadmintonSwingState.ImpactCandidate,
                Type = upward ? BadmintonSwingType.Underhand : BadmintonSwingType.Overhead,
                AngularSpeed = speed,
                PeakSpeed = speed,
                Direction = upward ? Vector3.up : Vector3.down,
                Impact = true,
                SinceImpactMs = 0f
            };
        }
    }
}
