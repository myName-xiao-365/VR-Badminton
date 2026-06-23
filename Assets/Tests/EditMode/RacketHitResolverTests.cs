using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VRBadminton.Gameplay;

namespace VRBadminton.Tests
{
    public sealed class RacketHitResolverTests
    {
        [Test]
        public void SweetSpotDownwardSwingResolvesSmash()
        {
            RacketHitResolver resolver = new RacketHitResolver();
            RacketHitResult result = resolver.Resolve(
                RacketHistory(Vector3.zero, new Vector3(0f, -4f, 9f), false, 70f),
                ShuttleHistory(new Vector3(0f, 0f, 0.03f), new Vector3(0f, 0f, -4f)),
                Context(upward: false, incomingFrontCourt: false, speed: 3000f, faceAngle: 70f),
                Settings());

            Assert.IsTrue(result.Hit);
            Assert.AreEqual(RacketResolvedShot.Smash, result.Shot);
            Assert.IsFalse(result.AssistUsed);
            Assert.Greater(result.Quality, 0.7f);
        }

        [Test]
        public void AssistShellHitDowngradesPowerShot()
        {
            RacketHitResolver resolver = new RacketHitResolver();
            RacketHitResult result = resolver.Resolve(
                RacketHistory(Vector3.zero, new Vector3(0f, -4f, 9f), false, 70f),
                ShuttleHistory(new Vector3(0.45f, 0f, 0.03f), new Vector3(0f, 0f, -4f)),
                Context(upward: false, incomingFrontCourt: false, speed: 3000f, faceAngle: 70f),
                Settings());

            Assert.IsTrue(result.Hit);
            Assert.IsTrue(result.AssistUsed);
            Assert.AreEqual(RacketResolvedShot.Drop, result.Shot);
        }

        [Test]
        public void EarlyContactOutsideBacktrackWindowMisses()
        {
            RacketHitResolver resolver = new RacketHitResolver();
            List<RacketKinematicFrame> racket = RacketHistory(
                Vector3.zero,
                new Vector3(0f, -4f, 9f),
                false,
                70f,
                0.70f);
            List<ShuttleKinematicFrame> shuttle = ShuttleHistory(
                new Vector3(0f, 0f, 0.03f),
                new Vector3(0f, 0f, -4f),
                0.70f);

            RacketHitResult result = resolver.Resolve(
                racket,
                shuttle,
                Context(upward: false, incomingFrontCourt: false, speed: 3000f, faceAngle: 70f),
                Settings());

            Assert.IsFalse(result.Hit);
            Assert.IsFalse(result.ConsumeSwing);
            Assert.AreEqual("no racket contact", result.Reason);
        }

        [Test]
        public void ReverseSwingConsumesAndMisses()
        {
            RacketHitResolver resolver = new RacketHitResolver();
            RacketHitResult result = resolver.Resolve(
                RacketHistory(Vector3.zero, new Vector3(0f, 2f, -8f), false, 70f),
                ShuttleHistory(new Vector3(0f, 0f, 0.03f), new Vector3(0f, 0f, -4f)),
                Context(upward: false, incomingFrontCourt: false, speed: 3000f, faceAngle: 70f),
                Settings());

            Assert.IsFalse(result.Hit);
            Assert.IsTrue(result.ConsumeSwing);
            Assert.AreEqual("reverse swing", result.Reason);
        }

        [Test]
        public void UpwardFrontCourtSwingResolvesClear()
        {
            RacketHitResolver resolver = new RacketHitResolver();
            RacketHitResult result = resolver.Resolve(
                RacketHistory(Vector3.zero, new Vector3(0f, 5f, 8f), true, 62f),
                ShuttleHistory(new Vector3(0f, 0f, 0.03f), new Vector3(0f, 0f, -3f)),
                Context(upward: true, incomingFrontCourt: true, speed: 1700f, faceAngle: 62f),
                Settings());

            Assert.IsTrue(result.Hit);
            Assert.AreEqual(RacketResolvedShot.Clear, result.Shot);
            Assert.IsTrue(result.SwingUpward);
        }

