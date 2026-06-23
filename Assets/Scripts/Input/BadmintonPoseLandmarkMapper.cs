using System.Collections.Generic;
using UnityEngine;

namespace VRBadminton.Input
{
    public struct BadmintonPoseLandmark
    {
        public float X;
        public float Y;
        public float Visibility;

        public BadmintonPoseLandmark(float x, float y, float visibility)
        {
            X = x;
            Y = y;
            Visibility = visibility;
        }
    }

    public sealed class BadmintonPoseLandmarkMapper
    {
        public const int Nose = 0;
        public const int LeftShoulder = 11;
        public const int RightShoulder = 12;
        public const int LeftElbow = 13;
        public const int RightElbow = 14;
        public const int LeftWrist = 15;
        public const int RightWrist = 16;
        public const int RightPinky = 18;
        public const int RightIndex = 20;
        public const int RightThumb = 22;
        public const int LeftHip = 23;
        public const int RightHip = 24;

        private const float ShoulderVisibilityMin = 0.42f;
        private const float OptionalVisibilityMin = 0.30f;
        private const float MinShoulderWidth = 0.06f;
        private const float MaxShoulderCenterY = 0.90f;
        private const float ImageToUserXSign = -1f;
        private const float XGain = 6.0f;
        private const float ZGain = 5.8f;
        private const float ZScaleDeltaDeadzone = 0.0025f;
        private const float ZScaleDeltaMaxStep = 0.18f;
        private const float ZPositionLimit = 3.2f;
        private const float LeanGain = 2.2f;
        private const float XSmoothAlpha = 0.42f;
        private const float ZSmoothAlpha = 0.18f;
        private const float ZFastAlpha = 0.36f;
        private const float ZDeadzone = 0.025f;
        private const int AutoDepthSampleCount = 8;

        private readonly Queue<Vector2> depthSamples = new Queue<Vector2>();
        private Vector2 calibrationCenter = new Vector2(0.5f, 0.5f);
        private float calibrationScale;
        private float calibrationTorsoHeight;
        private bool calibrationReady;
        private bool hasSmoothedPosition;
        private Vector3 smoothedVirtualPosition;
        private BadmintonPlayerFrame lastFrame = BadmintonPlayerFrame.Default("camera");
        private float lastComputedScale = 0.24f;
        private float lastComputedTorsoHeight = 0.25f;
        private float trackedVirtualZ;
        private float lastDepthScale;
        private bool hasDepthScaleSample;

        public bool Calibrated => calibrationReady;

        public void Reset()
        {
            depthSamples.Clear();
            calibrationCenter = new Vector2(0.5f, 0.5f);
            calibrationScale = 0f;
            calibrationTorsoHeight = 0f;
            calibrationReady = false;
            hasSmoothedPosition = false;
            trackedVirtualZ = 0f;
            lastDepthScale = 0f;
            hasDepthScaleSample = false;
            lastFrame = BadmintonPlayerFrame.Default("camera");
        }

        public BadmintonPlayerFrame BuildFrame(
            IReadOnlyList<BadmintonPoseLandmark> landmarks,
            long timestampMs,
            string clientId = "camera")
        {
            BadmintonPlayerFrame candidate = BuildCandidateFrame(landmarks, timestampMs, clientId);
            if (candidate.Visible)
            {
                UpdateAutoCalibration(candidate);
                if (calibrationReady)
                {
                    candidate = BuildCandidateFrame(landmarks, timestampMs, clientId);
                }

                UpdateRelativeDepth(ref candidate);
                candidate = SmoothVisibleFrame(candidate);
                lastFrame = candidate;
                return candidate;
            }

            if (lastFrame.Visible || lastFrame.TrackingBasis == "lost_hold")
            {
                lastFrame.Timestamp = timestampMs;
                lastFrame.Visible = false;
                lastFrame.Calibrated = calibrationReady;
                lastFrame.Confidence = 0f;
                lastFrame.TrackingBasis = "lost_hold";
                lastFrame.MotionState = "lost_hold";
                lastFrame.Posture = "hold_last";
                return lastFrame;
            }

            return candidate;
        }

