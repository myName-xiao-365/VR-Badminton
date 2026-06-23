using NUnit.Framework;
using UnityEngine;
using VRBadminton.App;
using VRBadminton.Gameplay;

namespace VRBadminton.Tests
{
    public sealed class ShuttleReturnPlannerTests
    {
        private const float CourtLengthScale = 0.95f;
        private const float MediumSwingSpeed = 1800f;
        private const float FastSwingSpeed = 3600f;

        [Test]
        public void SensorPlanIsDeterministicForSameInput()
        {
            ShuttleReturnPlanner planner = new ShuttleReturnPlanner();
            Vector3 start = new Vector3(0.2f, 1.4f, -5.2f);
            RacketHitResult hit = UpwardHit();

            ShuttlePlayerReturnPlan first = SensorPlan(planner, start, hit, 4200f);
            ShuttlePlayerReturnPlan second = SensorPlan(planner, start, hit, 4200f);

            Assert.AreEqual(first.Shot, second.Shot);
            AssertVectorNearlyEqual(first.Target, second.Target);
            Assert.AreEqual(first.Duration, second.Duration, 0.0001f);
            Assert.AreEqual(first.ArcHeight, second.ArcHeight, 0.0001f);
            Assert.AreEqual(first.ApexT, second.ApexT, 0.0001f);
            Assert.AreEqual(first.AimYawDegrees, second.AimYawDegrees, 0.0001f);
        }

        [Test]
        public void StrongUpwardMagnetCanStillReachBackCourt()
        {
            ShuttleReturnPlanner planner = new ShuttleReturnPlanner();
            RacketHitResult hit = UpwardHit();
            hit.AssistUsed = true;
            hit.MagnetUsed = true;

            ShuttlePlayerReturnPlan plan = SensorPlan(
                planner,
                new Vector3(0f, 1.45f, -5.4f),
                hit,
                5200f);

            Assert.AreEqual(ShuttleFeedController.ShotType.Clear, plan.Shot);
            Assert.GreaterOrEqual(plan.Target.z, 4.65f * CourtLengthScale);
            Assert.GreaterOrEqual(plan.ArcHeight, 3f);
            Assert.IsTrue(plan.UsesHighArc);
            Assert.Greater(plan.EffectivePower01, 0.62f);
        }

        [Test]
        public void BackcourtJumpDownwardSwingPlansSmash()
        {
            ShuttleReturnPlanner planner = new ShuttleReturnPlanner();
            RacketHitResult hit = DownwardHit();

            ShuttlePlayerReturnPlan plan = SensorPlan(
                planner,
                new Vector3(0f, 2.05f, -5.2f),
                hit,
                5200f,
                jumpSmash: true);

            Assert.AreEqual(ShuttleFeedController.ShotType.Smash, plan.Shot);
            Assert.GreaterOrEqual(plan.Attack01, 0.5f);
            Assert.LessOrEqual(plan.Duration, 0.8f);
            Assert.LessOrEqual(plan.ArcHeight, 1.05f);
        }

        [Test]
        public void BackcourtDownwardSwingWithoutJumpDoesNotPlanSmash()
        {
            ShuttleReturnPlanner planner = new ShuttleReturnPlanner();
            RacketHitResult hit = DownwardHit();

            ShuttlePlayerReturnPlan plan = SensorPlan(
                planner,
                new Vector3(0f, 2.05f, -5.2f),
                hit,
                5200f,
                jumpSmash: false);

            Assert.AreNotEqual(ShuttleFeedController.ShotType.Smash, plan.Shot);
            Assert.Less(plan.Attack01, 0.5f);
        }

        [Test]
        public void LightUpwardSwingCanStayFrontCourtAndHitNet()
        {
            ShuttleReturnPlanner planner = new ShuttleReturnPlanner();
            RacketHitResult hit = UpwardHit();
            Vector3 start = new Vector3(0f, 1.35f, -5.2f);

            ShuttlePlayerReturnPlan plan = SensorPlan(
                planner,
                start,
                hit,
                1500f);
            ShuttleTrajectory trajectory = ShuttleTrajectoryPlanner.Create(
                start,
                plan.Target,
                plan.Duration,
                plan.ArcHeight,
                plan.ApexT);
            float tNet = -start.z / (plan.Target.z - start.z);

            Assert.LessOrEqual(plan.Target.z, 2.85f * CourtLengthScale);
            Assert.Less(trajectory.Evaluate(tNet).y, 1.53f);
        }

