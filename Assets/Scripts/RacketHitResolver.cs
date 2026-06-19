using System.Collections.Generic;
using UnityEngine;

namespace VRBadminton.Gameplay
{
    public enum RacketResolvedShot
    {
        Net,
        Drop,
        Clear,
        Smash,
        Drive,
        Miss,
        Out
    }

    public struct RacketKinematicFrame
    {
        public float Time;
        public Vector3 FaceCenter;
        public Vector3 FaceNormal;
        public Vector3 FaceRight;
        public Vector3 FaceUp;
        public Vector3 FaceVelocity;
        public Vector3 SwingDirection;
        public float SwingSpeed;
        public float FaceAngle;
        public float TrackingConfidence;
        public bool SwingUpward;
    }

    public struct ShuttleKinematicFrame
    {
        public float Time;
        public Vector3 Position;
        public Vector3 Velocity;
    }

    public struct RacketHitSettings
    {
        public float SweetHalfWidth;
        public float SweetHalfHeight;
        public float AssistShell;
        public float PlaneTolerance;
        public float BacktrackSeconds;
        public float ForwardSeconds;
        public float MaxSampleGapSeconds;
        public float MinimumQuality;
        public float MinimumDirectionQuality;
        public float MinimumFaceQuality;
        public float MagnetRadius;
        public float MagnetPlaneTolerance;

        public static RacketHitSettings Default()
        {
            return new RacketHitSettings
            {
                SweetHalfWidth = 0.32f,
                SweetHalfHeight = 0.42f,
                AssistShell = 0.22f,
                PlaneTolerance = 0.28f,
                BacktrackSeconds = 0.16f,
                ForwardSeconds = 0.42f,
                MaxSampleGapSeconds = 0.085f,
                MinimumQuality = 0.46f,
                MinimumDirectionQuality = 0.35f,
                MinimumFaceQuality = 0.18f,
                MagnetRadius = 0.18f,
                MagnetPlaneTolerance = 0.12f
            };
        }
    }

    public struct RacketHitContext
    {
        public float Now;
        public float SwingStartedAt;
        public float SwingExpiresAt;
        public bool SwingPending;
        public bool SwingUpward;
        public float SwingSpeed;
        public float SwingStartAngle;
        public bool IncomingFrontCourt;
        public bool IncomingOpponentSmash;
        public bool SmashReceiveReady;
        public bool IsBackhand;
        public float MinimumSwingSpeed;
        public float MediumSwingSpeed;
        public float FastSwingSpeed;
    }

    public struct RacketHitResult
    {
        public bool Hit;
        public bool ConsumeSwing;
        public RacketResolvedShot Shot;
        public Vector3 ContactPoint;
        public float ContactTime;
        public float Quality;
        public float SpatialQuality;
        public float TimingQuality;
        public float DirectionQuality;
        public float FaceQuality;
        public float PowerQuality;
        public float SweetSpot01;
        public bool AssistUsed;
        public bool MagnetUsed;
        public bool SwingUpward;
        public float SwingSpeed;
        public float FaceAngle;
        public string Reason;

        public static RacketHitResult Miss(string reason, bool consumeSwing)
        {
            return new RacketHitResult
            {
                Hit = false,
                ConsumeSwing = consumeSwing,
                Shot = RacketResolvedShot.Miss,
                Reason = reason
            };
        }
    }