        private BadmintonPlayerFrame BuildCandidateFrame(
            IReadOnlyList<BadmintonPoseLandmark> landmarks,
            long timestampMs,
            string clientId)
        {
            if (landmarks == null || landmarks.Count == 0)
            {
                return EmptyFrame(timestampMs, clientId, "none");
            }

            bool shouldersVisible =
                IsUsable(landmarks, LeftShoulder, ShoulderVisibilityMin) &&
                IsUsable(landmarks, RightShoulder, ShoulderVisibilityMin);
            if (!shouldersVisible)
            {
                return EmptyFrame(timestampMs, clientId, "partial_upper");
            }

            BadmintonPoseLandmark leftShoulder = landmarks[LeftShoulder];
            BadmintonPoseLandmark rightShoulder = landmarks[RightShoulder];
            Vector2 shoulderMid = Mean(leftShoulder, rightShoulder);
            float shoulderWidth = Distance(leftShoulder, rightShoulder);
            if (shoulderWidth < MinShoulderWidth || shoulderMid.y > MaxShoulderCenterY)
            {
                BadmintonPlayerFrame weak = EmptyFrame(timestampMs, clientId, "weak_shoulders");
                weak.Center = shoulderMid;
                weak.MotionState = "partial";
                return weak;
            }

            bool hipsVisible =
                IsUsable(landmarks, LeftHip, OptionalVisibilityMin) &&
                IsUsable(landmarks, RightHip, OptionalVisibilityMin);

            Vector2 center = shoulderMid;
            float apparentScale = shoulderWidth;
            float torsoHeight = Mathf.Max(shoulderWidth * 1.35f, 0.000001f);
            Vector2 lean = new Vector2(0f, (rightShoulder.Y - leftShoulder.Y) / Mathf.Max(shoulderWidth, 0.000001f));
            string trackingBasis = "shoulders";
            float confidence = Mathf.Min(
                0.72f,
                0.30f + 0.21f * leftShoulder.Visibility + 0.21f * rightShoulder.Visibility);
            float headY = shoulderMid.y - shoulderWidth * 0.75f;
            float hipY = shoulderMid.y + torsoHeight;

            if (hipsVisible)
            {
                BadmintonPoseLandmark leftHip = landmarks[LeftHip];
                BadmintonPoseLandmark rightHip = landmarks[RightHip];
                Vector2 hipMid = Mean(leftHip, rightHip);
                torsoHeight = Mathf.Max(Mathf.Abs(hipMid.y - shoulderMid.y), shoulderWidth * 0.7f, 0.000001f);
                apparentScale = torsoHeight;
                lean = new Vector2(
                    (shoulderMid.x - hipMid.x) * ImageToUserXSign / torsoHeight,
                    (rightShoulder.Y - leftShoulder.Y) / Mathf.Max(shoulderWidth, 0.000001f));
                center = shoulderMid * 0.70f + hipMid * 0.30f;
                trackingBasis = "torso";
                confidence = Mathf.Min(
                    1f,
                    0.55f +
                    0.18f * leftShoulder.Visibility +
                    0.18f * rightShoulder.Visibility +
                    0.045f * leftHip.Visibility +
                    0.045f * rightHip.Visibility);
                headY = IsUsable(landmarks, Nose, OptionalVisibilityMin)
                    ? landmarks[Nose].Y
                    : shoulderMid.y - torsoHeight * 0.55f;
                hipY = hipMid.y;
            }

            calibrationCenter.y = center.y;
            Vector2 centerDelta = center - calibrationCenter;
            float virtualX = centerDelta.x * ImageToUserXSign * XGain + lean.x * LeanGain;
            float virtualZ = calibrationReady ? trackedVirtualZ : 0f;

            float torsoRatio = calibrationReady
                ? torsoHeight / Mathf.Max(calibrationTorsoHeight, 0.000001f)
                : 1f;
            Vector3 virtualPosition = new Vector3(virtualX, 0f, virtualZ);
            lastComputedScale = apparentScale;
            lastComputedTorsoHeight = torsoHeight;
            return new BadmintonPlayerFrame
            {
                Timestamp = timestampMs,
                ClientId = clientId,
                Visible = true,
                Calibrated = calibrationReady,
                Confidence = confidence,
                TrackingBasis = trackingBasis,
                Clipping = "none",
                Center = center,
                Lean = lean,
                VirtualPosition = virtualPosition,
                RightHand = RightHandFromLandmarks(landmarks, center, headY, hipY, shoulderWidth),
                MotionState = ClassifyMotion(virtualPosition, lean),
                Posture = ClassifyPosture(lean, torsoRatio, trackingBasis)
            };
        }

        private BadmintonPlayerFrame SmoothVisibleFrame(BadmintonPlayerFrame frame)
        {
            Vector3 raw = frame.VirtualPosition;
            if (!hasSmoothedPosition || !lastFrame.Visible)
            {
                hasSmoothedPosition = true;
                smoothedVirtualPosition = raw;
                return frame;
            }

            float dz = raw.z - smoothedVirtualPosition.z;
            float zAlpha = Mathf.Abs(dz) > 0.55f ? ZFastAlpha : ZSmoothAlpha;
            float nextZ = Mathf.Abs(dz) < ZDeadzone
                ? smoothedVirtualPosition.z
                : smoothedVirtualPosition.z + dz * zAlpha;
            smoothedVirtualPosition = new Vector3(
                smoothedVirtualPosition.x + (raw.x - smoothedVirtualPosition.x) * XSmoothAlpha,
                raw.y,
                nextZ);
            frame.VirtualPosition = smoothedVirtualPosition;
            frame.MotionState = ClassifyMotion(frame.VirtualPosition, frame.Lean);
            return frame;
        }

