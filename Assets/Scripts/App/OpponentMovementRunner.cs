using UnityEngine;
using VRBadminton.Gameplay;

namespace VRBadminton.App
{
    internal readonly struct OpponentRig
    {
        public OpponentRig(
            Transform player,
            Transform body,
            Transform racket,
            Transform racketFace)
        {
            Player = player;
            Body = body;
            Racket = racket;
            RacketFace = racketFace;
        }

        public Transform Player { get; }

        public Transform Body { get; }

        public Transform Racket { get; }

        public Transform RacketFace { get; }
    }

    internal readonly struct OpponentReturnToCenterResult
    {
        public OpponentReturnToCenterResult(float stamina, bool returningToCenter)
        {
            Stamina = stamina;
            ReturningToCenter = returningToCenter;
        }

        public float Stamina { get; }

        public bool ReturningToCenter { get; }
    }

    internal sealed class OpponentMovementRunner
    {
        public Vector3 GetReadyPosition(
            OpponentRig rig,
            Vector3 contactPoint,
            float frontCourtReadyDepth,
            float courtLengthScale)
        {
            bool backhand = ShouldUseBackhand(rig, contactPoint);
            float handSide = backhand ? 1f : -1f;
            float readyZ = contactPoint.z + 0.12f;
            if (contactPoint.z < frontCourtReadyDepth * courtLengthScale)
            {
                readyZ = frontCourtReadyDepth * courtLengthScale;
            }

            return new Vector3(
                contactPoint.x - handSide * 0.72f,
                0.55f,
                readyZ);
        }

        public float MoveTowards(
            OpponentRig rig,
            Vector3 readyPosition,
            Vector3 contactPoint,
            float sourceSide,
            float moveSpeed,
            float deltaTime,
            float stamina,
            float runStaminaPerMeter)
        {
            stamina = MovePlayerToReady(
                rig,
                readyPosition,
                moveSpeed,
                deltaTime,
                stamina,
                runStaminaPerMeter);

            bool backhand = ShouldUseBackhand(rig, contactPoint);
            PositionRacket(rig, contactPoint, sourceSide, backhand);
            Quaternion bodyTarget = Quaternion.Euler(
                0f,
                backhand ? 138f : 16f,
                backhand ? -9f : 3f);
            rig.Body.localRotation = Quaternion.Slerp(
                rig.Body.localRotation,
                bodyTarget,
                8f * deltaTime);
            return stamina;
        }

        public OpponentReturnToCenterResult UpdateReturnToCenter(
            OpponentRig rig,
            bool returningToCenter,
            float stamina,
            float recoverySpeed,
            float deltaTime,
            float courtLengthScale,
            float runStaminaPerMeter)
        {
            if (!returningToCenter || stamina <= 0f || rig.Player == null)
            {
                return new OpponentReturnToCenterResult(stamina, returningToCenter);
            }

            Vector3 center = new Vector3(
                0f,
                rig.Player.position.y,
                3.65f * courtLengthScale);
            Vector3 previousPosition = rig.Player.position;
            rig.Player.position = Vector3.MoveTowards(
                rig.Player.position,
                center,
                recoverySpeed * deltaTime);
            stamina = SpendRunStamina(
                stamina,
                previousPosition,
                rig.Player.position,
                runStaminaPerMeter);

            Vector2 currentGround = new Vector2(
                rig.Player.position.x,
                rig.Player.position.z);
            Vector2 centerGround = new Vector2(center.x, center.z);
            if (Vector2.Distance(currentGround, centerGround) <= 0.04f)
            {
                returningToCenter = false;
            }

            return new OpponentReturnToCenterResult(stamina, returningToCenter);
        }