    public sealed class RacketHitResolver
    {
        public RacketHitResult Resolve(
            IReadOnlyList<RacketKinematicFrame> racketHistory,
            IReadOnlyList<ShuttleKinematicFrame> shuttleHistory,
            RacketHitContext context,
            RacketHitSettings settings)
        {
            if (!context.SwingPending)
            {
                return RacketHitResult.Miss("no swing", false);
            }

            if (context.IncomingOpponentSmash && !context.SmashReceiveReady)
            {
                return RacketHitResult.Miss("smash receive not ready", false);
            }

            if (racketHistory == null || racketHistory.Count == 0 ||
                shuttleHistory == null || shuttleHistory.Count == 0)
            {
                return RacketHitResult.Miss("missing hit history", false);
            }

            settings = SanitizeSettings(settings);
            Candidate best = default;
            bool found = false;
            float fromTime = Mathf.Max(
                0f,
                context.SwingStartedAt - settings.BacktrackSeconds);
            float toTime = Mathf.Max(context.Now, context.SwingStartedAt);

            if (context.SwingExpiresAt > 0f)
            {
                toTime = Mathf.Min(toTime, context.SwingExpiresAt);
            }

            for (int i = 0; i < racketHistory.Count; i++)
            {
                RacketKinematicFrame racketFrame = racketHistory[i];
                if (racketFrame.Time < fromTime || racketFrame.Time > toTime)
                {
                    continue;
                }

                if (!TrySampleShuttleAt(
                    shuttleHistory,
                    racketFrame.Time,
                    settings.MaxSampleGapSeconds,
                    out ShuttleKinematicFrame shuttleFrame))
                {
                    continue;
                }

                if (!TryEvaluateCandidate(
                    racketFrame,
                    shuttleFrame,
                    context,
                    settings,
                    out Candidate candidate))
                {
                    continue;
                }

                if (!found || candidate.Quality > best.Quality)
                {
                    best = candidate;
                    found = true;
                }
            }

            if (!found)
            {
                return RacketHitResult.Miss("no racket contact", false);
            }

            if (best.DirectionQuality < settings.MinimumDirectionQuality)
            {
                return BuildResult(best, false, RacketResolvedShot.Miss, "reverse swing", true);
            }

            if (best.FaceQuality < settings.MinimumFaceQuality)
            {
                return BuildResult(best, false, RacketResolvedShot.Miss, "wrong swing shape", true);
            }

            if (best.Quality < settings.MinimumQuality)
            {
                return BuildResult(best, false, RacketResolvedShot.Miss, "weak contact", true);
            }

            RacketResolvedShot shot = ResolveShot(best, context);
            if (shot == RacketResolvedShot.Miss)
            {
                return BuildResult(best, false, shot, "unplayable contact", true);
            }

            string reason = best.MagnetUsed
                ? "magnet contact"
                : best.AssistUsed ? "assist contact" : "sweet contact";
            return BuildResult(best, true, shot, reason, true);
        }

        private static bool TryEvaluateCandidate(
            RacketKinematicFrame racketFrame,
            ShuttleKinematicFrame shuttleFrame,
            RacketHitContext context,
            RacketHitSettings settings,
            out Candidate candidate)
        {
            candidate = default;
            float trackingConfidence = Mathf.Clamp01(racketFrame.TrackingConfidence);
            if (trackingConfidence <= 0.001f)
            {
                return false;
            }

            Vector3 normal = NormalizeOrFallback(racketFrame.FaceNormal, Vector3.forward);
            Vector3 right = NormalizeOrFallback(racketFrame.FaceRight, Vector3.right);
            Vector3 up = NormalizeOrFallback(racketFrame.FaceUp, Vector3.up);
            Vector3 toShuttle = shuttleFrame.Position - racketFrame.FaceCenter;
            float signedPlaneDistance = Vector3.Dot(toShuttle, normal);
            float planeDistance = Mathf.Abs(signedPlaneDistance);
            float magnetPlaneTolerance = settings.PlaneTolerance + settings.MagnetPlaneTolerance;
            bool magnetUsed = planeDistance > settings.PlaneTolerance;
            if (planeDistance > magnetPlaneTolerance)
            {
                return false;
            }

            float localX = Vector3.Dot(toShuttle, right);
            float localY = Vector3.Dot(toShuttle, up);
            float sweetNorm = Mathf.Sqrt(
                Mathf.Pow(localX / settings.SweetHalfWidth, 2f) +
                Mathf.Pow(localY / settings.SweetHalfHeight, 2f));
            bool insideSweetSpot = sweetNorm <= 1f;
            float outsideDistance = 0f;
            if (!insideSweetSpot)
            {
                float outsideX = Mathf.Max(0f, Mathf.Abs(localX) - settings.SweetHalfWidth);
                float outsideY = Mathf.Max(0f, Mathf.Abs(localY) - settings.SweetHalfHeight);
                outsideDistance = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY);
                float magnetShell = settings.AssistShell + settings.MagnetRadius;
                if (outsideDistance > magnetShell)
                {
                    return false;
                }

                magnetUsed = magnetUsed || outsideDistance > settings.AssistShell;
            }

