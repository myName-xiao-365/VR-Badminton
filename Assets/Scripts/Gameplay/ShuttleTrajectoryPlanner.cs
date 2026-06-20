using UnityEngine;

namespace VRBadminton.Gameplay
{
    public readonly struct ShuttleTrajectory
    {
        public ShuttleTrajectory(
            Vector3 start,
            Vector3 target,
            float duration,
            float arcHeight,
            float apexT)
        {
            Start = start;
            Target = target;
            Duration = duration;
            ArcHeight = arcHeight;
            ApexT = Mathf.Clamp(apexT, 0.2f, 0.8f);
        }

        public Vector3 Start { get; }

        public Vector3 Target { get; }

        public float Duration { get; }

        public float ArcHeight { get; }

        public float ApexT { get; }

        public bool UsesClearArc => ArcHeight >= 3f;

        public Vector3 Evaluate(float progress)
        {
            float t = Mathf.Clamp01(progress);
            Vector3 position = Vector3.Lerp(Start, Target, t);
            float normalized = t <= ApexT
                ? (t - ApexT) / ApexT
                : (t - ApexT) / (1f - ApexT);
            position.y += ArcHeight * (1f - normalized * normalized);
            return position;
        }

        public float FindDescendingContactProgress(float desiredHeight, int sampleCount = 80)
        {
            int samples = Mathf.Max(1, sampleCount);
            float bestProgress = ApexT;
            float bestDifference = float.MaxValue;
            for (int i = 0; i <= samples; i++)
            {
                float t = Mathf.Lerp(ApexT, 0.96f, i / (float)samples);
                float difference = Mathf.Abs(Evaluate(t).y - desiredHeight);
                if (difference < bestDifference)
                {
                    bestDifference = difference;
                    bestProgress = t;
                }
            }

            return bestProgress;
        }
    }

    public static class ShuttleTrajectoryPlanner
    {
        public static float DefaultApexT(float arcHeight)
        {
            return arcHeight >= 3f ? 0.7f : 0.5f;
        }

        public static ShuttleTrajectory Create(
            Vector3 start,
            Vector3 target,
            float duration,
            float arcHeight)
        {
            return new ShuttleTrajectory(
                start,
                target,
                duration,
                arcHeight,
                DefaultApexT(arcHeight));
        }

        public static ShuttleTrajectory Create(
            Vector3 start,
            Vector3 target,
            float duration,
            float arcHeight,
            float apexT)
        {
            return new ShuttleTrajectory(start, target, duration, arcHeight, apexT);
        }
    }
}
