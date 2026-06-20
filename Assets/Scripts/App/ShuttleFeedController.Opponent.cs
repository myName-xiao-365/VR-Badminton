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
            Vector3 startPosition = opponentRacket.localPosition;
            Quaternion startRotation = opponentRacket.localRotation;
            Quaternion startBodyRotation = opponentBody.localRotation;
            float startHeight = opponentPlayer.position.y;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                opponentRacket.localPosition = Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    t);
                opponentRacket.localRotation = Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    t);
                opponentBody.localRotation = Quaternion.Slerp(
                    startBodyRotation,
                    targetBodyRotation,
                    t);
                if (jumping)
                {
                    Vector3 bodyPosition = opponentPlayer.position;
                    bodyPosition.y = Mathf.Lerp(
                        startHeight,
                        0.55f + 0.82f * jumpPhase,
                        t);
                    opponentPlayer.position = bodyPosition;
                }
                yield return null;
            }
        }

        private Vector3 GetOpponentReadyPosition(Vector3 contactPoint, float sourceSide)
        {
            bool backhand = ShouldOpponentUseBackhand(contactPoint);
            float handSide = backhand ? 1f : -1f;
            float readyZ = contactPoint.z + 0.12f;
            if (contactPoint.z < opponentFrontCourtReadyDepth * CourtLengthScale)
            {
                readyZ = opponentFrontCourtReadyDepth * CourtLengthScale;
            }

            return new Vector3(
                contactPoint.x - handSide * 0.72f,
                0.55f,
                readyZ);
        }

        private void MoveOpponentTowards(
            Vector3 readyPosition,
            Vector3 contactPoint,
            float sourceSide)
        {
            Vector3 previousPosition = opponentPlayer.position;
            Vector3 groundedTarget = new Vector3(
                readyPosition.x,
                0.55f,
                readyPosition.z);
            opponentPlayer.position = Vector3.MoveTowards(
                opponentPlayer.position,
                groundedTarget,
                opponentMoveSpeed * Time.deltaTime);
            SpendOpponentRunStamina(previousPosition, opponentPlayer.position);

            bool backhand = ShouldOpponentUseBackhand(contactPoint);
            PositionOpponentRacket(contactPoint, sourceSide, backhand);
            Quaternion bodyTarget = Quaternion.Euler(
                0f,
                backhand ? 138f : 16f,
                backhand ? -9f : 3f);
            opponentBody.localRotation = Quaternion.Slerp(
                opponentBody.localRotation,
                bodyTarget,
                8f * Time.deltaTime);
        }

        private void UpdateOpponentReturnToCenter()
        {
            if (!opponentReturningToCenter ||
                opponentStamina <= 0f ||
                opponentPlayer == null)
            {
                return;
            }

            Vector3 center = new Vector3(
                0f,
                opponentPlayer.position.y,
                3.65f * CourtLengthScale);
            Vector3 previousPosition = opponentPlayer.position;
            opponentPlayer.position = Vector3.MoveTowards(
                opponentPlayer.position,
                center,
                opponentRecoverySpeed * Time.deltaTime);
            SpendOpponentRunStamina(previousPosition, opponentPlayer.position);

            Vector2 currentGround = new Vector2(
                opponentPlayer.position.x,
                opponentPlayer.position.z);
            Vector2 centerGround = new Vector2(center.x, center.z);
            if (Vector2.Distance(currentGround, centerGround) <= 0.04f)
            {
                opponentReturningToCenter = false;
            }
        }

        private void UpdateOpponentForehandClearPreparation(
            float approachProgress,
            Vector3 readyPosition,
            Vector3 contactPoint)
        {
            Vector3 previousPosition = opponentPlayer.position;
            Vector3 groundedTarget = new Vector3(
                readyPosition.x,
                0.55f,
                readyPosition.z);
            opponentPlayer.position = Vector3.MoveTowards(
                opponentPlayer.position,
                groundedTarget,
                opponentMoveSpeed * Time.deltaTime);
            SpendOpponentRunStamina(previousPosition, opponentPlayer.position);

            approachProgress = Mathf.Clamp01(approachProgress);
            Vector3 readyRacketPosition = GetOpponentReadyRacketPosition();
            Quaternion readyRacketRotation = GetOpponentReadyRacketRotation();
            Quaternion readyBodyRotation = Quaternion.identity;

            Vector3 sideRacketPosition = new Vector3(-0.5f, 1.2f, 0.18f);
            Quaternion sideRacketRotation = Quaternion.Euler(-18f, 180f, 32f);
            Quaternion sideBodyRotation = Quaternion.Euler(-5f, 58f, 6f);

            Vector3 drawRacketPosition = new Vector3(-0.26f, 1.08f, 0.56f);
            Quaternion drawRacketRotation = Quaternion.Euler(58f, 180f, 94f);
            Quaternion drawBodyRotation = Quaternion.Euler(-10f, 88f, 11f);

            if (approachProgress < 0.38f)
            {
                float t = Mathf.SmoothStep(0f, 1f, approachProgress / 0.38f);
                opponentRacket.localPosition = Vector3.Lerp(
                    readyRacketPosition,
                    sideRacketPosition,
                    t);
                opponentRacket.localRotation = Quaternion.Slerp(
                    readyRacketRotation,
                    sideRacketRotation,
                    t);
                opponentBody.localRotation = Quaternion.Slerp(
                    readyBodyRotation,
                    sideBodyRotation,
                    t);
            }
            else
            {
                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    (approachProgress - 0.38f) / 0.62f);
                opponentRacket.localPosition = Vector3.Lerp(
                    sideRacketPosition,
                    drawRacketPosition,
                    t);
                opponentRacket.localRotation = Quaternion.Slerp(
                    sideRacketRotation,
                    drawRacketRotation,
                    t);
                opponentBody.localRotation = Quaternion.Slerp(
                    sideBodyRotation,
                    drawBodyRotation,
                    t);
            }

            if (approachProgress > 0.92f)
            {
                Quaternion contactRotation = Quaternion.Euler(0f, 180f, 0f);
                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    (approachProgress - 0.92f) / 0.08f);
                Vector3 localContact = opponentPlayer.InverseTransformPoint(contactPoint);
                Vector3 contactPosition =
                    localContact -
                    contactRotation * opponentRacketFace.localPosition;
                opponentRacket.localPosition = Vector3.Lerp(
                    opponentRacket.localPosition,
                    contactPosition,
                    t);
                opponentRacket.localRotation = Quaternion.Slerp(
                    opponentRacket.localRotation,
                    contactRotation,
                    t);
                opponentBody.localRotation = Quaternion.Slerp(
                    drawBodyRotation,
                    Quaternion.identity,
                    t);
            }
        }

        private void UpdateOpponentBackhandClearPreparation(
            float approachProgress,
            Vector3 readyPosition,
            Vector3 contactPoint)
        {
            Vector3 previousPosition = opponentPlayer.position;
            Vector3 groundedTarget = new Vector3(
                readyPosition.x,
                0.55f,
                readyPosition.z);
            opponentPlayer.position = Vector3.MoveTowards(
                opponentPlayer.position,
                groundedTarget,
                opponentMoveSpeed * Time.deltaTime);
            SpendOpponentRunStamina(previousPosition, opponentPlayer.position);

            approachProgress = Mathf.Clamp01(approachProgress);
            Vector3 readyRacketPosition = GetOpponentReadyRacketPosition();
            Quaternion readyRacketRotation = GetOpponentReadyRacketRotation();
            Quaternion readyBodyRotation = Quaternion.identity;

            Vector3 turnRacketPosition = new Vector3(-0.28f, 1.02f, 0.08f);
            Quaternion turnRacketRotation = Quaternion.Euler(18f, 96f, 72f);
            Quaternion turnBodyRotation = Quaternion.Euler(-4f, -72f, -5f);

            Vector3 drawRacketPosition = new Vector3(0.34f, 1.08f, 0.48f);
            Quaternion drawRacketRotation = Quaternion.Euler(8f, 88f, 96f);
            Quaternion drawBodyRotation = Quaternion.Euler(-7f, -148f, -8f);

            if (approachProgress < 0.34f)
            {
                float t = Mathf.SmoothStep(0f, 1f, approachProgress / 0.34f);
                opponentRacket.localPosition = Vector3.Lerp(
                    readyRacketPosition,
                    turnRacketPosition,
                    t);
                opponentRacket.localRotation = Quaternion.Slerp(
                    readyRacketRotation,
                    turnRacketRotation,
                    t);
                opponentBody.localRotation = Quaternion.Slerp(
                    readyBodyRotation,
                    turnBodyRotation,
                    t);
            }
            else
            {
                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    (approachProgress - 0.34f) / 0.66f);
                opponentRacket.localPosition = Vector3.Lerp(
                    turnRacketPosition,
                    drawRacketPosition,
                    t);
                opponentRacket.localRotation = Quaternion.Slerp(
                    turnRacketRotation,
                    drawRacketRotation,
                    t);
                opponentBody.localRotation = Quaternion.Slerp(
                    turnBodyRotation,
                    drawBodyRotation,
                    t);
            }

            if (approachProgress > 0.9f)
            {
                Quaternion contactRotation = Quaternion.Euler(0f, 0f, 0f);
                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    (approachProgress - 0.9f) / 0.1f);
                Vector3 localContact = opponentPlayer.InverseTransformPoint(contactPoint);
                Vector3 contactPosition =
                    localContact -
                    contactRotation * opponentRacketFace.localPosition;
                opponentRacket.localPosition = Vector3.Lerp(
                    opponentRacket.localPosition,
                    contactPosition,
                    t);
                opponentRacket.localRotation = Quaternion.Slerp(
                    opponentRacket.localRotation,
                    contactRotation,
                    t);
                opponentBody.localRotation = Quaternion.Slerp(
                    drawBodyRotation,
                    Quaternion.Euler(0f, -180f, 0f),
                    t);
            }
        }

        private void UpdateOpponentForehandNetPreparation(
            float approachProgress,
            Vector3 readyPosition,
            Vector3 contactPoint)
        {
            Vector3 previousPosition = opponentPlayer.position;
            Vector3 groundedTarget = new Vector3(
                readyPosition.x,
                0.55f,
                readyPosition.z);
            opponentPlayer.position = Vector3.MoveTowards(
                opponentPlayer.position,
                groundedTarget,
                opponentMoveSpeed * Time.deltaTime);
            SpendOpponentRunStamina(previousPosition, opponentPlayer.position);

            approachProgress = Mathf.Clamp01(approachProgress);
            Vector3 readyRacketPosition = GetOpponentReadyRacketPosition();
            Quaternion readyRacketRotation = GetOpponentReadyRacketRotation();
            Quaternion loweredBody = Quaternion.Euler(7f, 8f, -4f);
            Quaternion waitingRotation = GetOpponentForehandNetWaitingRotation();
            SetOpponentRacketFaceReversed(true);
            Vector3 localContact = opponentPlayer.InverseTransformPoint(contactPoint);
            Vector3 waitingPosition =
                localContact -
                waitingRotation * opponentRacketFace.localPosition;

            float prepareT = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.08f, 0.55f, approachProgress));
            if (approachProgress < 0.55f)
            {
                opponentRacket.localPosition = Vector3.Lerp(
                    readyRacketPosition,
                    waitingPosition,
                    prepareT);
                opponentRacket.localRotation = Quaternion.Slerp(
                    readyRacketRotation,
                    waitingRotation,
                    prepareT);
                opponentBody.localRotation = Quaternion.Slerp(
                    Quaternion.identity,
                    loweredBody,
                    prepareT);
            }
            else
            {
                // Hold the face on the predicted path until the shuttle arrives.
                opponentRacket.localPosition = waitingPosition;
                opponentRacket.localRotation = waitingRotation;
                opponentBody.localRotation = loweredBody;
            }
        }

        private void UpdateOpponentBackhandNetPreparation(
            float approachProgress,
            Vector3 readyPosition,
            Vector3 contactPoint)
        {
            Vector3 previousPosition = opponentPlayer.position;
            Vector3 groundedTarget = new Vector3(
                readyPosition.x,
                0.55f,
                readyPosition.z);
            opponentPlayer.position = Vector3.MoveTowards(
                opponentPlayer.position,
                groundedTarget,
                opponentMoveSpeed * Time.deltaTime);
            SpendOpponentRunStamina(previousPosition, opponentPlayer.position);

            approachProgress = Mathf.Clamp01(approachProgress);
            Vector3 readyRacketPosition = GetOpponentReadyRacketPosition();
            Quaternion readyRacketRotation = GetOpponentReadyRacketRotation();
            Quaternion loweredBody = Quaternion.Euler(7f, -45f, 5f);
            Quaternion waitingRotation = GetOpponentBackhandNetWaitingRotation();
            SetOpponentRacketFaceReversed(false);
            Vector3 localContact = opponentPlayer.InverseTransformPoint(contactPoint);
            Vector3 waitingPosition =
                localContact -
                waitingRotation * opponentRacketFace.localPosition;

            float prepareT = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.08f, 0.55f, approachProgress));
            if (approachProgress < 0.55f)
            {
                opponentRacket.localPosition = Vector3.Lerp(
                    readyRacketPosition,
                    waitingPosition,
                    prepareT);
                opponentRacket.localRotation = Quaternion.Slerp(
                    readyRacketRotation,
                    waitingRotation,
                    prepareT);
                opponentBody.localRotation = Quaternion.Slerp(
                    Quaternion.identity,
                    loweredBody,
                    prepareT);
            }
            else
            {
                opponentRacket.localPosition = waitingPosition;
                opponentRacket.localRotation = waitingRotation;
                opponentBody.localRotation = loweredBody;
            }
        }

        private void AlignOpponentRacketFace(
            Vector3 contactPoint,
            Quaternion racketRotation)
        {
            opponentRacket.localRotation = racketRotation;
            Vector3 localContact = opponentPlayer.InverseTransformPoint(contactPoint);
            opponentRacket.localPosition =
                localContact -
                racketRotation * opponentRacketFace.localPosition;
        }

        private static Vector3 GetOpponentReadyRacketPosition()
        {
            return new Vector3(-0.52f, 0.308f, -0.32f);
        }

        private static Quaternion GetOpponentReadyRacketRotation()
        {
            return Quaternion.Euler(18.021f, 98.821f, -53.36f);
        }

        private static Quaternion GetOpponentForehandNetWaitingRotation()
        {
            return Quaternion.Euler(100f, 180f, -4f);
        }

        private static Quaternion GetOpponentBackhandNetWaitingRotation()
        {
            return Quaternion.Euler(100f, 180f, -4f);
        }

        private void SetOpponentRacketFaceReversed(bool reversed)
        {
            opponentRacketFace.localRotation = reversed
                ? Quaternion.Euler(0f, 180f, 0f)
                : Quaternion.identity;
        }

        private float OpponentDistanceTo(Vector3 readyPosition)
        {
            Vector2 opponentGround = new Vector2(
                opponentPlayer.position.x,
                opponentPlayer.position.z);
            Vector2 targetGround = new Vector2(readyPosition.x, readyPosition.z);
            return Vector2.Distance(opponentGround, targetGround);
        }

        private bool ShouldOpponentUseBackhand(Vector3 contactPoint)
        {
            // The opponent faces -Z: world -X is its right (forehand) side,
            // while world +X is its left (backhand) side.
            return contactPoint.x > opponentPlayer.position.x;
        }

        private void PositionOpponentRacket(
            Vector3 contactPoint,
            float sourceSide,
            bool backhand)
        {
            SetOpponentRacketFaceReversed(false);
            float handSide = backhand ? 1f : -1f;
            float desiredHeight = Mathf.Clamp(
                contactPoint.y - opponentPlayer.position.y - 1.35f,
                0.35f,
                1.65f);
            opponentRacket.localPosition = new Vector3(
                handSide * 0.72f,
                desiredHeight,
                -0.02f);
            opponentRacket.localRotation = Quaternion.Euler(
                18f,
                backhand ? 0f : 180f,
                (backhand ? -sourceSide : sourceSide) * 12f);
        }

        private void PrepareOpponentRacket(
            Vector3 contactPoint,
            float sourceSide,
            bool backhand,
            bool jumpSmash)
        {
            PositionOpponentRacket(contactPoint, sourceSide, backhand);
            if (jumpSmash)
            {
                Vector3 bodyPosition = opponentPlayer.position;
                bodyPosition.y = 0.55f;
                opponentPlayer.position = bodyPosition;
            }

        }

    }
}