        [Test]
        public void UpwardBackcourtSwingCanStillResolve()
        {
            RacketHitResolver resolver = new RacketHitResolver();
            RacketHitResult result = resolver.Resolve(
                RacketHistory(Vector3.zero, new Vector3(0f, 5f, 8f), true, 42f),
                ShuttleHistory(new Vector3(0f, 0f, 0.03f), new Vector3(0f, 0f, -3f)),
                Context(upward: true, incomingFrontCourt: false, speed: 1300f, faceAngle: 42f),
                Settings());

            Assert.IsTrue(result.Hit);
            Assert.AreNotEqual(RacketResolvedShot.Miss, result.Shot);
            Assert.IsTrue(result.SwingUpward);
        }

        [Test]
        public void BackhandPlaneCrossingCanRecoverDirectionQuality()
        {
            RacketHitResolver resolver = new RacketHitResolver();
            RacketHitResult result = resolver.Resolve(
                RacketHistory(Vector3.zero, new Vector3(0f, 4f, -7f), false, 82f),
                ShuttleHistory(new Vector3(0f, 0f, 0.03f), new Vector3(0f, 0f, -4f)),
                Context(upward: false, incomingFrontCourt: false, speed: 1500f, faceAngle: 82f, isBackhand: true),
                Settings());

            Assert.IsTrue(result.Hit);
            Assert.AreNotEqual(RacketResolvedShot.Miss, result.Shot);
        }

        [Test]
        public void HitDoesNotDependOnPlayerPosition()
        {
            RacketHitResolver resolver = new RacketHitResolver();
            RacketHitResult result = resolver.Resolve(
                RacketHistory(Vector3.zero, new Vector3(0f, -4f, 9f), false, 70f),
                ShuttleHistory(new Vector3(0f, 0f, 0.03f), new Vector3(0f, 0f, -4f)),
                Context(upward: false, incomingFrontCourt: false, speed: 3000f, faceAngle: 70f),
                Settings());

            Assert.IsTrue(result.Hit);
        }

        [Test]
        public void MagnetShellHitConnectsAndDowngrades()
        {
            RacketHitResolver resolver = new RacketHitResolver();
            RacketHitSettings settings = Settings();
            settings.MagnetRadius = 0.22f;
            RacketHitResult result = resolver.Resolve(
                RacketHistory(Vector3.zero, new Vector3(0f, -4f, 9f), false, 70f),
                ShuttleHistory(new Vector3(0.59f, 0f, 0.03f), new Vector3(0f, 0f, -4f)),
                Context(upward: false, incomingFrontCourt: false, speed: 3000f, faceAngle: 70f),
                settings);

            Assert.IsTrue(result.Hit);
            Assert.IsTrue(result.MagnetUsed);
            Assert.AreEqual(RacketResolvedShot.Drop, result.Shot);
        }