        public float UpdateForehandClearPreparation(
            OpponentRig rig,
            float approachProgress,
            Vector3 readyPosition,
            Vector3 contactPoint,
            float moveSpeed,
            float deltaTime,
            float stamina,
            float runStaminaPerMeter)
        {
            stamina = MovePlayerToReady(
                rig,
                readyPosition,
                moveSpeed,
                deltaTime,
                stamina,
                runStaminaPerMeter);

            approachProgress = Mathf.Clamp01(approachProgress);
            Vector3 readyRacketPosition = GetReadyRacketPosition();
            Quaternion readyRacketRotation = GetReadyRacketRotation();
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
                rig.Racket.localPosition = Vector3.Lerp(
                    readyRacketPosition,
                    sideRacketPosition,
                    t);
                rig.Racket.localRotation = Quaternion.Slerp(
                    readyRacketRotation,
                    sideRacketRotation,
                    t);
                rig.Body.localRotation = Quaternion.Slerp(
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
                rig.Racket.localPosition = Vector3.Lerp(
                    sideRacketPosition,
                    drawRacketPosition,
                    t);
                rig.Racket.localRotation = Quaternion.Slerp(
                    sideRacketRotation,
                    drawRacketRotation,
                    t);
                rig.Body.localRotation = Quaternion.Slerp(
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
                Vector3 localContact = rig.Player.InverseTransformPoint(contactPoint);
                Vector3 contactPosition =
                    localContact -
                    contactRotation * rig.RacketFace.localPosition;
                rig.Racket.localPosition = Vector3.Lerp(
                    rig.Racket.localPosition,
                    contactPosition,
                    t);
                rig.Racket.localRotation = Quaternion.Slerp(
                    rig.Racket.localRotation,
                    contactRotation,
                    t);
                rig.Body.localRotation = Quaternion.Slerp(
                    drawBodyRotation,
                    Quaternion.identity,
                    t);
            }

            return stamina;
        }

        public float UpdateBackhandClearPreparation(
            OpponentRig rig,
            float approachProgress,
            Vector3 readyPosition,
            Vector3 contactPoint,
            float moveSpeed,
            float deltaTime,
            float stamina,
            float runStaminaPerMeter)
        {
            stamina = MovePlayerToReady(
                rig,
                readyPosition,
                moveSpeed,
                deltaTime,
                stamina,
                runStaminaPerMeter);

            approachProgress = Mathf.Clamp01(approachProgress);
            Vector3 readyRacketPosition = GetReadyRacketPosition();
            Quaternion readyRacketRotation = GetReadyRacketRotation();
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
                rig.Racket.localPosition = Vector3.Lerp(
                    readyRacketPosition,
                    turnRacketPosition,
                    t);
                rig.Racket.localRotation = Quaternion.Slerp(
                    readyRacketRotation,
                    turnRacketRotation,
                    t);
                rig.Body.localRotation = Quaternion.Slerp(
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
                rig.Racket.localPosition = Vector3.Lerp(
                    turnRacketPosition,
                    drawRacketPosition,
                    t);
                rig.Racket.localRotation = Quaternion.Slerp(
                    turnRacketRotation,
                    drawRacketRotation,
                    t);
                rig.Body.localRotation = Quaternion.Slerp(
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
                Vector3 localContact = rig.Player.InverseTransformPoint(contactPoint);
                Vector3 contactPosition =
                    localContact -
                    contactRotation * rig.RacketFace.localPosition;
                rig.Racket.localPosition = Vector3.Lerp(
                    rig.Racket.localPosition,
                    contactPosition,
                    t);
                rig.Racket.localRotation = Quaternion.Slerp(
                    rig.Racket.localRotation,
                    contactRotation,
                    t);
                rig.Body.localRotation = Quaternion.Slerp(
                    drawBodyRotation,
                    Quaternion.Euler(0f, -180f, 0f),
                    t);
            }

            return stamina;
        }

        public float UpdateForehandNetPreparation(
            OpponentRig rig,
            float approachProgress,
            Vector3 readyPosition,
            Vector3 contactPoint,
            float moveSpeed,
            float deltaTime,
            float stamina,
            float runStaminaPerMeter)
        {
            stamina = MovePlayerToReady(
                rig,
                readyPosition,
                moveSpeed,
                deltaTime,
                stamina,
                runStaminaPerMeter);

            approachProgress = Mathf.Clamp01(approachProgress);
            Vector3 readyRacketPosition = GetReadyRacketPosition();
            Quaternion readyRacketRotation = GetReadyRacketRotation();
            Quaternion loweredBody = Quaternion.Euler(-7f, 8f, -4f);
            Quaternion waitingRotation = GetForehandNetWaitingRotation();
            SetRacketFaceReversed(rig, false);
            Vector3 localContact = rig.Player.InverseTransformPoint(contactPoint);
            Vector3 waitingPosition =
                localContact -
                waitingRotation * rig.RacketFace.localPosition;

            float prepareT = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.08f, 0.55f, approachProgress));
            if (approachProgress < 0.55f)
            {
                rig.Racket.localPosition = Vector3.Lerp(
                    readyRacketPosition,
                    waitingPosition,
                    prepareT);
                rig.Racket.localRotation = Quaternion.Slerp(
                    readyRacketRotation,
                    waitingRotation,
                    prepareT);
                rig.Body.localRotation = Quaternion.Slerp(
                    Quaternion.identity,
                    loweredBody,
                    prepareT);
            }
            else
            {
                rig.Racket.localPosition = waitingPosition;
                rig.Racket.localRotation = waitingRotation;
                rig.Body.localRotation = loweredBody;
            }

            return stamina;
        }

