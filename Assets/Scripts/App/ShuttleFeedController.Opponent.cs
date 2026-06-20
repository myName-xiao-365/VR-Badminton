using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Unity.Profiling;
using UnityEngine;
using VRBadminton.Gameplay;
using VRBadminton.Input;

namespace VRBadminton.App
{
    public sealed partial class ShuttleFeedController
    {
        private OpponentRig CurrentOpponentRig =>
            new OpponentRig(
                opponentPlayer,
                opponentBody,
                opponentRacket,
                opponentRacketFace);

        private OpponentShotType ChooseOpponentShot(bool canSmash, bool fromFrontCourt)
        {
            OpponentDecision decision = OpponentStrategy.Choose(
                difficultyLevel,
                opponentStamina,
                canSmash,
                fromFrontCourt,
                opponentSmashChance,
                Random.value);
            return ToOpponentShotType(decision.Shot);
        }

        private void SpendOpponentStamina(OpponentShotType shot)
        {
            opponentStamina = OpponentStaminaModel.SpendShot(
                opponentStamina,
                ToOpponentShotKind(shot));
        }

        private void SpendOpponentRunStamina(Vector3 from, Vector3 to)
        {
            opponentStamina = OpponentStaminaModel.SpendRun(
                opponentStamina,
                from,
                to,
                opponentRunStaminaPerMeter);
        }

        private bool CanOpponentAfford(OpponentShotType shot)
        {
            return OpponentStaminaModel.CanAfford(
                opponentStamina,
                ToOpponentShotKind(shot));
        }

        private static OpponentShotKind ToOpponentShotKind(OpponentShotType shot)
        {
            switch (shot)
            {
                case OpponentShotType.Drop:
                    return OpponentShotKind.Drop;
                case OpponentShotType.Lift:
                    return OpponentShotKind.Lift;
                case OpponentShotType.Clear:
                    return OpponentShotKind.Clear;
                case OpponentShotType.Smash:
                    return OpponentShotKind.Smash;
                default:
                    return OpponentShotKind.Net;
            }
        }

        private static OpponentShotType ToOpponentShotType(OpponentShotKind shot)
        {
            switch (shot)
            {
                case OpponentShotKind.Drop:
                    return OpponentShotType.Drop;
                case OpponentShotKind.Lift:
                    return OpponentShotType.Lift;
                case OpponentShotKind.Clear:
                    return OpponentShotType.Clear;
                case OpponentShotKind.Smash:
                    return OpponentShotType.Smash;
                default:
                    return OpponentShotType.Net;
            }
        }

        private IEnumerator AnimateOpponentHit(Vector3 contactPoint, float sourceSide)
        {
            opponentReturningToCenter = false;
            Vector3 readyPosition = GetOpponentReadyPosition(contactPoint, sourceSide);
            while (OpponentDistanceTo(readyPosition) > opponentReachTolerance)
            {
                if (opponentStamina <= 0f)
                {
                    yield break;
                }

                MoveOpponentTowards(readyPosition, contactPoint, sourceSide);
                yield return null;
            }

            bool backhand = ShouldOpponentUseBackhand(contactPoint);
            PrepareOpponentRacket(contactPoint, sourceSide, backhand, false);
            yield return AnimateOpponentSwing(
                sourceSide,
                backhand,
                OpponentShotType.Lift,
                false);
        }

