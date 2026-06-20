using UnityEngine;

namespace VRBadminton.Gameplay
{
    public readonly struct ShuttleFlightPlan
    {
        public ShuttleFlightPlan(
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
            ApexT = Mathf.Clamp01(apexT);
        }

        public Vector3 Start { get; }

        public Vector3 Target { get; }

        public float Duration { get; }

        public float ArcHeight { get; }

        public float ApexT { get; }

        public bool UsesClearArc => ArcHeight >= 3f;

        public static float DefaultApexT(float arcHeight)
        {
            return arcHeight >= 3f ? 0.7f : 0.5f;
        }

        public Vector3 Evaluate(float progress)
        {
            float t = Mathf.Clamp01(progress);
            Vector3 position = Vector3.Lerp(Start, Target, t);
            float normalized;
            if (t <= ApexT)
            {
                normalized = ApexT <= 0f ? 1f : t / ApexT;
            }
            else
            {
                normalized = ApexT >= 1f ? 0f : (1f - t) / (1f - ApexT);
            }

            position.y += Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI * 0.5f) * ArcHeight;
            return position;
        }
    }
}
