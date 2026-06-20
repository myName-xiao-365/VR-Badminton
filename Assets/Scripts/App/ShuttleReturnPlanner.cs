using UnityEngine;

namespace VRBadminton.App
{
    internal readonly struct ShuttleTrailPalette
    {
        public ShuttleTrailPalette(Color startColor, Color endColor)
        {
            StartColor = startColor;
            EndColor = endColor;
        }

        public Color StartColor { get; }

        public Color EndColor { get; }
    }

    internal readonly struct ShuttlePlayerReturnPlan
    {
        public ShuttlePlayerReturnPlan(
            Vector3 target,
            float duration,
            float arcHeight)
        {
            Target = target;
            Duration = duration;
            ArcHeight = arcHeight;
        }

        public Vector3 Target { get; }

        public float Duration { get; }

        public float ArcHeight { get; }
    }

    internal readonly struct ShuttleOpponentReturnFlightPlan
    {
        public ShuttleOpponentReturnFlightPlan(
            float duration,
            float arcHeight,
            bool isSmash,
            ShuttleTrailPalette trail)
        {
            Duration = duration;
            ArcHeight = arcHeight;
            IsSmash = isSmash;
            Trail = trail;
        }

        public float Duration { get; }

        public float ArcHeight { get; }

        public bool IsSmash { get; }

        public ShuttleTrailPalette Trail { get; }
    }

    internal sealed class ShuttleReturnPlanner
    {
        public ShuttlePlayerReturnPlan CreatePlayerReturnPlan(
            ShuttleFeedController.ShotType shot,
            float courtLengthScale,
            float minimumSwingSpeed,
            float fastSwingSpeed,
            float pendingSwingSpeed)
        {
            switch (shot)
            {
                case ShuttleFeedController.ShotType.Drop:
                    return new ShuttlePlayerReturnPlan(
                        new Vector3(
                            Random.Range(-2.2f, 2.2f),
                            0.09f,
                            Random.Range(1.55f, 2.15f) * courtLengthScale),
                        1.1f,
                        1.35f);

                case ShuttleFeedController.ShotType.Clear:
                    return new ShuttlePlayerReturnPlan(
                        new Vector3(
                            Random.Range(-2.3f, 2.3f),
                            0.09f,
                            6.2f * courtLengthScale),
                        2f,
                        4.5f);

                case ShuttleFeedController.ShotType.Smash:
                    return new ShuttlePlayerReturnPlan(
                        new Vector3(
                            Random.Range(-2.25f, 2.25f),
                            0.09f,
                            4.7f * courtLengthScale),
                        Mathf.Lerp(
                            0.82f,
                            0.42f,
                            Mathf.InverseLerp(
                                minimumSwingSpeed,
                                fastSwingSpeed,
                                pendingSwingSpeed)),
                        0.18f);

                case ShuttleFeedController.ShotType.Drive:
                    return new ShuttlePlayerReturnPlan(
                        new Vector3(
                            Random.Range(-2.3f, 2.3f),
                            0.09f,
                            6.45f * courtLengthScale),
                        0.82f,
                        0.55f);

                case ShuttleFeedController.ShotType.Out:
                    return new ShuttlePlayerReturnPlan(
                        new Vector3(
                            Random.Range(-3.8f, 3.8f),
                            0.09f,
                            8.25f * courtLengthScale),
                        1.25f,
                        1.5f);

                default:
                    return new ShuttlePlayerReturnPlan(
                        new Vector3(
                            Random.Range(-1.9f, 1.9f),
                            0.09f,
                            Random.Range(1.45f, 1.95f) * courtLengthScale),
                        1.05f,
                        1.35f);
            }
        }

        public Vector3 CreateOpponentReturnTarget(
            float sourceSide,
            ShuttleFeedController.OpponentShotType shot,
            float courtLengthScale)
        {
            float targetDepth;
            switch (shot)
            {
                case ShuttleFeedController.OpponentShotType.Net:
                    targetDepth = Random.Range(1.45f, 1.95f);
                    break;
                case ShuttleFeedController.OpponentShotType.Drop:
                    targetDepth = Random.Range(1.75f, 2.25f);
                    break;
                case ShuttleFeedController.OpponentShotType.Smash:
                    targetDepth = Random.Range(3.4f, 5.2f);
                    break;
                default:
                    targetDepth = Random.Range(5.98f, 6.58f);
                    break;
            }

            return new Vector3(
                -sourceSide * Random.Range(0.85f, 2.45f),
                0.09f,
                -targetDepth * courtLengthScale);
        }

        public ShuttleOpponentReturnFlightPlan CreateOpponentReturnFlight(
            ShuttleFeedController.OpponentShotType shot)
        {
            switch (shot)
            {
                case ShuttleFeedController.OpponentShotType.Net:
                    return new ShuttleOpponentReturnFlightPlan(
                        1.15f,
                        1.35f,
                        false,
                        NetTrail());

                case ShuttleFeedController.OpponentShotType.Drop:
                    return new ShuttleOpponentReturnFlightPlan(
                        1.35f,
                        1.65f,
                        false,
                        NetTrail());

                case ShuttleFeedController.OpponentShotType.Smash:
                    return new ShuttleOpponentReturnFlightPlan(
                        0.85f,
                        0.32f,
                        true,
                        SmashTrail());

                default:
                    return new ShuttleOpponentReturnFlightPlan(
                        shot == ShuttleFeedController.OpponentShotType.Lift ? 2.4f : 2.25f,
                        shot == ShuttleFeedController.OpponentShotType.Lift ? 5.25f : 4.85f,
                        false,
                        ClearTrail());
            }
        }

        public ShuttleTrailPalette GetPlayerTrailPalette(
            ShuttleFeedController.ShotType shot)
        {
            switch (shot)
            {
                case ShuttleFeedController.ShotType.Clear:
                    return ClearTrail();
                case ShuttleFeedController.ShotType.Smash:
                    return SmashTrail();
                case ShuttleFeedController.ShotType.Drive:
                    return DriveTrail();
                default:
                    return NetTrail();
            }
        }

        private static ShuttleTrailPalette NetTrail()
        {
            return new ShuttleTrailPalette(
                new Color(0.25f, 1f, 0.35f, 0.82f),
                new Color(0.2f, 0.8f, 0.25f, 0f));
        }

        private static ShuttleTrailPalette ClearTrail()
        {
            return new ShuttleTrailPalette(
                new Color(1f, 0.9f, 0.42f, 0.82f),
                new Color(1f, 0.82f, 0.25f, 0f));
        }

        private static ShuttleTrailPalette SmashTrail()
        {
            return new ShuttleTrailPalette(
                new Color(1f, 0.12f, 0.08f, 0.9f),
                new Color(0.85f, 0.02f, 0.02f, 0f));
        }

        private static ShuttleTrailPalette DriveTrail()
        {
            return new ShuttleTrailPalette(
                new Color(1f, 0.28f, 0.68f, 0.88f),
                new Color(0.95f, 0.12f, 0.55f, 0f));
        }
    }
}
