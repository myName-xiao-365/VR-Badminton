using UnityEngine;
using VRBadminton.Gameplay;

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
            ShuttleFeedController.ShotType shot,
            Vector3 target,
            float duration,
            float arcHeight,
            float apexT,
            float rawPower01 = 0f,
            float effectivePower01 = 0f,
            float contactRetention = 1f,
            float aim01 = 0f,
            float aimYawDegrees = 0f,
            float elevation01 = 0f,
            float lift01 = 0f,
            float attack01 = 0f,
            bool usesHighArc = false)
        {
            Shot = shot;
            Target = target;
            Duration = duration;
            ArcHeight = arcHeight;
            ApexT = apexT;
            RawPower01 = rawPower01;
            EffectivePower01 = effectivePower01;
            ContactRetention = contactRetention;
            Aim01 = aim01;
            AimYawDegrees = aimYawDegrees;
            Elevation01 = elevation01;
            Lift01 = lift01;
            Attack01 = attack01;
            UsesHighArc = usesHighArc;
        }

        public ShuttleFeedController.ShotType Shot { get; }

        public Vector3 Target { get; }

        public float Duration { get; }

        public float ArcHeight { get; }

        public float ApexT { get; }

        public float RawPower01 { get; }

        public float EffectivePower01 { get; }

        public float ContactRetention { get; }

        public float Aim01 { get; }

        public float AimYawDegrees { get; }

        public float Elevation01 { get; }

        public float Lift01 { get; }

        public float Attack01 { get; }

        public bool UsesHighArc { get; }
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
                        shot,
                        new Vector3(
                            Random.Range(-2.2f, 2.2f),
                            0.09f,
                            Random.Range(1.55f, 2.15f) * courtLengthScale),
                        1.1f,
                        1.35f,
                        0.5f);

                case ShuttleFeedController.ShotType.Clear:
                    return new ShuttlePlayerReturnPlan(
                        shot,
                        new Vector3(
                            Random.Range(-2.3f, 2.3f),
                            0.09f,
                            6.2f * courtLengthScale),
                        2f,
                        4.5f,
                        0.7f,
                        usesHighArc: true);

                case ShuttleFeedController.ShotType.Smash:
                    return new ShuttlePlayerReturnPlan(
                        shot,
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
                        0.18f,
                        0.5f);

                case ShuttleFeedController.ShotType.Drive:
                    return new ShuttlePlayerReturnPlan(
                        shot,
                        new Vector3(
                            Random.Range(-2.3f, 2.3f),
                            0.09f,
                            6.45f * courtLengthScale),
                        0.82f,
                        0.55f,
                        0.5f);

                case ShuttleFeedController.ShotType.Out:
                    return new ShuttlePlayerReturnPlan(
                        shot,
                        new Vector3(
                            Random.Range(-3.8f, 3.8f),
                            0.09f,
                            8.25f * courtLengthScale),
                        1.25f,
                        1.5f,
                        0.5f);

                default:
                    return new ShuttlePlayerReturnPlan(
                        shot,
                        new Vector3(
                            Random.Range(-1.9f, 1.9f),
                            0.09f,
                            Random.Range(1.45f, 1.95f) * courtLengthScale),
                        1.05f,
                        1.35f,
                        0.5f);
            }
        }

        public ShuttlePlayerReturnPlan CreateSensorPlayerReturnPlan(
            Vector3 start,
            RacketHitResult hit,
            float powerSwingSpeed,
            float courtLengthScale,
            float mediumSwingSpeed,
            float fastSwingSpeed,
            float powerExponent,
            float assistPowerRetention,
            float magnetPowerRetention,
            float maxAimYawDegrees,
            bool jumpSmash)
        {
            float lowSpeed = Mathf.Max(1f, mediumSwingSpeed * 0.78f);
            float highSpeed = Mathf.Max(lowSpeed + 1f, fastSwingSpeed * 1.18f);
            float normalizedPower = Mathf.InverseLerp(
                lowSpeed,
                highSpeed,
                Mathf.Max(0f, powerSwingSpeed));
            float rawPower01 = Mathf.Pow(
                Mathf.Clamp01(normalizedPower),
                Mathf.Max(0.1f, powerExponent));

            float contactRetention = hit.MagnetUsed
                ? magnetPowerRetention
                : hit.AssistUsed ? assistPowerRetention : 1f;
            contactRetention = Mathf.Clamp01(contactRetention);
            contactRetention *= Mathf.Lerp(0.94f, 1f, Mathf.Clamp01(hit.Quality));
            float effectivePower01 = Mathf.Clamp01(rawPower01 * contactRetention);

            Vector3 normal = NormalizeOrFallback(hit.ContactFaceNormal, Vector3.forward);
            if (normal.z < 0f)
            {
                normal = -normal;
            }

            Vector3 velocityGround = Vector3.ProjectOnPlane(hit.ContactFaceVelocity, Vector3.up);
            Vector3 normalGround = Vector3.ProjectOnPlane(normal, Vector3.up);
            float velocityReliability =
                Mathf.InverseLerp(0.4f, 6f, velocityGround.magnitude) *
                Mathf.InverseLerp(-0.05f, 0.3f, velocityGround.z);
            float normalReliability = Mathf.InverseLerp(0.08f, 0.55f, normalGround.z);
            float aim01 = WeightedAim(
                LateralIntent(velocityGround),
                velocityReliability,
                0.65f,
                LateralIntent(normalGround),
                normalReliability,
                0.35f);
            aim01 = ApplyAimDeadZone(aim01, 0.08f);

            float aimAuthority = Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(hit.Quality));
            if (hit.MagnetUsed)
            {
                aimAuthority *= 0.78f;
            }
            else if (hit.AssistUsed)
            {
                aimAuthority *= 0.90f;
            }

            aimAuthority = Mathf.Clamp01(aimAuthority);
            float aimYawDegrees = aim01 * Mathf.Max(0f, maxAimYawDegrees);

            Vector3 velocityDirection = hit.ContactFaceVelocity.sqrMagnitude > 0.0001f
                ? hit.ContactFaceVelocity.normalized
                : DefaultVelocityDirection(hit.SwingUpward);
            float motionUp01 = Mathf.InverseLerp(-0.7f, 0.7f, velocityDirection.y);
            float faceLoft01 = Mathf.InverseLerp(-0.35f, 0.65f, normal.y);
            float upward01 = hit.SwingUpward ? 1f : 0f;
            float elevation01 = Mathf.Clamp01(
                0.60f * upward01 +
                0.25f * motionUp01 +
                0.15f * faceLoft01);
            float liftGateT = Mathf.InverseLerp(0.42f, 0.82f, effectivePower01);
            float liftGate = Mathf.SmoothStep(0f, 1f, liftGateT);
            float lift01 = Mathf.Clamp01(elevation01 * liftGate);

            float downwardMotion01 = Mathf.InverseLerp(0.05f, 0.75f, -velocityDirection.y);
            float highContact01 = Mathf.InverseLerp(1.25f, 2.35f, hit.ContactPoint.y);
            float attack01 = Mathf.Clamp01(
                (1f - upward01) *
                effectivePower01 *
                Mathf.Lerp(0.45f, 1f, downwardMotion01) *
                Mathf.Lerp(0.55f, 1f, highContact01));
            if (!jumpSmash)
            {
                attack01 = Mathf.Min(attack01, 0.34f);
            }

            float nearZ = 1.55f * courtLengthScale;
            float farZ = 6.25f * courtLengthScale;
            float depth01;
            if (hit.SwingUpward)
            {
                float shortDepth01 = Mathf.Lerp(0f, 0.16f, effectivePower01);
                float deepDepth01 = Mathf.Lerp(0.60f, 1f, effectivePower01);
                depth01 = Mathf.Lerp(shortDepth01, deepDepth01, lift01);
            }
            else
            {
                float driveDepth01 = Mathf.Lerp(0.42f, 0.86f, effectivePower01);
                float smashDepth01 = Mathf.Lerp(0.36f, 0.70f, effectivePower01);
                depth01 = Mathf.Lerp(driveDepth01, smashDepth01, attack01);
            }

            float targetZ = Mathf.Lerp(nearZ, farZ, Mathf.Clamp01(depth01));
            float desiredXFromYaw = start.x +
                Mathf.Tan(aimYawDegrees * Mathf.Deg2Rad) *
                (targetZ - start.z);
            float safeX = start.x * 0.35f;
            float targetX = Mathf.Clamp(
                Mathf.Lerp(safeX, desiredXFromYaw, aimAuthority),
                -2.45f,
                2.45f);
            Vector3 target = new Vector3(targetX, 0.09f, targetZ);

            float drive01 = Mathf.Clamp01(
                (1f - upward01) *
                (1f - attack01) *
                effectivePower01);
            float softArc = Mathf.Lerp(1.15f, 0.65f, drive01);
            float highArc = Mathf.Lerp(1.55f, 5.15f, lift01);
            float attackingArc = Mathf.Lerp(softArc, 0.16f, attack01);
            float arcHeight = Mathf.Lerp(attackingArc, highArc, lift01);
            float apexT = Mathf.Lerp(0.5f, 0.7f, lift01);
            float netSafety01 = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.30f, 0.70f, effectivePower01));
            float guardedMinimumNetY = Mathf.Lerp(1.62f, 1.56f, attack01);
            float minimumNetY = Mathf.Lerp(1.34f, guardedMinimumNetY, netSafety01);
            arcHeight = Mathf.Max(
                arcHeight,
                RequiredArcHeightForNetClearance(start, target, apexT, minimumNetY));

            float duration =
                Mathf.Lerp(1.38f, 0.62f, effectivePower01) +
                lift01 * 0.95f -
                attack01 * 0.18f;
            duration = Mathf.Clamp(duration, 0.42f, 2.35f);

            ShuttleFeedController.ShotType shot = ClassifySensorShot(
                hit,
                courtLengthScale,
                target.z,
                arcHeight,
                effectivePower01,
                lift01,
                attack01,
                jumpSmash);

            return new ShuttlePlayerReturnPlan(
                shot,
                target,
                duration,
                arcHeight,
                apexT,
                rawPower01,
                effectivePower01,
                contactRetention,
                aim01,
                aimYawDegrees,
                elevation01,
                lift01,
                attack01,
                arcHeight >= 3f);
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

        public static float RequiredArcHeightForNetClearance(
            Vector3 start,
            Vector3 target,
            float apexT,
            float minimumNetY)
        {
            float deltaZ = target.z - start.z;
            if (Mathf.Abs(deltaZ) <= 0.0001f)
            {
                return 0f;
            }

            float tNet = -start.z / deltaZ;
            if (tNet <= 0f || tNet >= 1f)
            {
                return 0f;
            }

            float clampedApexT = Mathf.Clamp(apexT, 0.2f, 0.8f);
            float denominator = tNet <= clampedApexT
                ? clampedApexT
                : 1f - clampedApexT;
            if (denominator <= 0.0001f)
            {
                return 0f;
            }

            float baseY = Mathf.Lerp(start.y, target.y, tNet);
            float normalized = tNet <= clampedApexT
                ? (tNet - clampedApexT) / clampedApexT
                : (tNet - clampedApexT) / (1f - clampedApexT);
            float coefficient = 1f - normalized * normalized;
            if (coefficient <= 0.0001f)
            {
                return 0f;
            }

            return Mathf.Max(0f, (minimumNetY - baseY) / coefficient);
        }

        private static ShuttleFeedController.ShotType ClassifySensorShot(
            RacketHitResult hit,
            float courtLengthScale,
            float targetZ,
            float arcHeight,
            float effectivePower01,
            float lift01,
            float attack01,
            bool jumpSmash)
        {
            if (jumpSmash &&
                attack01 >= 0.50f &&
                effectivePower01 >= 0.52f &&
                hit.ContactPoint.y >= 1.45f)
            {
                return ShuttleFeedController.ShotType.Smash;
            }

            if (lift01 >= 0.55f &&
                targetZ >= 4.65f * courtLengthScale &&
                arcHeight >= 3f)
            {
                return ShuttleFeedController.ShotType.Clear;
            }

            if (!hit.SwingUpward &&
                effectivePower01 >= 0.45f &&
                arcHeight <= 1.05f)
            {
                return ShuttleFeedController.ShotType.Drive;
            }

            return targetZ <= 2.85f * courtLengthScale
                ? ShuttleFeedController.ShotType.Net
                : ShuttleFeedController.ShotType.Drop;
        }

        private static float WeightedAim(
            float velocityLateral,
            float velocityReliability,
            float velocityWeight,
            float normalLateral,
            float normalReliability,
            float normalWeight)
        {
            float weightedAim = 0f;
            float totalWeight = 0f;
            if (velocityReliability > 0f)
            {
                float weight = velocityWeight * velocityReliability;
                weightedAim += velocityLateral * weight;
                totalWeight += weight;
            }

            if (normalReliability > 0f)
            {
                float weight = normalWeight * normalReliability;
                weightedAim += normalLateral * weight;
                totalWeight += weight;
            }

            return totalWeight > 0.0001f
                ? Mathf.Clamp(weightedAim / totalWeight, -1f, 1f)
                : 0f;
        }

        private static float ApplyAimDeadZone(float aim01, float deadZone)
        {
            float magnitude = Mathf.Abs(aim01);
            if (magnitude <= deadZone)
            {
                return 0f;
            }

            return Mathf.Sign(aim01) * Mathf.InverseLerp(deadZone, 1f, magnitude);
        }

        private static float LateralIntent(Vector3 value)
        {
            return value.x /
                (Mathf.Abs(value.x) + Mathf.Abs(value.z) + 0.001f);
        }

        private static Vector3 DefaultVelocityDirection(bool upward)
        {
            Vector3 vertical = upward ? Vector3.up : Vector3.down;
            return (Vector3.forward + vertical).normalized;
        }

        private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
        {
            return value.sqrMagnitude > 0.0001f
                ? value.normalized
                : fallback;
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