            float radialQuality = insideSweetSpot
                ? Mathf.Lerp(1f, 0.78f, Mathf.Clamp01(sweetNorm))
                : outsideDistance <= settings.AssistShell
                    ? Mathf.Lerp(0.64f, 0.34f, Mathf.Clamp01(outsideDistance / settings.AssistShell))
                    : Mathf.Lerp(
                        0.30f,
                        0.18f,
                        Mathf.Clamp01((outsideDistance - settings.AssistShell) / settings.MagnetRadius));
            float planeQuality = 1f - Mathf.Clamp01(planeDistance / magnetPlaneTolerance);
            float spatialQuality = Mathf.Clamp01(radialQuality * Mathf.Lerp(0.62f, 1f, planeQuality));
            if (magnetUsed)
            {
                spatialQuality = Mathf.Min(spatialQuality + 0.08f, 0.42f);
            }
            float timingQuality = TimingQuality(racketFrame.Time, context, settings);
            float directionQuality = DirectionQuality(
                racketFrame,
                shuttleFrame,
                toShuttle,
                normal,
                context);
            if (context.SwingUpward || context.IsBackhand)
            {
                directionQuality = Mathf.Max(directionQuality, DirectionRecoveryQuality(racketFrame, shuttleFrame, normal));
            }

            float faceQuality = FaceQuality(racketFrame, context);
            float powerQuality = Mathf.InverseLerp(
                Mathf.Max(1f, context.MinimumSwingSpeed * 0.45f),
                Mathf.Max(context.MinimumSwingSpeed + 1f, context.FastSwingSpeed),
                context.SwingSpeed);
            float quality =
                spatialQuality * 0.33f +
                timingQuality * 0.19f +
                directionQuality * 0.20f +
                faceQuality * 0.17f +
                powerQuality * 0.11f;
            quality *= Mathf.Lerp(0.72f, 1f, trackingConfidence);

            candidate = new Candidate
            {
                ContactPoint = shuttleFrame.Position - normal * signedPlaneDistance,
                ContactTime = racketFrame.Time,
                Quality = Mathf.Clamp01(quality),
                SpatialQuality = spatialQuality,
                TimingQuality = timingQuality,
                DirectionQuality = directionQuality,
                FaceQuality = faceQuality,
                PowerQuality = powerQuality,
                SweetSpot01 = Mathf.Clamp01(sweetNorm),
                AssistUsed = !insideSweetSpot,
                MagnetUsed = magnetUsed,
                SwingUpward = context.SwingUpward,
                SwingSpeed = context.SwingSpeed,
                FaceAngle = racketFrame.FaceAngle
            };
            return true;
        }

        private static RacketResolvedShot ResolveShot(Candidate candidate, RacketHitContext context)
        {
            float quality = candidate.Quality;
            float speed = candidate.SwingSpeed;
            float faceAngle = candidate.FaceAngle;
            float mediumSpeed = Mathf.Max(context.MinimumSwingSpeed + 1f, context.MediumSwingSpeed);
            bool strong = speed >= mediumSpeed * 0.75f || candidate.PowerQuality >= 0.55f;

            if (candidate.MagnetUsed)
            {
                return context.IncomingFrontCourt
                    ? RacketResolvedShot.Net
                    : RacketResolvedShot.Drop;
            }

            if (context.IncomingOpponentSmash)
            {
                return quality >= 0.62f || strong
                    ? RacketResolvedShot.Clear
                    : RacketResolvedShot.Drop;
            }

            if (candidate.AssistUsed && quality < 0.86f)
            {
                return RacketResolvedShot.Drop;
            }

            if (context.SwingUpward)
            {
                if (!context.IncomingFrontCourt)
                {
                    if (quality < 0.48f || speed < context.MinimumSwingSpeed * 0.72f)
                    {
                        return RacketResolvedShot.Drop;
                    }

                    return RacketResolvedShot.Clear;
                }

                if (quality < 0.52f || speed < context.MinimumSwingSpeed)
                {
                    return RacketResolvedShot.Drop;
                }

                if (faceAngle >= -35f && faceAngle <= 10f && !strong)
                {
                    return RacketResolvedShot.Drop;
                }

                return RacketResolvedShot.Clear;
            }

            if (context.IncomingFrontCourt)
            {
                return RacketResolvedShot.Miss;
            }

            if (faceAngle > 118f && strong && quality < 0.72f)
            {
                return RacketResolvedShot.Out;
            }

            if (quality < 0.50f)
            {
                return RacketResolvedShot.Drop;
            }

            if (faceAngle >= 58f && faceAngle <= 88f && strong && quality >= 0.58f)
            {
                return RacketResolvedShot.Smash;
            }

            if (faceAngle > 82f && faceAngle <= 104f && quality >= 0.54f)
            {
                return RacketResolvedShot.Drive;
            }

            if (faceAngle > 95f)
            {
                return strong && quality >= 0.58f
                    ? RacketResolvedShot.Clear
                    : RacketResolvedShot.Drop;
            }

            if (strong && quality >= 0.66f)
            {
                return RacketResolvedShot.Smash;
            }

            return RacketResolvedShot.Drop;
        }