        private void UpdateRelativeDepth(ref BadmintonPlayerFrame frame)
        {
            if (!calibrationReady)
            {
                return;
            }

            if (frame.TrackingBasis != "torso")
            {
                hasDepthScaleSample = false;
                frame.VirtualPosition = new Vector3(
                    frame.VirtualPosition.x,
                    frame.VirtualPosition.y,
                    trackedVirtualZ);
                frame.MotionState = ClassifyMotion(frame.VirtualPosition, frame.Lean);
                return;
            }

            float currentScale = Mathf.Max(0.000001f, lastComputedScale);
            if (!hasDepthScaleSample)
            {
                lastDepthScale = currentScale;
                hasDepthScaleSample = true;
                frame.VirtualPosition = new Vector3(
                    frame.VirtualPosition.x,
                    frame.VirtualPosition.y,
                    trackedVirtualZ);
                frame.MotionState = ClassifyMotion(frame.VirtualPosition, frame.Lean);
                return;
            }

            float scaleDelta = Mathf.Log(currentScale / Mathf.Max(0.000001f, lastDepthScale));
            lastDepthScale = currentScale;
            float absDelta = Mathf.Abs(scaleDelta);
            if (absDelta >= ZScaleDeltaDeadzone)
            {
                float adjustedDelta = Mathf.Sign(scaleDelta) *
                    Mathf.Min(absDelta - ZScaleDeltaDeadzone, ZScaleDeltaMaxStep);
                trackedVirtualZ = Mathf.Clamp(
                    trackedVirtualZ + adjustedDelta * ZGain,
                    -ZPositionLimit,
                    ZPositionLimit);
            }

            frame.VirtualPosition = new Vector3(
                frame.VirtualPosition.x,
                frame.VirtualPosition.y,
                trackedVirtualZ);
            frame.MotionState = ClassifyMotion(frame.VirtualPosition, frame.Lean);
        }

        private void UpdateAutoCalibration(BadmintonPlayerFrame frame)
        {
            if (!IsCalibrationReady(frame))
            {
                return;
            }

            calibrationCenter = new Vector2(0.5f, frame.Center.y);
            Vector2 sample = new Vector2(
                Mathf.Max(0.000001f, EstimateScale(frame)),
                Mathf.Max(0.000001f, EstimateTorsoHeight(frame)));
            if (!calibrationReady)
            {
                depthSamples.Enqueue(sample);
                while (depthSamples.Count > AutoDepthSampleCount)
                {
                    depthSamples.Dequeue();
                }

                if (depthSamples.Count >= AutoDepthSampleCount)
                {
                    Vector2 sum = Vector2.zero;
                    foreach (Vector2 item in depthSamples)
                    {
                        sum += item;
                    }

                    calibrationScale = sum.x / depthSamples.Count;
                    calibrationTorsoHeight = sum.y / depthSamples.Count;
                    calibrationReady = true;
                }
            }
        }

        private bool IsCalibrationReady(BadmintonPlayerFrame frame)
        {
            return frame.Visible &&
                   frame.Confidence >= 0.55f &&
                   frame.TrackingBasis == "torso" &&
                   frame.Center.y <= MaxShoulderCenterY;
        }

        private float EstimateScale(BadmintonPlayerFrame frame)
        {
            return frame.TrackingBasis == "torso" ? Mathf.Max(0.001f, lastComputedScale) : 0.24f;
        }

        private float EstimateTorsoHeight(BadmintonPlayerFrame frame)
        {
            return frame.TrackingBasis == "torso" ? Mathf.Max(0.001f, lastComputedTorsoHeight) : 0.18f;
        }