        [Test]
        public void AssistAndMagnetRetainOrderedEffectivePower()
        {
            ShuttleReturnPlanner planner = new ShuttleReturnPlanner();
            Vector3 start = new Vector3(0f, 1.4f, -5.1f);
            RacketHitResult sweet = UpwardHit();
            RacketHitResult assist = sweet;
            assist.AssistUsed = true;
            RacketHitResult magnet = sweet;
            magnet.AssistUsed = true;
            magnet.MagnetUsed = true;

            ShuttlePlayerReturnPlan sweetPlan = SensorPlan(planner, start, sweet, 4200f);
            ShuttlePlayerReturnPlan assistPlan = SensorPlan(planner, start, assist, 4200f);
            ShuttlePlayerReturnPlan magnetPlan = SensorPlan(planner, start, magnet, 4200f);

            Assert.Greater(sweetPlan.EffectivePower01, assistPlan.EffectivePower01);
            Assert.Greater(assistPlan.EffectivePower01, magnetPlan.EffectivePower01);
        }

        [Test]
        public void SensorPlanUsesWorldFaceMotionForLateralAim()
        {
            ShuttleReturnPlanner planner = new ShuttleReturnPlanner();
            Vector3 start = new Vector3(0f, 1.3f, -4.8f);
            RacketHitResult rightward = UpwardHit();
            rightward.ContactFaceVelocity = new Vector3(4f, 2.4f, 8f);
            RacketHitResult leftward = rightward;
            leftward.ContactFaceVelocity = new Vector3(-4f, 2.4f, 8f);

            ShuttlePlayerReturnPlan rightPlan = SensorPlan(planner, start, rightward, 3400f);
            ShuttlePlayerReturnPlan leftPlan = SensorPlan(planner, start, leftward, 3400f);

            Assert.Greater(rightPlan.Target.x, 0f);
            Assert.Less(leftPlan.Target.x, 0f);
            Assert.Greater(rightPlan.AimYawDegrees, leftPlan.AimYawDegrees);
        }

        [Test]
        public void RequiredArcHeightMatchesTrajectoryAtNet()
        {
            Vector3 start = new Vector3(0f, 0.85f, -2f);
            Vector3 target = new Vector3(0f, 0.09f, 3f);
            float apexT = 0.5f;
            const float minimumNetY = 1.62f;

            float requiredArc = ShuttleReturnPlanner.RequiredArcHeightForNetClearance(
                start,
                target,
                apexT,
                minimumNetY);
            ShuttleTrajectory trajectory = ShuttleTrajectoryPlanner.Create(
                start,
                target,
                1f,
                requiredArc,
                apexT);
            float tNet = -start.z / (target.z - start.z);

            Assert.Greater(requiredArc, 0f);
            Assert.AreEqual(minimumNetY, trajectory.Evaluate(tNet).y, 0.001f);
        }

        private static ShuttlePlayerReturnPlan SensorPlan(
            ShuttleReturnPlanner planner,
            Vector3 start,
            RacketHitResult hit,
            float powerSwingSpeed,
            bool jumpSmash = false)
        {
            return planner.CreateSensorPlayerReturnPlan(
                start,
                hit,
                powerSwingSpeed,
                CourtLengthScale,
                MediumSwingSpeed,
                FastSwingSpeed,
                1.05f,
                0.88f,
                0.74f,
                30f,
                jumpSmash);
        }

        private static RacketHitResult UpwardHit()
        {
            return new RacketHitResult
            {
                Hit = true,
                Shot = RacketResolvedShot.Drop,
                ContactPoint = new Vector3(0f, 1.55f, -5.2f),
                Quality = 0.86f,
                SwingUpward = true,
                ContactFaceNormal = new Vector3(0.08f, 0.45f, 0.89f).normalized,
                ContactFaceVelocity = new Vector3(0.35f, 5.1f, 8.2f),
                ContactSwingDirection = new Vector3(0.05f, 0.55f, 0.83f).normalized,
                ContactTrackingConfidence = 1f
            };
        }

        private static RacketHitResult DownwardHit()
        {
            return new RacketHitResult
            {
                Hit = true,
                Shot = RacketResolvedShot.Drop,
                ContactPoint = new Vector3(0f, 2.08f, -5f),
                Quality = 0.88f,
                SwingUpward = false,
                ContactFaceNormal = new Vector3(0.04f, -0.2f, 0.98f).normalized,
                ContactFaceVelocity = new Vector3(0.2f, -7.2f, 7.8f),
                ContactSwingDirection = new Vector3(0f, -0.65f, 0.76f).normalized,
                ContactTrackingConfidence = 1f
            };
        }

        private static void AssertVectorNearlyEqual(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.0001f);
            Assert.AreEqual(expected.y, actual.y, 0.0001f);
            Assert.AreEqual(expected.z, actual.z, 0.0001f);
        }
    }
}