        private IEnumerator AnimateOpponentSwing(
            float sourceSide,
            bool backhand,
            OpponentShotType shot,
            bool receivingSmash,
            bool forehandClearPrepared = false,
            bool backhandOverheadPrepared = false,
            bool forehandNetPrepared = false,
            bool backhandNetPrepared = false)
        {
            OpponentSwingStyle style = GetOpponentSwingStyle(
                shot,
                backhand,
                receivingSmash);
            SetOpponentRacketFaceReversed(
                style == OpponentSwingStyle.ForehandNet);
            if (style == OpponentSwingStyle.ForehandOverhead)
            {
                yield return AnimateOpponentForehandClear(forehandClearPrepared);
                yield break;
            }
            if (style == OpponentSwingStyle.BackhandOverhead)
            {
                yield return AnimateOpponentBackhandClear(backhandOverheadPrepared);
                yield break;
            }
            if (style == OpponentSwingStyle.BackhandDrop)
            {
                yield return AnimateOpponentBackhandDrop(backhandOverheadPrepared);
                yield break;
            }
            if (style == OpponentSwingStyle.ForehandNet)
            {
                yield return AnimateOpponentForehandNet(forehandNetPrepared);
                yield break;
            }
            if (style == OpponentSwingStyle.BackhandNet)
            {
                yield return AnimateOpponentBackhandNet(backhandNetPrepared);
                yield break;
            }

            float handSide = backhand ? 1f : -1f;
            Quaternion bodyNeutral = Quaternion.identity;
            Quaternion bodyTurned = Quaternion.Euler(
                0f,
                backhand ? 145f : 24f,
                backhand ? -10f : 5f);
            Vector3 neutralPosition = GetOpponentReadyRacketPosition();
            Quaternion neutralRotation = GetOpponentReadyRacketRotation();

            Vector3 preparationPosition;
            Vector3 contactPosition;
            Vector3 followPosition;
            Quaternion preparationRotation;
            Quaternion contactRotation;
            Quaternion followRotation;
            float preparationDuration;
            float strikeDuration;
            bool jump = false;

            switch (style)
            {
                case OpponentSwingStyle.BackhandOverhead:
                    // Rear draw: vertical face, horizontal shaft, elbow leading.
                    preparationPosition = new Vector3(handSide * 0.58f, 1.05f, 0.34f);
                    contactPosition = new Vector3(handSide * 0.82f, 1.58f, -0.08f);
                    followPosition = new Vector3(handSide * 0.52f, 1.15f, -0.34f);
                    preparationRotation = Quaternion.Euler(0f, 92f, 90f);
                    contactRotation = Quaternion.Euler(-4f, 0f, 2f);
                    followRotation = Quaternion.Euler(-62f, -8f, -18f);
                    preparationDuration = 0.18f;
                    strikeDuration = 0.2f;
                    break;

                case OpponentSwingStyle.Net:
                    // Flat face carried gently through the shuttle.
                    preparationPosition = new Vector3(handSide * 0.96f, 0.62f, -0.18f);
                    contactPosition = new Vector3(handSide * 1.08f, 0.7f, -0.42f);
                    followPosition = new Vector3(handSide * 1.04f, 0.74f, -0.52f);
                    preparationRotation = Quaternion.Euler(88f, backhand ? 0f : 180f, 0f);
                    contactRotation = Quaternion.Euler(82f, backhand ? 0f : 180f, 0f);
                    followRotation = Quaternion.Euler(76f, backhand ? 0f : 180f, 0f);
                    preparationDuration = 0.12f;
                    strikeDuration = 0.15f;
                    break;

                case OpponentSwingStyle.Lift:
                    // Drop the racket head, then accelerate upward through contact.
                    preparationPosition = new Vector3(handSide * 0.82f, 0.16f, 0.12f);
                    contactPosition = new Vector3(handSide * 0.95f, 0.72f, -0.3f);
                    followPosition = new Vector3(handSide * 0.72f, 1.22f, -0.38f);
                    preparationRotation = Quaternion.Euler(112f, backhand ? 0f : 180f, 8f);
                    contactRotation = Quaternion.Euler(18f, backhand ? 0f : 180f, 2f);
                    followRotation = Quaternion.Euler(-48f, backhand ? 0f : 180f, -8f);
                    preparationDuration = 0.16f;
                    strikeDuration = 0.2f;
                    break;

                case OpponentSwingStyle.SmashDefense:
                    // Block by extending the face to the incoming side.
                    preparationPosition = new Vector3(handSide * 0.78f, 0.72f, -0.08f);
                    contactPosition = new Vector3(handSide * 1.3f, 0.78f, -0.28f);
                    followPosition = new Vector3(handSide * 1.18f, 0.82f, -0.36f);
                    preparationRotation = Quaternion.Euler(6f, backhand ? 0f : 180f, 4f);
                    contactRotation = Quaternion.Euler(-4f, backhand ? 0f : 180f, 0f);
                    followRotation = Quaternion.Euler(-16f, backhand ? 0f : 180f, -4f);
                    preparationDuration = 0.09f;
                    strikeDuration = 0.12f;
                    break;

                case OpponentSwingStyle.JumpSmash:
                    preparationPosition = new Vector3(handSide * 0.48f, 1.28f, 0.3f);
                    contactPosition = new Vector3(handSide * 0.66f, 1.72f, -0.16f);
                    followPosition = new Vector3(handSide * 0.34f, 0.94f, -0.42f);
                    preparationRotation = Quaternion.Euler(-42f, backhand ? 0f : 180f, 18f);
                    contactRotation = Quaternion.Euler(-126f, backhand ? 0f : 180f, 4f);
                    followRotation = Quaternion.Euler(-158f, backhand ? 0f : 180f, -12f);
                    preparationDuration = 0.17f;
                    strikeDuration = 0.16f;
                    jump = true;
                    break;

                case OpponentSwingStyle.ForehandDrop:
                    preparationPosition = new Vector3(handSide * 0.48f, 1.24f, 0.3f);
                    contactPosition = new Vector3(handSide * 0.68f, 1.62f, -0.06f);
                    followPosition = new Vector3(handSide * 0.54f, 1.28f, -0.22f);
                    preparationRotation = Quaternion.Euler(-26f, 180f, 18f);
                    contactRotation = Quaternion.Euler(-76f, 180f, 4f);
                    followRotation = Quaternion.Euler(-96f, 180f, -6f);
                    preparationDuration = 0.16f;
                    strikeDuration = 0.16f;
                    break;

                default:
                    // Raise the hand, draw behind the shoulder, then contact at full reach.
                    preparationPosition = new Vector3(handSide * 0.46f, 1.28f, 0.34f);
                    contactPosition = new Vector3(handSide * 0.68f, 1.68f, -0.08f);
                    followPosition = new Vector3(handSide * 0.38f, 1.02f, -0.36f);
                    preparationRotation = Quaternion.Euler(-28f, 180f, 20f);
                    contactRotation = Quaternion.Euler(-92f, 180f, 4f);
                    followRotation = Quaternion.Euler(-132f, 180f, -14f);
                    preparationDuration = 0.18f;
                    strikeDuration = 0.18f;
                    break;
            }

            yield return AnimateOpponentPose(
                preparationPosition,
                preparationRotation,
                bodyTurned,
                preparationDuration,
                false,
                0f);
            yield return AnimateOpponentPose(
                contactPosition,
                contactRotation,
                bodyTurned,
                strikeDuration,
                jump,
                1f);
            yield return AnimateOpponentPose(
                followPosition,
                followRotation,
                bodyTurned,
                0.12f,
                jump,
                0.35f);

            Vector3 groundedPosition = opponentPlayer.position;
            groundedPosition.y = 0.55f;
            opponentPlayer.position = groundedPosition;
            yield return AnimateOpponentPose(
                neutralPosition,
                neutralRotation,
                bodyNeutral,
                backhand ? 0.3f : 0.22f,
                false,
                0f);
        }