        private static float TimingQuality(
            float sampleTime,
            RacketHitContext context,
            RacketHitSettings settings)
        {
            float swingTime = context.SwingStartedAt > 0f
                ? context.SwingStartedAt
                : context.Now;
            float delta = sampleTime - swingTime;
            float window = delta < 0f
                ? settings.BacktrackSeconds
                : Mathf.Max(settings.ForwardSeconds, context.SwingExpiresAt - swingTime);
            return 1f - Mathf.Clamp01(Mathf.Abs(delta) / Mathf.Max(0.001f, window));
        }

        private static float DirectionQuality(
            RacketKinematicFrame racketFrame,
            ShuttleKinematicFrame shuttleFrame,
            Vector3 toShuttle,
            Vector3 normal,
            RacketHitContext context)
        {
            Vector3 intendedDirection = racketFrame.SwingDirection.sqrMagnitude > 0.0001f
                ? racketFrame.SwingDirection.normalized
                : DefaultSwingDirection(context.SwingUpward);
            Vector3 faceVelocity = racketFrame.FaceVelocity;
            if (faceVelocity.sqrMagnitude < 0.04f)
            {
                faceVelocity = intendedDirection * Mathf.Max(1f, context.SwingSpeed * 0.003f);
            }

            Vector3 towardShuttle = toShuttle.sqrMagnitude > 0.0001f
                ? toShuttle.normalized
                : normal;
            float towardQuality = Mathf.InverseLerp(
                -0.15f,
                0.80f,
                Vector3.Dot(faceVelocity.normalized, towardShuttle));
            Vector3 relativeVelocity = faceVelocity - shuttleFrame.Velocity;
            float planeCrossQuality = relativeVelocity.sqrMagnitude > 0.0001f
                ? Mathf.InverseLerp(0.05f, 0.70f, Mathf.Abs(Vector3.Dot(relativeVelocity.normalized, normal)))
                : 0f;
            return Mathf.Clamp01(towardQuality * 0.70f + planeCrossQuality * 0.30f);
        }

