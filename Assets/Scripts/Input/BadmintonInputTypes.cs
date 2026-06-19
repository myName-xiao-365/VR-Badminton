using System;
using UnityEngine;

namespace VRBadminton.Input
{
    public enum BadmintonInputMode
    {
        Sensor,
        Legacy
    }

    public enum BadmintonSwingState
    {
        Idle,
        Prepare,
        Swing,
        ImpactCandidate,
        Recover
    }

    public enum BadmintonSwingType
    {
        None,
        Overhead,
        Forehand,
        Backhand,
        Underhand,
        Unknown
    }

    [Serializable]
    public struct BadmintonRightHand
    {
        public bool Visible;
        public string Source;
        public float Confidence;
        public Vector2 Image;
        public Vector2 Relative;
        public float Height;

        public static BadmintonRightHand Default()
        {
            return new BadmintonRightHand
            {
                Visible = false,
                Source = "none",
                Confidence = 0f,
                Image = Vector2.zero,
                Relative = Vector2.zero,
                Height = 0f
            };
        }
    }

    [Serializable]
    public struct BadmintonPlayerFrame
    {
        public long Timestamp;
        public string ClientId;
        public bool Visible;
        public bool Calibrated;
        public float Confidence;
        public string TrackingBasis;
        public string Clipping;
        public Vector2 Center;
        public Vector2 Lean;
        public Vector3 VirtualPosition;
        public BadmintonRightHand RightHand;
        public string MotionState;
        public string Posture;

        public static BadmintonPlayerFrame Default(string clientId = "player")
        {
            return new BadmintonPlayerFrame
            {
                Timestamp = BadmintonInputClock.NowMs(),
                ClientId = clientId,
                Visible = false,
                Calibrated = false,
                Confidence = 0f,
                TrackingBasis = "none",
                Clipping = "none",
                Center = Vector2.zero,
                Lean = Vector2.zero,
                VirtualPosition = Vector3.zero,
                RightHand = BadmintonRightHand.Default(),
                MotionState = "lost",
                Posture = "unknown"
            };
        }
    }

    [Serializable]
    public struct BadmintonRacketFrame
    {
        public long Timestamp;
        public string ClientId;
        public bool Aligned;
        public string SessionId;
        public Quaternion Orientation;
        public Matrix4x4 RotationMatrix;
        public Vector3 AngularVelocity;
        public Vector3 Acceleration;
        public float AngularSpeed;
        public Vector3 RawEuler;

        public static BadmintonRacketFrame Default(string clientId = "racket")
        {
            return new BadmintonRacketFrame
            {
                Timestamp = BadmintonInputClock.NowMs(),
                ClientId = clientId,
                Aligned = false,
                SessionId = string.Empty,
                Orientation = Quaternion.identity,
                RotationMatrix = Matrix4x4.identity,
                AngularVelocity = Vector3.zero,
                Acceleration = Vector3.zero,
                AngularSpeed = 0f,
                RawEuler = Vector3.zero
            };
        }
    }

    [Serializable]
    public struct BadmintonSwingSample
    {
        public BadmintonSwingState State;
        public BadmintonSwingType Type;
        public float AngularSpeed;
        public float PeakSpeed;
        public Vector3 Direction;
        public bool Impact;
        public float SinceImpactMs;

        public static BadmintonSwingSample Default()
        {
            return new BadmintonSwingSample
            {
                State = BadmintonSwingState.Idle,
                Type = BadmintonSwingType.None,
                AngularSpeed = 0f,
                PeakSpeed = 0f,
                Direction = Vector3.zero,
                Impact = false,
                SinceImpactMs = 999999f
            };
        }
    }

    public struct BadmintonInputContext
    {
        public bool ShuttleIncoming;
        public bool AwaitingPlayerServe;
        public bool IncomingOpponentSmash;
        public float ContactWindow;
    }

    public struct BadmintonInputSnapshot
    {
        public BadmintonPlayerFrame Player;
        public BadmintonRacketFrame Racket;
        public BadmintonSwingSample Swing;
        public bool PlayerStale;
        public bool RacketStale;
        public bool SmashReceiveReady;
        public bool ToggleBackhand;
        public bool HasGroundPosition;
        public Vector3 GroundPosition;
        public float Face01;
        public float FaceAngle;
        public float DisplayedPower;
        public bool HasSwingGesture;
        public bool SwingUpward;
        public float SwingGameSpeed;
        public float SwingStartAngle;
        public string Status;
        public string CameraStatus;
        public string PhoneStatus;
        public string PhoneUrl;
        public bool CameraRunning;
        public bool PhoneConnected;
        public bool Calibrated;

        public static BadmintonInputSnapshot Default()
        {
            return new BadmintonInputSnapshot
            {
                Player = BadmintonPlayerFrame.Default(),
                Racket = BadmintonRacketFrame.Default(),
                Swing = BadmintonSwingSample.Default(),
                PlayerStale = true,
                RacketStale = true,
                SmashReceiveReady = false,
                ToggleBackhand = false,
                HasGroundPosition = false,
                GroundPosition = Vector3.zero,
                Face01 = 0.5f,
                FaceAngle = 37.5f,
                DisplayedPower = 0f,
                HasSwingGesture = false,
                SwingUpward = false,
                SwingGameSpeed = 0f,
                SwingStartAngle = 37.5f,
                Status = "Input idle",
                CameraStatus = "Camera idle",
                PhoneStatus = "Phone idle",
                PhoneUrl = string.Empty,
                CameraRunning = false,
                PhoneConnected = false,
                Calibrated = false
            };
        }
    }

    public static class BadmintonInputClock
    {
        public static long NowMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