        private OpponentSwingStyle GetOpponentSwingStyle(
            OpponentShotType shot,
            bool backhand,
            bool receivingSmash)
        {
            if (receivingSmash)
            {
                return OpponentSwingStyle.SmashDefense;
            }

            if (shot == OpponentShotType.Smash)
            {
                return OpponentSwingStyle.JumpSmash;
            }

            if (shot == OpponentShotType.Net)
            {
                return backhand
                    ? OpponentSwingStyle.BackhandNet
                    : OpponentSwingStyle.ForehandNet;
            }

            if (shot == OpponentShotType.Lift)
            {
                return OpponentSwingStyle.Lift;
            }

            if (shot == OpponentShotType.Drop && !backhand)
            {
                return OpponentSwingStyle.ForehandOverhead;
            }
            if (shot == OpponentShotType.Drop && backhand)
            {
                return OpponentSwingStyle.BackhandDrop;
            }

            return backhand
                ? OpponentSwingStyle.BackhandOverhead
                : OpponentSwingStyle.ForehandOverhead;
        }

        private IEnumerator AnimateOpponentForehandClear(bool preparationComplete)
        {
            const float handSide = -1f;

            if (!preparationComplete)
            {
                // 1. Ready: racket upright, weight centered.
                yield return AnimateOpponentPose(
                    GetOpponentReadyRacketPosition(),
                    GetOpponentReadyRacketRotation(),
                    Quaternion.identity,
                    0.1f,
                    false,
                    0f);

                // 2. Turn sideways and raise the elbow.
                yield return AnimateOpponentPose(
                    new Vector3(handSide * 0.5f, 1.2f, 0.18f),
                    Quaternion.Euler(-18f, 180f, 32f),
                    Quaternion.Euler(-5f, 58f, 6f),
                    0.13f,
                    false,
                    0f);

                // 3. Racket head drops behind the shoulder while the elbow stays high.
                yield return AnimateOpponentPose(
                    new Vector3(handSide * 0.26f, 1.08f, 0.56f),
                    Quaternion.Euler(58f, 180f, 94f),
                    Quaternion.Euler(-10f, 88f, 11f),
                    0.18f,
                    false,
                    0f);
            }

            if (!preparationComplete)
            {
                // 4. Drive upward: hips unwind and the arm reaches for the apex.
                yield return AnimateOpponentPose(
                    new Vector3(handSide * 0.56f, 1.52f, 0.08f),
                    Quaternion.Euler(-54f, 180f, 24f),
                    Quaternion.Euler(-4f, 24f, 4f),
                    0.12f,
                    false,
                    0f);

                // 5. Full extension at contact with the body facing forward.
                yield return AnimateOpponentPose(
                    new Vector3(handSide * 0.7f, 1.76f, -0.1f),
                    Quaternion.Euler(0f, 180f, 0f),
                    Quaternion.identity,
                    0.1f,
                    false,
                    0f);
            }

            // 6. Forearm pronation sends the racket across the body.
            yield return AnimateOpponentPose(
                new Vector3(-0.3f, 0.92f, -0.42f),
                Quaternion.Euler(118f, 180f, 24f),
                Quaternion.Euler(9f, 30f, 8f),
                0.2f,
                false,
                0f);

            // 7. Recover balance and return to the central ready position.
            yield return AnimateOpponentPose(
                GetOpponentReadyRacketPosition(),
                GetOpponentReadyRacketRotation(),
                Quaternion.identity,
                0.22f,
                false,
                0f);
        }

