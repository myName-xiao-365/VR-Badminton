using UnityEngine;

namespace VRBadminton.Input
{
    public sealed class BadmintonSwingDetector
    {
        private readonly float prepareSpeed;
        private readonly float swingSpeed;
        private readonly float impactSpeed;
        private readonly float recoverMs;
        private readonly float impactCooldownMs;

        private float smoothedSpeed;
        private float peakSpeed;
        private long lastImpactAt = long.MinValue;
        private long lastSampleAt;
        private Vector3 lastDirection = Vector3.zero;

        public BadmintonSwingDetector(
            float prepareSpeed = 80f,
            float swingSpeed = 220f,
            float impactSpeed = 420f,
            float recoverMs = 360f,
            float impactCooldownMs = 520f)
        {
            this.prepareSpeed = prepareSpeed;
            this.swingSpeed = swingSpeed;
            this.impactSpeed = impactSpeed;
            this.recoverMs = recoverMs;
            this.impactCooldownMs = impactCooldownMs;
        }

        public BadmintonSwingSample Update(BadmintonRacketFrame frame, long nowMs)
        {
            float rawSpeed = !float.IsNaN(frame.AngularSpeed) && !float.IsInfinity(frame.AngularSpeed)
                ? Mathf.Max(0f, frame.AngularSpeed)
                : frame.AngularVelocity.magnitude;
            long elapsedMs = lastSampleAt > 0 ? Mathf.Max(1, (int)(nowMs - lastSampleAt)) : 16;
            float smoothing = lastSampleAt > 0 ? Mathf.Min(1f, elapsedMs / 90f) : 1f;
            smoothedSpeed = smoothedSpeed * (1f - smoothing) + rawSpeed * smoothing;
            peakSpeed = Mathf.Max(peakSpeed * 0.96f, smoothedSpeed);
            lastSampleAt = nowMs;

            Vector3 direction = frame.AngularVelocity.sqrMagnitude > 0.0001f
                ? frame.AngularVelocity.normalized
                : Vector3.zero;
            if (direction.sqrMagnitude > 0.0001f)
            {
                lastDirection = direction;
            }

            bool impact = false;
            if (smoothedSpeed >= impactSpeed &&
                (lastImpactAt == long.MinValue || nowMs - lastImpactAt > impactCooldownMs))
            {
                impact = true;
                lastImpactAt = nowMs;
                peakSpeed = smoothedSpeed;
            }

            float sinceImpactMs = lastImpactAt == long.MinValue ? 999999f : nowMs - lastImpactAt;
            BadmintonSwingState state = impact
                ? BadmintonSwingState.ImpactCandidate
                : sinceImpactMs < recoverMs
                    ? BadmintonSwingState.Recover
                    : smoothedSpeed >= swingSpeed
                        ? BadmintonSwingState.Swing
                        : smoothedSpeed >= prepareSpeed
                            ? BadmintonSwingState.Prepare
                            : BadmintonSwingState.Idle;

            return new BadmintonSwingSample
            {
                State = state,
                Type = ClassifySwingType(lastDirection, smoothedSpeed),
                AngularSpeed = smoothedSpeed,
                PeakSpeed = peakSpeed,
                Direction = lastDirection,
                Impact = impact,
                SinceImpactMs = sinceImpactMs
            };
        }

        public BadmintonSwingType ClassifySwingType(Vector3 direction, float speed)
        {
            if (speed < prepareSpeed)
            {
                return BadmintonSwingType.None;
            }

            float absX = Mathf.Abs(direction.x);
            float absY = Mathf.Abs(direction.y);
            float absZ = Mathf.Abs(direction.z);
            if (absY > absX && absY > absZ)
            {
                return direction.y < 0f ? BadmintonSwingType.Overhead : BadmintonSwingType.Underhand;
            }

            if (absX > absZ)
            {
                return direction.x > 0f ? BadmintonSwingType.Forehand : BadmintonSwingType.Backhand;
            }

            return BadmintonSwingType.Unknown;
        }
    }
}