        private static float DirectionRecoveryQuality(
            RacketKinematicFrame racketFrame,
            ShuttleKinematicFrame shuttleFrame,
            Vector3 normal)
        {
            Vector3 relativeVelocity = racketFrame.FaceVelocity - shuttleFrame.Velocity;
            if (relativeVelocity.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            float planeCrossQuality = Mathf.InverseLerp(
                0.02f,
                0.58f,
                Mathf.Abs(Vector3.Dot(relativeVelocity.normalized, normal)));
            return planeCrossQuality * 0.88f;
        }

        private static float FaceQuality(RacketKinematicFrame racketFrame, RacketHitContext context)
        {
            float faceAngle = racketFrame.FaceAngle;
            if (context.IncomingOpponentSmash)
            {
                if (context.SwingUpward)
                {
                    return 1f;
                }

                return Mathf.InverseLerp(20f, 75f, faceAngle) * 0.70f;
            }

            if (context.IncomingFrontCourt)
            {
                if (!context.SwingUpward)
                {
                    return 0.05f;
                }

                if (faceAngle >= -40f && faceAngle <= 85f)
                {
                    return Mathf.Clamp01(1f - Mathf.Abs(faceAngle - 25f) / 120f);
                }

                return 0.25f;
            }

            if (context.SwingUpward)
            {
                if (faceAngle >= -45f && faceAngle <= 105f)
                {
                    return Mathf.Clamp01(0.82f - Mathf.Abs(faceAngle - 30f) / 150f);
                }

                return 0.32f;
            }

            if (faceAngle >= 55f && faceAngle <= 115f)
            {
                return Mathf.Clamp01(1f - Mathf.Abs(faceAngle - 82f) / 80f);
            }

            if (faceAngle > 115f)
            {
                return 0.45f;
            }

            return 0.15f;
        }

        private static RacketHitResult BuildResult(
            Candidate candidate,
            bool hit,
            RacketResolvedShot shot,
            string reason,
            bool consumeSwing)
        {
            return new RacketHitResult
            {
                Hit = hit,
                ConsumeSwing = consumeSwing,
                Shot = shot,
                ContactPoint = candidate.ContactPoint,
                ContactTime = candidate.ContactTime,
                Quality = candidate.Quality,
                SpatialQuality = candidate.SpatialQuality,
                TimingQuality = candidate.TimingQuality,
                DirectionQuality = candidate.DirectionQuality,
                FaceQuality = candidate.FaceQuality,
                PowerQuality = candidate.PowerQuality,
                SweetSpot01 = candidate.SweetSpot01,
                AssistUsed = candidate.AssistUsed,
                MagnetUsed = candidate.MagnetUsed,
                SwingUpward = candidate.SwingUpward,
                SwingSpeed = candidate.SwingSpeed,
                FaceAngle = candidate.FaceAngle,
                Reason = reason
            };
        }

        private static bool TrySampleShuttleAt(
            IReadOnlyList<ShuttleKinematicFrame> history,
            float time,
            float maxGap,
            out ShuttleKinematicFrame sample)
        {
            sample = default;
            if (history.Count == 0)
            {
                return false;
            }

            ShuttleKinematicFrame first = history[0];
            if (time <= first.Time)
            {
                if (first.Time - time > maxGap)
                {
                    return false;
                }

                sample = first;
                return true;
            }

            for (int i = 1; i < history.Count; i++)
            {
                ShuttleKinematicFrame previous = history[i - 1];
                ShuttleKinematicFrame next = history[i];
                if (time > next.Time)
                {
                    continue;
                }

                float span = next.Time - previous.Time;
                if (span > maxGap)
                {
                    return false;
                }

                float t = span <= 0.0001f ? 0f : Mathf.Clamp01((time - previous.Time) / span);
                sample = new ShuttleKinematicFrame
                {
                    Time = time,
                    Position = Vector3.Lerp(previous.Position, next.Position, t),
                    Velocity = Vector3.Lerp(previous.Velocity, next.Velocity, t)
                };
                return true;
            }

            ShuttleKinematicFrame last = history[history.Count - 1];
            if (time - last.Time > maxGap)
            {
                return false;
            }

            sample = last;
            return true;
        }

        private static RacketHitSettings SanitizeSettings(RacketHitSettings settings)
        {
            RacketHitSettings fallback = RacketHitSettings.Default();
            settings.SweetHalfWidth = settings.SweetHalfWidth > 0f ? settings.SweetHalfWidth : fallback.SweetHalfWidth;
            settings.SweetHalfHeight = settings.SweetHalfHeight > 0f ? settings.SweetHalfHeight : fallback.SweetHalfHeight;
            settings.AssistShell = settings.AssistShell > 0f ? settings.AssistShell : fallback.AssistShell;
            settings.PlaneTolerance = settings.PlaneTolerance > 0f ? settings.PlaneTolerance : fallback.PlaneTolerance;
            settings.BacktrackSeconds = settings.BacktrackSeconds > 0f ? settings.BacktrackSeconds : fallback.BacktrackSeconds;
            settings.ForwardSeconds = settings.ForwardSeconds > 0f ? settings.ForwardSeconds : fallback.ForwardSeconds;
            settings.MaxSampleGapSeconds = settings.MaxSampleGapSeconds > 0f ? settings.MaxSampleGapSeconds : fallback.MaxSampleGapSeconds;
            settings.MinimumQuality = Mathf.Clamp01(settings.MinimumQuality > 0f ? settings.MinimumQuality : fallback.MinimumQuality);
            settings.MinimumDirectionQuality = Mathf.Clamp01(settings.MinimumDirectionQuality > 0f
                ? settings.MinimumDirectionQuality
                : fallback.MinimumDirectionQuality);
            settings.MinimumFaceQuality = Mathf.Clamp01(settings.MinimumFaceQuality > 0f
                ? settings.MinimumFaceQuality
                : fallback.MinimumFaceQuality);
            settings.MagnetRadius = settings.MagnetRadius > 0f ? settings.MagnetRadius : fallback.MagnetRadius;
            settings.MagnetPlaneTolerance = settings.MagnetPlaneTolerance > 0f
                ? settings.MagnetPlaneTolerance
                : fallback.MagnetPlaneTolerance;
            return settings;
        }

        private static Vector3 DefaultSwingDirection(bool upward)
        {
            Vector3 vertical = upward ? Vector3.up : Vector3.down;
            return (Vector3.forward * 0.74f + vertical * 0.67f).normalized;
        }

        private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
        {
            return value.sqrMagnitude > 0.0001f ? value.normalized : fallback;
        }

        private struct Candidate
        {
            public Vector3 ContactPoint;
            public float ContactTime;
            public float Quality;
            public float SpatialQuality;
            public float TimingQuality;
            public float DirectionQuality;
            public float FaceQuality;
            public float PowerQuality;
            public float SweetSpot01;
            public bool AssistUsed;
            public bool MagnetUsed;
            public bool SwingUpward;
            public float SwingSpeed;
            public float FaceAngle;
        }
    }
}