        private IEnumerator AnimateOpponentBackhandClear(bool preparationComplete)
        {
            if (!preparationComplete)
            {
                // 1. Face forward with the racket carried in front.
                yield return AnimateOpponentPose(
                    GetOpponentReadyRacketPosition(),
                    GetOpponentReadyRacketRotation(),
                    Quaternion.identity,
                    0.1f,
                    false,
                    0f);

                // 2. Turn toward the body's left and lift the elbow.
                yield return AnimateOpponentPose(
                    new Vector3(-0.28f, 1.02f, 0.08f),
                    Quaternion.Euler(18f, 96f, 72f),
                    Quaternion.Euler(-4f, -72f, -5f),
                    0.14f,
                    false,
                    0f);

                // 3. Continue to a back-facing stance and draw behind the head.
                yield return AnimateOpponentPose(
                    new Vector3(0.34f, 1.08f, 0.48f),
                    Quaternion.Euler(8f, 88f, 96f),
                    Quaternion.Euler(-7f, -148f, -8f),
                    0.18f,
                    false,
                    0f);

                // 4. Lead with the elbow and externally rotate into contact.
                yield return AnimateOpponentPose(
                    new Vector3(0.62f, 1.5f, 0.06f),
                    Quaternion.Euler(0f, 0f, 0f),
                    Quaternion.Euler(0f, -180f, 0f),
                    0.16f,
                    false,
                    0f);
            }

            // 5. The body remains back-facing at contact, then the racket releases.
            yield return AnimateOpponentPose(
                new Vector3(0.34f, 1.02f, -0.34f),
                Quaternion.Euler(106f, 180f, -18f),
                Quaternion.Euler(5f, -142f, -6f),
                0.18f,
                false,
                0f);

            // 6. Turn back to the forward ready position.
            yield return AnimateOpponentPose(
                GetOpponentReadyRacketPosition(),
                GetOpponentReadyRacketRotation(),
                Quaternion.identity,
                0.3f,
                false,
                0f);
            SetOpponentRacketFaceReversed(false);
        }