        private BadmintonRightHand RightHandFromLandmarks(
            IReadOnlyList<BadmintonPoseLandmark> landmarks,
            Vector2 center,
            float headY,
            float hipY,
            float shoulderWidth)
        {
            List<BadmintonPoseLandmark> handPoints = new List<BadmintonPoseLandmark>(3);
            AddIfUsable(landmarks, RightIndex, OptionalVisibilityMin, handPoints);
            AddIfUsable(landmarks, RightPinky, OptionalVisibilityMin, handPoints);
            AddIfUsable(landmarks, RightThumb, OptionalVisibilityMin, handPoints);
            bool hasWrist = IsUsable(landmarks, RightWrist, OptionalVisibilityMin);
            bool hasElbow = IsUsable(landmarks, RightElbow, OptionalVisibilityMin);

            Vector2 landmark = Vector2.zero;
            string source = "none";
            float confidence = 0f;
            if (handPoints.Count >= 2)
            {
                landmark = Mean(handPoints);
                source = "right_hand";
                confidence = AverageVisibility(handPoints);
            }
            else if (handPoints.Count == 1 && hasWrist)
            {
                landmark = new Vector2(
                    handPoints[0].X * 0.66f + landmarks[RightWrist].X * 0.34f,
                    handPoints[0].Y * 0.66f + landmarks[RightWrist].Y * 0.34f);
                source = "right_hand_est";
                confidence = (handPoints[0].Visibility + landmarks[RightWrist].Visibility) * 0.5f;
            }
            else if (hasWrist)
            {
                landmark = new Vector2(landmarks[RightWrist].X, landmarks[RightWrist].Y);
                source = "right_wrist_fallback";
                confidence = landmarks[RightWrist].Visibility;
            }
            else if (hasElbow)
            {
                landmark = new Vector2(landmarks[RightElbow].X, landmarks[RightElbow].Y);
                source = "right_elbow_fallback";
                confidence = landmarks[RightElbow].Visibility;
            }
            else
            {
                return BadmintonRightHand.Default();
            }

            float heightSpan = hipY - headY;
            if (Mathf.Abs(heightSpan) < 0.000001f)
            {
                heightSpan = heightSpan < 0f ? -0.000001f : 0.000001f;
            }

            float height = Mathf.Clamp((hipY - landmark.y) / heightSpan, -0.12f, 1.25f);
            float lateral = Mathf.Clamp(
                (landmark.x - center.x) * ImageToUserXSign / Mathf.Max(shoulderWidth, 0.000001f),
                -1.55f,
                1.55f);
            return new BadmintonRightHand
            {
                Visible = true,
                Source = source,
                Confidence = confidence,
                Image = landmark,
                Relative = new Vector2(lateral, height),
                Height = height
            };
        }

        private static BadmintonPlayerFrame EmptyFrame(long timestampMs, string clientId, string trackingBasis)
        {
            BadmintonPlayerFrame frame = BadmintonPlayerFrame.Default(clientId);
            frame.Timestamp = timestampMs;
            frame.TrackingBasis = trackingBasis;
            return frame;
        }

        private static bool IsUsable(IReadOnlyList<BadmintonPoseLandmark> landmarks, int index, float threshold)
        {
            return landmarks != null &&
                   index >= 0 &&
                   index < landmarks.Count &&
                   landmarks[index].Visibility >= threshold;
        }

        private static Vector2 Mean(BadmintonPoseLandmark a, BadmintonPoseLandmark b)
        {
            return new Vector2((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f);
        }

        private static Vector2 Mean(List<BadmintonPoseLandmark> points)
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < points.Count; i++)
            {
                sum += new Vector2(points[i].X, points[i].Y);
            }

            return points.Count == 0 ? Vector2.zero : sum / points.Count;
        }

        private static float Distance(BadmintonPoseLandmark a, BadmintonPoseLandmark b)
        {
            return Vector2.Distance(new Vector2(a.X, a.Y), new Vector2(b.X, b.Y));
        }

        private static void AddIfUsable(
            IReadOnlyList<BadmintonPoseLandmark> landmarks,
            int index,
            float threshold,
            List<BadmintonPoseLandmark> target)
        {
            if (IsUsable(landmarks, index, threshold))
            {
                target.Add(landmarks[index]);
            }
        }

        private static float AverageVisibility(List<BadmintonPoseLandmark> points)
        {
            if (points.Count == 0)
            {
                return 0f;
            }

            float sum = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                sum += points[i].Visibility;
            }

            return sum / points.Count;
        }

        private static string ClassifyMotion(Vector3 position, Vector2 lean)
        {
            if (Mathf.Abs(position.x) > 0.75f || Mathf.Abs(position.z) > 0.75f)
            {
                return "large_move";
            }

            if (Mathf.Abs(lean.x) > 0.035f || Mathf.Abs(lean.y) > 0.035f)
            {
                return "micro_adjust";
            }

            return "ready";
        }

        private static string ClassifyPosture(Vector2 lean, float torsoRatio, string basis)
        {
            if (basis == "shoulders")
            {
                return Mathf.Abs(lean.y) > 0.04f ? "shoulder_tilt" : "upper_ready";
            }

            if (torsoRatio < 0.86f)
            {
                return "low_ready";
            }

            if (lean.x > 0.04f)
            {
                return "lean_right";
            }

            if (lean.x < -0.04f)
            {
                return "lean_left";
            }

            return "neutral";
        }
    }
}