        public float UpdateBackhandNetPreparation(
            OpponentRig rig,
            float approachProgress,
            Vector3 readyPosition,
            Vector3 contactPoint,
            float moveSpeed,
            float deltaTime,
            float stamina,
            float runStaminaPerMeter)
        {
            stamina = MovePlayerToReady(
                rig,
                readyPosition,
                moveSpeed,
                deltaTime,
                stamina,
                runStaminaPerMeter);

            approachProgress = Mathf.Clamp01(approachProgress);
            Vector3 readyRacketPosition = GetReadyRacketPosition();
            Quaternion readyRacketRotation = GetReadyRacketRotation();
            Quaternion loweredBody = Quaternion.Euler(-7f, -45f, 5f);
            Quaternion waitingRotation = GetBackhandNetWaitingRotation();
            SetRacketFaceReversed(rig, false);
            Vector3 localContact = rig.Player.InverseTransformPoint(contactPoint);
            Vector3 waitingPosition =
                localContact -
                waitingRotation * rig.RacketFace.localPosition;

            float prepareT = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.08f, 0.55f, approachProgress));
            if (approachProgress < 0.55f)
            {
                rig.Racket.localPosition = Vector3.Lerp(
                    readyRacketPosition,
                    waitingPosition,
                    prepareT);
                rig.Racket.localRotation = Quaternion.Slerp(
                    readyRacketRotation,
                    waitingRotation,
                    prepareT);
                rig.Body.localRotation = Quaternion.Slerp(
                    Quaternion.identity,
                    loweredBody,
                    prepareT);
            }
            else
            {
                rig.Racket.localPosition = waitingPosition;
                rig.Racket.localRotation = waitingRotation;
                rig.Body.localRotation = loweredBody;
            }

            return stamina;
        }

        public void AlignRacketFace(
            OpponentRig rig,
            Vector3 contactPoint,
            Quaternion racketRotation)
        {
            rig.Racket.localRotation = racketRotation;
            Vector3 localContact = rig.Player.InverseTransformPoint(contactPoint);
            rig.Racket.localPosition =
                localContact -
                racketRotation * rig.RacketFace.localPosition;
        }

        public static Vector3 GetReadyRacketPosition()
        {
            return new Vector3(-0.52f, 0.308f, -0.32f);
        }

        public static Quaternion GetReadyRacketRotation()
        {
            return Quaternion.Euler(18.021f, 98.821f, -53.36f);
        }

        public static Quaternion GetForehandNetWaitingRotation()
        {
            return Quaternion.Euler(-100f, 360.004f, 40.772f);
        }

        public static Quaternion GetBackhandNetWaitingRotation()
        {
            return Quaternion.Euler(-261.192f, 159.933f, 9.886002f);
        }

        public void SetRacketFaceReversed(OpponentRig rig, bool reversed)
        {
            rig.RacketFace.localRotation = reversed
                ? Quaternion.Euler(0f, 180f, 0f)
                : Quaternion.identity;
        }

        public float DistanceTo(OpponentRig rig, Vector3 readyPosition)
        {
            Vector2 opponentGround = new Vector2(
                rig.Player.position.x,
                rig.Player.position.z);
            Vector2 targetGround = new Vector2(readyPosition.x, readyPosition.z);
            return Vector2.Distance(opponentGround, targetGround);
        }

        public bool ShouldUseBackhand(OpponentRig rig, Vector3 contactPoint)
        {
            // The opponent faces -Z: world -X is its right (forehand) side,
            // while world +X is its left (backhand) side.
            return contactPoint.x > rig.Player.position.x;
        }

        public void PositionRacket(
            OpponentRig rig,
            Vector3 contactPoint,
            float sourceSide,
            bool backhand)
        {
            SetRacketFaceReversed(rig, false);
            float handSide = backhand ? 1f : -1f;
            float desiredHeight = Mathf.Clamp(
                contactPoint.y - rig.Player.position.y - 1.35f,
                0.35f,
                1.65f);
            rig.Racket.localPosition = new Vector3(
                handSide * 0.72f,
                desiredHeight,
                -0.02f);
            rig.Racket.localRotation = Quaternion.Euler(
                18f,
                backhand ? 0f : 180f,
                (backhand ? -sourceSide : sourceSide) * 12f);
        }

        public void PrepareRacket(
            OpponentRig rig,
            Vector3 contactPoint,
            float sourceSide,
            bool backhand,
            bool jumpSmash)
        {
            PositionRacket(rig, contactPoint, sourceSide, backhand);
            if (jumpSmash)
            {
                Vector3 bodyPosition = rig.Player.position;
                bodyPosition.y = 0.55f;
                rig.Player.position = bodyPosition;
            }
        }

        private static float MovePlayerToReady(
            OpponentRig rig,
            Vector3 readyPosition,
            float moveSpeed,
            float deltaTime,
            float stamina,
            float runStaminaPerMeter)
        {
            Vector3 previousPosition = rig.Player.position;
            Vector3 groundedTarget = new Vector3(
                readyPosition.x,
                0.55f,
                readyPosition.z);
            rig.Player.position = Vector3.MoveTowards(
                rig.Player.position,
                groundedTarget,
                moveSpeed * deltaTime);
            return SpendRunStamina(
                stamina,
                previousPosition,
                rig.Player.position,
                runStaminaPerMeter);
        }

        private static float SpendRunStamina(
            float stamina,
            Vector3 from,
            Vector3 to,
            float runStaminaPerMeter)
        {
            return OpponentStaminaModel.SpendRun(
                stamina,
                from,
                to,
                runStaminaPerMeter);
        }
    }
}