        private IEnumerator AnimateOpponentBackhandDrop(bool preparationComplete)
        {
            if (!preparationComplete)
            {
                yield return AnimateOpponentPose(
                    GetOpponentReadyRacketPosition(),
                    GetOpponentReadyRacketRotation(),
                    Quaternion.identity,
                    0.1f,
                    false,
                    0f);
                yield return AnimateOpponentPose(
                    new Vector3(-0.28f, 1.02f, 0.08f),
                    Quaternion.Euler(18f, 96f, 72f),
                    Quaternion.Euler(-4f, -72f, -5f),
                    0.14f,
                    false,
                    0f);
                yield return AnimateOpponentPose(
                    new Vector3(0.34f, 1.08f, 0.48f),
                    Quaternion.Euler(8f, 88f, 96f),
                    Quaternion.Euler(-7f, -148f, -8f),
                    0.18f,
                    false,
                    0f);
                yield return AnimateOpponentPose(
                    new Vector3(0.62f, 1.5f, 0.06f),
                    Quaternion.Euler(0f, 0f, 0f),
                    Quaternion.Euler(0f, -180f, 0f),
                    0.15f,
                    false,
                    0f);
            }

            // Soft slice: keep the same disguise, then shorten the release.
            yield return AnimateOpponentPose(
                new Vector3(0.5f, 1.28f, -0.2f),
                Quaternion.Euler(38f, 0f, -10f),
                Quaternion.Euler(2f, -166f, -3f),
                0.13f,
                false,
                0f);
            yield return AnimateOpponentPose(
                new Vector3(0.3f, 1.04f, -0.3f),
                Quaternion.Euler(64f, 0f, -14f),
                Quaternion.Euler(4f, -146f, -5f),
                0.12f,
                false,
                0f);
            yield return AnimateOpponentPose(
                GetOpponentReadyRacketPosition(),
                GetOpponentReadyRacketRotation(),
                Quaternion.identity,
                0.3f,
                false,
                0f);
            SetOpponentRacketFaceReversed(false);
        }

        private IEnumerator AnimateOpponentForehandNet(bool preparationComplete)
        {
            if (!preparationComplete)
            {
                yield return AnimateOpponentPose(
                    GetOpponentReadyRacketPosition(),
                    GetOpponentReadyRacketRotation(),
                    Quaternion.identity,
                    0.08f,
                    false,
                    0f);
                yield return AnimateOpponentPose(
                    new Vector3(-0.96f, 0.5f, -0.42f),
                    GetOpponentForehandNetWaitingRotation(),
                    Quaternion.Euler(7f, 8f, -4f),
                    0.22f,
                    false,
                    0f);
            }

            Vector3 waitingPosition = opponentRacket.localPosition;

            // Settle slightly lower on contact without pulling toward the body.
            yield return AnimateOpponentPose(
                waitingPosition + new Vector3(0f, -0.055f, 0f),
                Quaternion.Euler(104f, 180f, -3f),
                Quaternion.Euler(7f, 8f, -4f),
                0.12f,
                false,
                0f);

            // Rebound softly toward a nearly level, still slightly downward face.
            yield return AnimateOpponentPose(
                waitingPosition + new Vector3(0f, -0.015f, -0.025f),
                Quaternion.Euler(96f, 180f, -2f),
                Quaternion.Euler(7f, 8f, -4f),
                0.16f,
                false,
                0f);

            // Only after the shuttle has left the face does the racket recover.
            yield return AnimateOpponentPose(
                GetOpponentReadyRacketPosition(),
                GetOpponentReadyRacketRotation(),
                Quaternion.identity,
                0.3f,
                false,
                0f);
            SetOpponentRacketFaceReversed(false);
        }