        [Test]
        public void HitResultCarriesBestCandidateWorldContactFrame()
        {
            RacketHitResolver resolver = new RacketHitResolver();
            Vector3 bestNormal = new Vector3(0.18f, 0.24f, 0.95f).normalized;
            Vector3 bestRight = Vector3.Cross(Vector3.up, bestNormal).normalized;
            Vector3 bestUp = Vector3.Cross(bestNormal, bestRight).normalized;
            Vector3 bestVelocity = new Vector3(1.2f, 2.4f, 8.6f);
            Vector3 bestSwingDirection = bestVelocity.normalized;
            List<RacketKinematicFrame> racket = new List<RacketKinematicFrame>
            {
                new RacketKinematicFrame
                {
                    Time = 0.98f,
                    FaceCenter = Vector3.zero,
                    FaceNormal = Vector3.forward,
                    FaceRight = Vector3.right,
                    FaceUp = Vector3.up,
                    FaceVelocity = new Vector3(-3f, 1f, 3f),
                    SwingDirection = new Vector3(-3f, 1f, 3f).normalized,
                    SwingSpeed = 4.4f,
                    FaceAngle = 70f,
                    TrackingConfidence = 1f,
                    SwingUpward = false
                },
                new RacketKinematicFrame
                {
                    Time = 1f,
                    FaceCenter = Vector3.zero,
                    FaceNormal = bestNormal,
                    FaceRight = bestRight,
                    FaceUp = bestUp,
                    FaceVelocity = bestVelocity,
                    SwingDirection = bestSwingDirection,
                    SwingSpeed = bestVelocity.magnitude,
                    FaceAngle = 70f,
                    TrackingConfidence = 0.86f,
                    SwingUpward = false
                }
            };
            List<ShuttleKinematicFrame> shuttle = new List<ShuttleKinematicFrame>
            {
                new ShuttleKinematicFrame
                {
                    Time = 0.98f,
                    Position = new Vector3(0.44f, 0f, 0.03f),
                    Velocity = new Vector3(0f, 0f, -4f)
                },
                new ShuttleKinematicFrame
                {
                    Time = 1f,
                    Position = bestNormal * 0.03f,
                    Velocity = new Vector3(0f, 0f, -4f)
                }
            };

            RacketHitResult result = resolver.Resolve(
                racket,
                shuttle,
                Context(upward: false, incomingFrontCourt: false, speed: 3000f, faceAngle: 70f),
                Settings());

            Assert.IsTrue(result.Hit);
            Assert.AreEqual(bestNormal.x, result.ContactFaceNormal.x, 0.001f);
            Assert.AreEqual(bestNormal.y, result.ContactFaceNormal.y, 0.001f);
            Assert.AreEqual(bestNormal.z, result.ContactFaceNormal.z, 0.001f);
            Assert.AreEqual(bestRight.x, result.ContactFaceRight.x, 0.001f);
            Assert.AreEqual(bestUp.y, result.ContactFaceUp.y, 0.001f);
            Assert.AreEqual(bestVelocity.x, result.ContactFaceVelocity.x, 0.001f);
            Assert.AreEqual(bestVelocity.z, result.ContactFaceVelocity.z, 0.001f);
            Assert.AreEqual(bestSwingDirection.z, result.ContactSwingDirection.z, 0.001f);
            Assert.AreEqual(0f, result.ContactLocalX, 0.001f);
            Assert.AreEqual(0f, result.ContactLocalY, 0.001f);
            Assert.AreEqual(0.03f, result.ContactPlaneDistance, 0.001f);
            Assert.AreEqual(0.86f, result.ContactTrackingConfidence, 0.001f);
        }

        private static RacketHitSettings Settings()
        {
            return RacketHitSettings.Default();
        }

        private static RacketHitContext Context(
            bool upward,
            bool incomingFrontCourt,
            float speed,
            float faceAngle,
            bool isBackhand = false)
        {
            return new RacketHitContext
            {
                Now = 1f,
                SwingStartedAt = 1f,
                SwingExpiresAt = 1.58f,
                SwingPending = true,
                SwingUpward = upward,
                SwingSpeed = speed,
                SwingStartAngle = faceAngle,
                IncomingFrontCourt = incomingFrontCourt,
                IncomingOpponentSmash = false,
                SmashReceiveReady = false,
                IsBackhand = isBackhand,
                MinimumSwingSpeed = 220f,
                MediumSwingSpeed = 1800f,
                FastSwingSpeed = 3600f
            };
        }

        private static List<RacketKinematicFrame> RacketHistory(
            Vector3 center,
            Vector3 velocity,
            bool upward,
            float faceAngle,
            float time = 1f)
        {
            return new List<RacketKinematicFrame>
            {
                new RacketKinematicFrame
                {
                    Time = time,
                    FaceCenter = center,
                    FaceNormal = Vector3.forward,
                    FaceRight = Vector3.right,
                    FaceUp = Vector3.up,
                    FaceVelocity = velocity,
                    SwingDirection = velocity.normalized,
                    SwingSpeed = velocity.magnitude,
                    FaceAngle = faceAngle,
                    TrackingConfidence = 1f,
                    SwingUpward = upward
                }
            };
        }

        private static List<ShuttleKinematicFrame> ShuttleHistory(
            Vector3 position,
            Vector3 velocity,
            float time = 1f)
        {
            return new List<ShuttleKinematicFrame>
            {
                new ShuttleKinematicFrame
                {
                    Time = time,
                    Position = position,
                    Velocity = velocity
                }
            };
        }
    }
}
