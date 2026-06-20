using NUnit.Framework;
using UnityEngine;
using VRBadminton.Gameplay;

namespace VRBadminton.Tests
{
    public sealed class GameplayFoundationTests
    {
        [Test]
        public void MatchRulesRequireTwoPointLeadBeforeCap()
        {
            MatchRules rules = MatchRules.TwentyOnePoint;

            Assert.AreEqual(0, rules.GetWinner(21, 20));
            Assert.AreEqual(1, rules.GetWinner(22, 20));
            Assert.AreEqual(2, rules.GetWinner(29, 30));
        }

        [Test]
        public void MatchStateAppliesRallyWinnerAndServiceOwner()
        {
            MatchState state = new MatchState();
            MatchRules rules = MatchRules.FifteenPoint;

            state.ApplyRallyWinner(1, rules);
            Assert.AreEqual(1, state.PlayerScore);
            Assert.IsTrue(state.PlayerServing);
            Assert.IsFalse(state.MatchOver);

            state.PlayerScore = 20;
            state.OpponentScore = 20;
            state.ApplyRallyWinner(1, rules);
            Assert.AreEqual(1, state.MatchWinner);
            Assert.IsTrue(state.MatchOver);
        }

        [Test]
        public void ServiceSideMatchesOddEvenScore()
        {
            Assert.AreEqual(1f, MatchRules.ServiceSideForScore(0));
            Assert.AreEqual(-1f, MatchRules.ServiceSideForScore(1));
            Assert.AreEqual(1f, MatchRules.ServiceSideForScore(2));
        }

        [Test]
        public void DifficultyTuningMatchesCurrentBaselineValues()
        {
            DifficultyTuning n0 = DifficultyTuning.ForLevel(0);
            DifficultyTuning n5 = DifficultyTuning.ForLevel(5);

            Assert.AreEqual(0, n0.Level);
            Assert.AreEqual(100f, n0.OpponentMaxStamina);
            Assert.AreEqual(0f, n0.OpponentSmashChance);
            Assert.AreEqual(0.05f, n0.OpponentSmashReceiveChance);
            Assert.AreEqual(500f, n5.OpponentMaxStamina);
            Assert.AreEqual(1f, n5.OpponentSmashChance);
            Assert.AreEqual(1f, n5.OpponentSmashReceiveChance);
        }

        [Test]
        public void ShuttleFlightPlanEvaluatesClearArc()
        {
            ShuttleFlightPlan plan = new ShuttleFlightPlan(
                Vector3.zero,
                new Vector3(0f, 0f, 10f),
                2f,
                4f,
                ShuttleFlightPlan.DefaultApexT(4f));

            Vector3 apex = plan.Evaluate(plan.ApexT);
            Vector3 end = plan.Evaluate(1f);

            Assert.IsTrue(plan.UsesClearArc);
            Assert.AreEqual(4f, apex.y, 0.001f);
            Assert.AreEqual(10f, end.z, 0.001f);
            Assert.AreEqual(0f, end.y, 0.001f);
        }

        [Test]
        public void ShuttleTrajectoryPlannerPreservesRuntimeArcShape()
        {
            ShuttleTrajectory trajectory = ShuttleTrajectoryPlanner.Create(
                Vector3.zero,
                new Vector3(0f, 0f, 10f),
                2f,
                4f,
                0.7f);

            Vector3 apex = trajectory.Evaluate(trajectory.ApexT);
            Vector3 start = trajectory.Evaluate(0f);
            Vector3 end = trajectory.Evaluate(1f);
            float contactProgress = trajectory.FindDescendingContactProgress(2.5f);

            Assert.IsTrue(trajectory.UsesClearArc);
            Assert.AreEqual(0f, start.y, 0.001f);
            Assert.AreEqual(4f, apex.y, 0.001f);
            Assert.AreEqual(0f, end.y, 0.001f);
            Assert.GreaterOrEqual(contactProgress, trajectory.ApexT);
            Assert.AreEqual(2.5f, trajectory.Evaluate(contactProgress).y, 0.12f);
        }

        [Test]
        public void CourtFaultResolverAwardsNetFaultToOpponentOfHitter()
        {
            CourtFaultResult playerFault = CourtFaultResolver.ResolveNetFault(1);
            CourtFaultResult opponentFault = CourtFaultResolver.ResolveNetFault(2);

            Assert.AreEqual(CourtFaultKind.Net, playerFault.Kind);
            Assert.AreEqual(2, playerFault.RallyWinner);
            Assert.AreEqual(1, opponentFault.RallyWinner);
        }

        [Test]
        public void RallyOutcomeResolverAppliesScoreServiceAndWinner()
        {
            MatchState state = new MatchState
            {
                PlayerScore = 20,
                OpponentScore = 19,
                PlayerServing = false
            };

            state = RallyOutcomeResolver.ApplyRallyWinner(
                state,
                1,
                MatchRules.TwentyOnePoint);

            Assert.AreEqual(21, state.PlayerScore);
            Assert.AreEqual(19, state.OpponentScore);
            Assert.IsTrue(state.PlayerServing);
            Assert.AreEqual(1, state.MatchWinner);
            Assert.IsTrue(state.MatchOver);
        }

        [Test]
        public void OpponentDecisionPreservesBaselineShotChoices()
        {
            Assert.AreEqual(
                OpponentShotKind.Clear,
                OpponentDecision.Choose(0, 100f, true, false, 1f, 0.25f).Shot);
            Assert.AreEqual(
                OpponentShotKind.Smash,
                OpponentDecision.Choose(3, 100f, true, false, 0.75f, 0.1f).Shot);
            Assert.AreEqual(
                OpponentShotKind.Lift,
                OpponentDecision.Choose(2, 5f, false, true, 0.5f, 0.2f).Shot);
            Assert.AreEqual(
                OpponentShotKind.Net,
                OpponentDecision.Choose(2, 0f, false, false, 0.5f, 0.2f).Shot);
        }

        [Test]
        public void OpponentStrategyDelegatesBaselineShotChoices()
        {
            Assert.AreEqual(
                OpponentShotKind.Drop,
                OpponentStrategy.Choose(0, 100f, false, false, 0f, 0.75f).Shot);
            Assert.AreEqual(
                OpponentShotKind.Net,
                OpponentStrategy.Choose(2, 5f, false, true, 0.5f, 0.8f).Shot);
        }

        [Test]
        public void OpponentStaminaModelSpendsShotAndRunCosts()
        {
            Assert.AreEqual(5f, OpponentStaminaModel.ShotCost(OpponentShotKind.Clear));
            Assert.AreEqual(10f, OpponentStaminaModel.ShotCost(OpponentShotKind.Smash));
            Assert.AreEqual(3f, OpponentStaminaModel.ShotCost(OpponentShotKind.Drop));
            Assert.IsTrue(OpponentStaminaModel.CanAfford(3f, OpponentShotKind.Net));
            Assert.IsFalse(OpponentStaminaModel.CanAfford(2.9f, OpponentShotKind.Net));

            float afterShot = OpponentStaminaModel.SpendShot(8f, OpponentShotKind.Clear);
            float afterRun = OpponentStaminaModel.SpendRun(
                10f,
                Vector3.zero,
                new Vector3(3f, 5f, 4f),
                0.5f);

            Assert.AreEqual(3f, afterShot, 0.001f);
            Assert.AreEqual(7.5f, afterRun, 0.001f);
        }
    }
}