        private IEnumerator AnimateOpponentBackhandNet(bool preparationComplete)
        {
            if (!preparationComplete)
            {
                yield return AnimateOpponentPose(
                    GetOpponentReadyRacketPosition(),
                    GetOpponentReadyRacketRotation(),
                    Quaternion.identity,
                    0.08f,
                    false,
                    0f);
                yield return AnimateOpponentPose(
                    new Vector3(0.96f, 0.5f, -0.62f),
                    GetOpponentBackhandNetWaitingRotation(),
                    Quaternion.Euler(7f, -45f, 5f),
                    0.22f,
                    false,
                    0f);
            }

            Vector3 waitingPosition = opponentRacket.localPosition;
            yield return AnimateOpponentPose(
                waitingPosition + new Vector3(0f, -0.055f, 0f),
                Quaternion.Euler(104f, 180f, -3f),
                Quaternion.Euler(7f, -45f, 5f),
                0.12f,
                false,
                0f);
            yield return AnimateOpponentPose(
                waitingPosition + new Vector3(0f, -0.015f, -0.025f),
                Quaternion.Euler(96f, 180f, -2f),
                Quaternion.Euler(7f, -45f, 5f),
                0.16f,
                false,
                0f);
            yield return AnimateOpponentPose(
                GetOpponentReadyRacketPosition(),
                GetOpponentReadyRacketRotation(),
                Quaternion.identity,
                0.3f,
                false,
                0f);
        }

        private IEnumerator AnimateOpponentPose(
            Vector3 targetPosition,
            Quaternion targetRotation,
            Quaternion targetBodyRotation,
            float duration,
            bool jumping,
            float jumpPhase)
        {
            return opponentPoseAnimator.AnimatePose(
                opponentPlayer,
                opponentBody,
                opponentRacket,
                targetPosition,
                targetRotation,
                targetBodyRotation,
                duration,
                jumping,
                jumpPhase);
        }

        private Vector3 GetOpponentReadyPosition(Vector3 contactPoint, float sourceSide)
        {
            return opponentMovementRunner.GetReadyPosition(
                CurrentOpponentRig,
                contactPoint,
                opponentFrontCourtReadyDepth,
                CourtLengthScale);
        }

        private void MoveOpponentTowards(
            Vector3 readyPosition,
            Vector3 contactPoint,
            float sourceSide)
        {
            opponentStamina = opponentMovementRunner.MoveTowards(
                CurrentOpponentRig,
                readyPosition,
                contactPoint,
                sourceSide,
                opponentMoveSpeed,
                Time.deltaTime,
                opponentStamina,
                opponentRunStaminaPerMeter);
        }

        private void UpdateOpponentReturnToCenter()
        {
            OpponentReturnToCenterResult result =
                opponentMovementRunner.UpdateReturnToCenter(
                    CurrentOpponentRig,
                    opponentReturningToCenter,
                    opponentStamina,
                    opponentRecoverySpeed,
                    Time.deltaTime,
                    CourtLengthScale,
                    opponentRunStaminaPerMeter);
            opponentStamina = result.Stamina;
            opponentReturningToCenter = result.ReturningToCenter;
        }

        private void UpdateOpponentForehandClearPreparation(
            float approachProgress,
            Vector3 readyPosition,
            Vector3 contactPoint)
        {
            opponentStamina =
                opponentMovementRunner.UpdateForehandClearPreparation(
                    CurrentOpponentRig,
                    approachProgress,
                    readyPosition,
                    contactPoint,
                    opponentMoveSpeed,
                    Time.deltaTime,
                    opponentStamina,
                    opponentRunStaminaPerMeter);
        }

        private void UpdateOpponentBackhandClearPreparation(
            float approachProgress,
            Vector3 readyPosition,
            Vector3 contactPoint)
        {
            opponentStamina =
                opponentMovementRunner.UpdateBackhandClearPreparation(
                    CurrentOpponentRig,
                    approachProgress,
                    readyPosition,
                    contactPoint,
                    opponentMoveSpeed,
                    Time.deltaTime,
                    opponentStamina,
                    opponentRunStaminaPerMeter);
        }

        private void UpdateOpponentForehandNetPreparation(
            float approachProgress,
            Vector3 readyPosition,
            Vector3 contactPoint)
        {
            opponentStamina =
                opponentMovementRunner.UpdateForehandNetPreparation(
                    CurrentOpponentRig,
                    approachProgress,
                    readyPosition,
                    contactPoint,
                    opponentMoveSpeed,
                    Time.deltaTime,
                    opponentStamina,
                    opponentRunStaminaPerMeter);
        }

        private void UpdateOpponentBackhandNetPreparation(
            float approachProgress,
            Vector3 readyPosition,
            Vector3 contactPoint)
        {
            opponentStamina =
                opponentMovementRunner.UpdateBackhandNetPreparation(
                    CurrentOpponentRig,
                    approachProgress,
                    readyPosition,
                    contactPoint,
                    opponentMoveSpeed,
                    Time.deltaTime,
                    opponentStamina,
                    opponentRunStaminaPerMeter);
        }

        private void AlignOpponentRacketFace(
            Vector3 contactPoint,
            Quaternion racketRotation)
        {
            opponentMovementRunner.AlignRacketFace(
                CurrentOpponentRig,
                contactPoint,
                racketRotation);
        }

        private static Vector3 GetOpponentReadyRacketPosition()
        {
            return OpponentMovementRunner.GetReadyRacketPosition();
        }

        private static Quaternion GetOpponentReadyRacketRotation()
        {
            return OpponentMovementRunner.GetReadyRacketRotation();
        }

        private static Quaternion GetOpponentForehandNetWaitingRotation()
        {
            return OpponentMovementRunner.GetForehandNetWaitingRotation();
        }

        private static Quaternion GetOpponentBackhandNetWaitingRotation()
        {
            return OpponentMovementRunner.GetBackhandNetWaitingRotation();
        }

        private void SetOpponentRacketFaceReversed(bool reversed)
        {
            opponentMovementRunner.SetRacketFaceReversed(CurrentOpponentRig, reversed);
        }

        private float OpponentDistanceTo(Vector3 readyPosition)
        {
            return opponentMovementRunner.DistanceTo(CurrentOpponentRig, readyPosition);
        }

        private bool ShouldOpponentUseBackhand(Vector3 contactPoint)
        {
            return opponentMovementRunner.ShouldUseBackhand(
                CurrentOpponentRig,
                contactPoint);
        }

        private void PositionOpponentRacket(
            Vector3 contactPoint,
            float sourceSide,
            bool backhand)
        {
            opponentMovementRunner.PositionRacket(
                CurrentOpponentRig,
                contactPoint,
                sourceSide,
                backhand);
        }

        private void PrepareOpponentRacket(
            Vector3 contactPoint,
            float sourceSide,
            bool backhand,
            bool jumpSmash)
        {
            opponentMovementRunner.PrepareRacket(
                CurrentOpponentRig,
                contactPoint,
                sourceSide,
                backhand,
                jumpSmash);
        }

    }
}
