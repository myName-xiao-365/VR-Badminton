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
        private IEnumerator FeedOneShuttle()
        {
            OpponentShotType opponentShot = ChooseOpponentShot(false, true);
            SpendOpponentStamina(opponentShot);
            bool incomingClear =
                opponentShot == OpponentShotType.Clear ||
                opponentShot == OpponentShotType.Lift;
            float sourceSide = opponentScore % 2 == 1 ? 1f : -1f;

            Vector3 start = new Vector3(
                sourceSide * (1.55f + Random.Range(-0.06f, 0.06f)),
                Random.Range(1.45f, 1.6f),
                (2.05f + Random.Range(-0.06f, 0.06f)) * CourtLengthScale);

            float targetDepth = incomingClear
                ? Random.Range(5.98f, 6.58f)
                : Random.Range(2.2f, 3.15f);
            Vector3 target = new Vector3(
                -sourceSide * Random.Range(0.9f, 2.45f),
                0.09f,
                -targetDepth * CourtLengthScale);

            float duration = incomingClear ? clearDuration : dropShotDuration;
            float arcHeight = incomingClear
                ? clearArcHeight
                : opponentShot == OpponentShotType.Net ? 1.35f : dropShotArcHeight;

            awaitingOpponentServe = true;
            opponentServeReady = false;
            while (!opponentServeReady)
            {
                yield return null;
            }
            awaitingOpponentServe = false;

            yield return AnimateOpponentHit(start, sourceSide);
            start = opponentRacketFace.position;
            opponentReturningToCenter = true;
            if (incomingClear)
            {
                SetTrailColors(
                    new Color(1f, 0.9f, 0.42f, 0.82f),
                    new Color(1f, 0.82f, 0.25f, 0f));
            }
            else
            {
                SetTrailColors(
                    new Color(0.25f, 1f, 0.35f, 0.82f),
                    new Color(0.2f, 0.8f, 0.25f, 0f));
            }

            shuttle.position = start;
            shuttle.gameObject.SetActive(true);
            shuttleTrail.Clear();
            shuttleTrail.emitting = true;
            yield return PlayIncomingShuttle(start, target, duration, arcHeight, false);

            shuttleTrail.emitting = false;
            shuttle.gameObject.SetActive(false);
            landingMarker.gameObject.SetActive(false);
            playerPositionMarker.gameObject.SetActive(false);
            trajectoryGuide.gameObject.SetActive(false);
        }

        private IEnumerator PlayerServe()
        {
            opponentReturningToCenter = false;
            currentFlightHitter = 1;
            netFaultTriggered = false;
            float serviceSide = playerScore % 2 == 1 ? -1f : 1f;
            playerServeSide = serviceSide;
            playerGroundPosition = new Vector3(
                serviceSide * 1.55f,
                0.55f,
                -2.05f * CourtLengthScale);

            Vector3 start = new Vector3(
                serviceSide * 1.55f,
                1.05f,
                -2.05f * CourtLengthScale);
            shuttle.position = GetServeContactPosition();
            shuttle.gameObject.SetActive(true);
            shuttleTrail.Clear();
            shuttleTrail.emitting = false;
            awaitingPlayerServe = true;
            playerServeGestureReady = false;
            swingPending = false;

            while (!playerServeGestureReady)
            {
                shuttle.position = GetServeContactPosition();
                yield return null;
            }

            awaitingPlayerServe = false;
            float power = Mathf.Clamp01(
                Mathf.InverseLerp(minimumSwingSpeed, fastSwingSpeed, playerServeSpeed));
            float targetDepth = Mathf.Lerp(2.45f, 5.9f, power);
            Vector3 target = new Vector3(
                -serviceSide * Random.Range(0.85f, 2.2f),
                0.09f,
                targetDepth * CourtLengthScale);
            float duration = Mathf.Lerp(1.5f, 1.9f, power);
            float arcHeight = Mathf.Lerp(1.55f, 4.35f, power);

            SetTrailColors(
                new Color(1f, 0.9f, 0.42f, 0.82f),
                new Color(1f, 0.82f, 0.25f, 0f));
            shuttleTrail.Clear();
            shuttleTrail.emitting = true;

            Vector3 serveStart = shuttle.position;
            float progress = 0f;
            Vector3 previousPosition = serveStart;
            bool serveUsesClearArc = power >= 0.55f;
            float serveApexT = serveUsesClearArc ? 0.7f : 0.5f;
            float opponentContactProgress = serveUsesClearArc
                ? FindDescendingContactProgress(
                    serveStart,
                    target,
                    arcHeight,
                    serveApexT,
                    2.55f)
                : 0.86f;
            Vector3 contactPoint = EvaluateArc(
                serveStart,
                target,
                opponentContactProgress,
                arcHeight,
                serveApexT);
            float opponentSide = contactPoint.x >= 0f ? 1f : -1f;
            Vector3 readyPosition = GetOpponentReadyPosition(contactPoint, opponentSide);
            bool plannedBackhand = ShouldOpponentUseBackhand(contactPoint);
            OpponentShotType plannedReturnShot = serveUsesClearArc
                ? (Random.value < 0.5f
                    ? OpponentShotType.Clear
                    : OpponentShotType.Drop)
                : OpponentShotType.Net;
            bool plannedForehandNet =
                !serveUsesClearArc &&
                plannedReturnShot == OpponentShotType.Net &&
                !plannedBackhand;
            bool plannedBackhandNet =
                !serveUsesClearArc &&
                plannedReturnShot == OpponentShotType.Net &&
                plannedBackhand;

            ShowLandingPrediction(serveStart, target, serveApexT, serveUsesClearArc);

            while (progress < opponentContactProgress)
            {
                progress = Mathf.Min(1f, progress + Time.deltaTime / duration);
                Vector3 position = EvaluateArc(
                    serveStart,
                    target,
                    progress,
                    arcHeight,
                    serveApexT);
                MoveShuttle(position, ref previousPosition);
                if (netFaultTriggered)
                {
                    yield return ResolveNetFault();
                    yield break;
                }

                if (serveUsesClearArc)
                {
                    if (plannedBackhand)
                    {
                        UpdateOpponentBackhandClearPreparation(
                            progress / opponentContactProgress,
                            readyPosition,
                            contactPoint);
                    }
                    else
                    {
                        UpdateOpponentForehandClearPreparation(
                            progress / opponentContactProgress,
                            readyPosition,
                            contactPoint);
                    }
                }
                else if (plannedForehandNet)
                {
                    UpdateOpponentForehandNetPreparation(
                        progress / opponentContactProgress,
                        readyPosition,
                        contactPoint);
                }
                else if (plannedBackhandNet)
                {
                    UpdateOpponentBackhandNetPreparation(
                        progress / opponentContactProgress,
                        readyPosition,
                        contactPoint);
                }
                else
                {
                    MoveOpponentTowards(readyPosition, contactPoint, opponentSide);
                }
                if (opponentStamina <= 0f)
                {
                    break;
                }

                yield return null;
            }

            HideLandingPrediction();
            if (opponentStamina <= 0f ||
                OpponentDistanceTo(readyPosition) > opponentReachTolerance)
            {
                while (progress < 1f)
                {
                    progress = Mathf.Min(1f, progress + Time.deltaTime / duration);
                    Vector3 position = EvaluateArc(
                        serveStart,
                        target,
                        progress,
                        arcHeight,
                        serveApexT);
                    MoveShuttle(position, ref previousPosition);
                    if (netFaultTriggered)
                    {
                        yield return ResolveNetFault();
                        yield break;
                    }
                    yield return null;
                }

                rallyWinner = 1;
                yield return new WaitForSeconds(0.45f);
            }
            else
            {
                OpponentShotType returnShot = plannedReturnShot;
                if (!CanOpponentAfford(returnShot))
                {
                    rallyWinner = 1;
                    yield return new WaitForSeconds(0.45f);
                    yield break;
                }

                SpendOpponentStamina(returnShot);
                bool backhand = serveUsesClearArc
                    ? plannedBackhand
                    : ShouldOpponentUseBackhand(contactPoint);
                Vector3 returnTarget = CreateOpponentReturnTarget(
                    opponentSide,
                    returnShot);
                PrepareOpponentRacket(
                    contactPoint,
                    opponentSide,
                    backhand,
                    false);
                bool forehandOverheadPrepared =
                    serveUsesClearArc &&
                    !plannedBackhand;
                bool backhandOverheadPrepared =
                    serveUsesClearArc &&
                    plannedBackhand;
                bool forehandNetPrepared = plannedForehandNet;
                bool backhandNetPrepared = plannedBackhandNet;
                if (forehandOverheadPrepared)
                {
                    Quaternion contactRotation = returnShot == OpponentShotType.Clear
                        ? GetOpponentClearContactRotation(contactPoint, returnTarget)
                        : Quaternion.Euler(0f, 180f, 0f);
                    AlignOpponentRacketFace(
                        contactPoint,
                        contactRotation);
                    opponentBody.localRotation = Quaternion.identity;
                }
                else if (backhandOverheadPrepared)
                {
                    Quaternion contactRotation = returnShot == OpponentShotType.Clear
                        ? GetOpponentClearContactRotation(contactPoint, returnTarget)
                        : Quaternion.Euler(0f, 0f, 0f);
                    AlignOpponentRacketFace(
                        contactPoint,
                        contactRotation);
                    opponentBody.localRotation = Quaternion.Euler(
                        0f,
                        -180f,
                        0f);
                }
                else if (forehandNetPrepared)
                {
                    AlignOpponentRacketFace(
                        contactPoint,
                        GetOpponentForehandNetWaitingRotation());
                    opponentBody.localRotation = Quaternion.Euler(
                        8f,
                        10f,
                        -5f);
                }
                else if (backhandNetPrepared)
                {
                    AlignOpponentRacketFace(
                        contactPoint,
                        GetOpponentBackhandNetWaitingRotation());
                    opponentBody.localRotation = Quaternion.Euler(
                        8f,
                        -45f,
                        5f);
                }
                StartCoroutine(AnimateOpponentSwing(
                    opponentSide,
                    backhand,
                    returnShot,
                    false,
                    forehandOverheadPrepared,
                    backhandOverheadPrepared,
                    forehandNetPrepared,
                    backhandNetPrepared));
                if (returnShot == OpponentShotType.Clear)
                {
                    AlignOpponentRacketFace(
                        contactPoint,
                        GetOpponentClearContactRotation(
                            contactPoint,
                            returnTarget));
                }
                Vector3 racketContact = opponentRacketFace.position;
                opponentReturningToCenter = true;
                yield return OpponentReturn(
                    racketContact,
                    returnTarget,
                    returnShot);
            }

            shuttleTrail.emitting = false;
            shuttle.gameObject.SetActive(false);
            landingMarker.gameObject.SetActive(false);
            playerPositionMarker.gameObject.SetActive(false);
        }

        private Vector3 GetServeContactPosition()
        {
            float racketSide = isBackhand ? -0.95f : 0.95f;
            Vector3 racketPivot = new Vector3(
                playerGroundPosition.x + racketSide,
                0.65f,
                playerGroundPosition.z);
            Vector3 swingPathDirection =
                Quaternion.Euler(-10f, 0f, 0f) * Vector3.up;
            return racketPivot + swingPathDirection * 1.22f;
        }

        private IEnumerator PlayIncomingShuttle(
            Vector3 start,
            Vector3 target,
            float duration,
            float arcHeight,
            bool isOpponentSmash)
        {
            currentFlightHitter = 2;
            netFaultTriggered = false;
            bool incomingUsesClearArc = arcHeight >= 3f;
            float incomingApexT = incomingUsesClearArc ? 0.7f : 0.5f;
            ShowLandingPrediction(start, target, incomingApexT, incomingUsesClearArc);
            incomingFrontCourt = Mathf.Abs(target.z) < 4f * CourtLengthScale;
            incomingHighClear = arcHeight >= 3f && !isOpponentSmash;
            hasPlayerContactPrediction = incomingHighClear;
            if (hasPlayerContactPrediction)
            {
                float contactProgress = FindDescendingContactProgress(
                    start,
                    target,
                    arcHeight,
                    incomingApexT,
                    2.45f);
                playerPredictedContactPoint = EvaluateArc(
                    start,
                    target,
                    contactProgress,
                    arcHeight,
                    incomingApexT);
            }
            incomingOpponentSmash = isOpponentSmash;
            if (!isOpponentSmash)
            {
                smashReceiveReady = false;
            }
            playerPositionMarker.gameObject.SetActive(true);
            UpdatePlayerPositionMarker();
            shuttleIncoming = true;
            swingPending = false;
            pendingSwingStartedAt = 0f;
            ClearHitHistory();

            float progress = 0f;
            Vector3 previousPosition = start;
            RecordShuttleFrame(start, Vector3.zero);
            while (progress < 1f)
            {
                float movementScale = isOpponentSmash
                    ? opponentSmashSpeedBeforeNet
                    : 1f;
                if (previousPosition.z <= 0f)
                {
                    movementScale = isOpponentSmash
                        ? opponentSmashSpeedAfterNet
                        : speedAfterNet;
                }
                progress = Mathf.Min(1f, progress + Time.deltaTime * movementScale / duration);
                Vector3 position = EvaluateArc(
                    start,
                    target,
                    progress,
                    arcHeight,
                    incomingApexT);
                MoveShuttle(position, ref previousPosition);
                if (netFaultTriggered)
                {
                    yield return ResolveNetFault();
                    yield break;
                }

                if (TryHitShuttle(out ShotType shot))
                {
                    shuttleIncoming = false;
                    hasPlayerContactPrediction = false;
                    HideLandingPrediction();
                    playerPositionMarker.gameObject.SetActive(false);
                    yield return ReturnShuttle(shot, previousPosition);
                    yield break;
                }

                yield return null;
            }

            shuttleIncoming = false;
            incomingHighClear = false;
            hasPlayerContactPrediction = false;
            incomingOpponentSmash = false;
            smashReceiveReady = false;
            shuttle.position = target;
            HideLandingPrediction();
            playerPositionMarker.gameObject.SetActive(false);
            rallyWinner = 2;
            yield return new WaitForSeconds(0.45f);
        }

        private bool TryHitShuttle(out ShotType shot)
        {
            shot = ShotType.Net;
            if (!swingPending)
            {
                return false;
            }

            RacketHitContext context = new RacketHitContext
            {
                Now = Time.time,
                SwingStartedAt = pendingSwingStartedAt > 0f ? pendingSwingStartedAt : Time.time,
                SwingExpiresAt = pendingSwingStartedAt > 0f ? pendingSwingStartedAt + contactWindow : Time.time,
                SwingPending = swingPending,
                SwingUpward = swingUpward,
                SwingSpeed = pendingSwingSpeed,
                SwingStartAngle = pendingStartAngle,
                IncomingFrontCourt = incomingFrontCourt,
                IncomingOpponentSmash = incomingOpponentSmash,
                SmashReceiveReady = smashReceiveReady,
                IsBackhand = isBackhand,
                MinimumSwingSpeed = minimumSwingSpeed,
                MediumSwingSpeed = mediumSwingSpeed,
                FastSwingSpeed = fastSwingSpeed
            };

            using (HitResolveMarker.Auto())
            {
                lastHitResult = hitResolver.Resolve(
                    racketHistory,
                    shuttleHistory,
                    context,
                    CurrentHitSettings());
            }
            if (lastHitResult.Hit || lastHitResult.ConsumeSwing)
            {
                LogSensorHitDebug("hit_resolve", HitResultFields(lastHitResult));
            }

            if (lastHitResult.ConsumeSwing)
            {
                swingPending = false;
                pendingSwingStartedAt = 0f;
            }

            if (!lastHitResult.Hit)
            {
                return false;
            }

            pendingSwingSpeed = lastHitResult.SwingSpeed;
            pendingStartAngle = lastHitResult.FaceAngle;
            swingUpward = lastHitResult.SwingUpward;
            shot = MapResolvedShot(lastHitResult.Shot);
            if (incomingOpponentSmash)
            {
                incomingOpponentSmash = false;
                smashReceiveReady = false;
            }

            if (shot == ShotType.Miss)
            {
                return false;
            }

            ResetPlayerBackhandAfterHit();
            return true;
        }

        private void ResetPlayerBackhandAfterHit()
        {
            if (isBackhand)
            {
                StartCoroutine(ReturnPlayerToForehand());
            }
        }

        private IEnumerator ReturnPlayerToForehand()
        {
            yield return new WaitForSeconds(0.16f);
            isBackhand = false;
        }

        private static ShotType MapResolvedShot(RacketResolvedShot shot)
        {
            switch (shot)
            {
                case RacketResolvedShot.Net:
                    return ShotType.Net;
                case RacketResolvedShot.Drop:
                    return ShotType.Drop;
                case RacketResolvedShot.Clear:
                    return ShotType.Clear;
                case RacketResolvedShot.Smash:
                    return ShotType.Smash;
                case RacketResolvedShot.Drive:
                    return ShotType.Drive;
                case RacketResolvedShot.Out:
                    return ShotType.Out;
                default:
                    return ShotType.Miss;
            }
        }

        private IEnumerator ReturnShuttle(ShotType shot, Vector3 start)
        {
            opponentReturningToCenter = false;
            currentFlightHitter = 1;
            netFaultTriggered = false;
            temporarySlowMotionArmed =
                temporarySlowMotionEnabled &&
                shot != ShotType.Miss;
            Vector3 target;
            float duration;
            float arcHeight;
            SetTrailForShot(shot);

            switch (shot)
            {
                case ShotType.Drop:
                    target = new Vector3(
                        Random.Range(-2.2f, 2.2f),
                        0.09f,
                        Random.Range(1.55f, 2.15f) * CourtLengthScale);
                    duration = 1.1f;
                    arcHeight = 1.35f;
                    break;
                case ShotType.Clear:
                    target = new Vector3(Random.Range(-2.3f, 2.3f), 0.09f, 6.2f * CourtLengthScale);
                    duration = 2f;
                    arcHeight = 4.5f;
                    break;
                case ShotType.Smash:
                    target = new Vector3(Random.Range(-2.25f, 2.25f), 0.09f, 4.7f * CourtLengthScale);
                    duration = Mathf.Lerp(
                        0.82f,
                        0.42f,
                        Mathf.InverseLerp(minimumSwingSpeed, fastSwingSpeed, pendingSwingSpeed));
                    arcHeight = 0.18f;
                    break;
                case ShotType.Drive:
                    target = new Vector3(Random.Range(-2.3f, 2.3f), 0.09f, 6.45f * CourtLengthScale);
                    duration = 0.82f;
                    arcHeight = 0.55f;
                    break;
                case ShotType.Out:
                    target = new Vector3(Random.Range(-3.8f, 3.8f), 0.09f, 8.25f * CourtLengthScale);
                    duration = 1.25f;
                    arcHeight = 1.5f;
                    break;
                default:
                    target = new Vector3(
                        Random.Range(-1.9f, 1.9f),
                        0.09f,
                        Random.Range(1.45f, 1.95f) * CourtLengthScale);
                    duration = 1.05f;
                    arcHeight = 1.35f;
                    break;
            }

            bool returnUsesClearArc = shot == ShotType.Clear;
            float returnApexT = returnUsesClearArc ? 0.7f : 0.5f;
            ShowLandingPrediction(start, target, returnApexT, returnUsesClearArc);

            float progress = 0f;
            Vector3 previousPosition = start;
            float opponentContactProgress = shot == ShotType.Clear
                ? FindDescendingContactProgress(
                    start,
                    target,
                    arcHeight,
                    returnApexT,
                    2.55f)
                : 0.86f;
            Vector3 opponentContactPoint = EvaluateArc(
                start,
                target,
                opponentContactProgress,
                arcHeight,
                returnApexT);
            float opponentSide = opponentContactPoint.x >= 0f ? 1f : -1f;
            Vector3 opponentReadyPosition = GetOpponentReadyPosition(
                opponentContactPoint,
                opponentSide);
            OpponentShotType plannedOpponentShot = shot == ShotType.Drop
                ? OpponentShotType.Net
                : ChooseOpponentShot(
                    shot == ShotType.Clear,
                    Mathf.Abs(opponentContactPoint.z) < 4f * CourtLengthScale);
            bool plannedOverhead =
                plannedOpponentShot == OpponentShotType.Clear ||
                plannedOpponentShot == OpponentShotType.Drop;
            bool plannedOverheadBackhand =
                ShouldOpponentUseBackhand(opponentContactPoint);
            bool plannedForehandNet =
                plannedOpponentShot == OpponentShotType.Net &&
                !plannedOverheadBackhand;
            bool plannedBackhandNet =
                plannedOpponentShot == OpponentShotType.Net &&
                plannedOverheadBackhand;

            while (progress < opponentContactProgress)
            {
                progress = Mathf.Min(1f, progress + Time.deltaTime / duration);
                Vector3 position = EvaluateArc(
                    start,
                    target,
                    progress,
                    arcHeight,
                    returnApexT);
                MoveShuttle(position, ref previousPosition);
                if (netFaultTriggered)
                {
                    yield return ResolveNetFault();
                    yield break;
                }
                if (plannedOverhead)
                {
                    if (plannedOverheadBackhand)
                    {
                        UpdateOpponentBackhandClearPreparation(
                            progress / opponentContactProgress,
                            opponentReadyPosition,
                            opponentContactPoint);
                    }
                    else
                    {
                        UpdateOpponentForehandClearPreparation(
                            progress / opponentContactProgress,
                            opponentReadyPosition,
                            opponentContactPoint);
                    }
                }
                else if (plannedForehandNet)
                {
                    UpdateOpponentForehandNetPreparation(
                        progress / opponentContactProgress,
                        opponentReadyPosition,
                        opponentContactPoint);
                }
                else if (plannedBackhandNet)
                {
                    UpdateOpponentBackhandNetPreparation(
                        progress / opponentContactProgress,
                        opponentReadyPosition,
                        opponentContactPoint);
                }
                else
                {
                    MoveOpponentTowards(
                        opponentReadyPosition,
                        opponentContactPoint,
                        opponentSide);
                }
                if (opponentStamina <= 0f)
                {
                    break;
                }

                yield return null;
            }

            HideLandingPrediction();
            bool opponentReached =
                opponentStamina > 0f &&
                OpponentDistanceTo(opponentReadyPosition) <= opponentReachTolerance;
            if (!opponentReached)
            {
                while (progress < 1f)
                {
                    progress = Mathf.Min(1f, progress + Time.deltaTime / duration);
                    Vector3 position = EvaluateArc(
                        start,
                        target,
                        progress,
                        arcHeight,
                        returnApexT);
                    MoveShuttle(position, ref previousPosition);
                    if (netFaultTriggered)
                    {
                        yield return ResolveNetFault();
                        yield break;
                    }
                    yield return null;
                }

                rallyWinner = 1;
                yield return new WaitForSeconds(0.45f);
                yield break;
            }

            if (shot == ShotType.Smash && Random.value > opponentSmashReceiveChance)
            {
                while (progress < 1f)
                {
                    progress = Mathf.Min(1f, progress + Time.deltaTime / duration);
                    Vector3 position = EvaluateArc(
                        start,
                        target,
                        progress,
                        arcHeight,
                        returnApexT);
                    MoveShuttle(position, ref previousPosition);
                    if (netFaultTriggered)
                    {
                        yield return ResolveNetFault();
                        yield break;
                    }
                    yield return null;
                }

                rallyWinner = 1;
                yield return new WaitForSeconds(0.45f);
                yield break;
            }

            OpponentShotType opponentShot = plannedOpponentShot;
            if (!CanOpponentAfford(opponentShot))
            {
                rallyWinner = 1;
                yield return new WaitForSeconds(0.45f);
                yield break;
            }

            SpendOpponentStamina(opponentShot);
            if (opponentShot == OpponentShotType.Smash)
            {
                incomingOpponentSmash = true;
                smashReceiveReady = false;
            }

            bool useBackhand = ShouldOpponentUseBackhand(opponentContactPoint);
            bool jumpSmash = opponentShot == OpponentShotType.Smash;
            Vector3 opponentReturnTarget = CreateOpponentReturnTarget(
                opponentSide,
                opponentShot);
            PrepareOpponentRacket(
                opponentContactPoint,
                opponentSide,
                useBackhand,
                jumpSmash);
            bool forehandClearPrepared =
                plannedOverhead &&
                !plannedOverheadBackhand;
            bool backhandOverheadPrepared =
                plannedOverhead &&
                plannedOverheadBackhand;
            bool forehandNetPrepared = plannedForehandNet;
            bool backhandNetPrepared = plannedBackhandNet;
            if (forehandClearPrepared)
            {
                Quaternion contactRotation = opponentShot == OpponentShotType.Clear
                    ? GetOpponentClearContactRotation(
                        opponentContactPoint,
                        opponentReturnTarget)
                    : Quaternion.Euler(0f, 180f, 0f);
                AlignOpponentRacketFace(
                    opponentContactPoint,
                    contactRotation);
                opponentBody.localRotation = Quaternion.identity;
            }
            else if (backhandOverheadPrepared)
            {
                Quaternion contactRotation = opponentShot == OpponentShotType.Clear
                    ? GetOpponentClearContactRotation(
                        opponentContactPoint,
                        opponentReturnTarget)
                    : Quaternion.Euler(0f, 0f, 0f);
                AlignOpponentRacketFace(
                    opponentContactPoint,
                    contactRotation);
                opponentBody.localRotation = Quaternion.Euler(0f, -180f, 0f);
            }
            else if (forehandNetPrepared)
            {
                AlignOpponentRacketFace(
                    opponentContactPoint,
                    GetOpponentForehandNetWaitingRotation());
                opponentBody.localRotation = Quaternion.Euler(8f, 10f, -5f);
            }
            else if (backhandNetPrepared)
            {
                AlignOpponentRacketFace(
                    opponentContactPoint,
                    GetOpponentBackhandNetWaitingRotation());
                opponentBody.localRotation = Quaternion.Euler(8f, -45f, 5f);
            }
            StartCoroutine(AnimateOpponentSwing(
                opponentSide,
                useBackhand,
                opponentShot,
                shot == ShotType.Smash,
                forehandClearPrepared,
                backhandOverheadPrepared,
                forehandNetPrepared,
                backhandNetPrepared));
            if (opponentShot == OpponentShotType.Clear)
            {
                AlignOpponentRacketFace(
                    opponentContactPoint,
                    GetOpponentClearContactRotation(
                        opponentContactPoint,
                        opponentReturnTarget));
            }
            Vector3 contactStart = opponentRacketFace.position;
            opponentReturningToCenter = true;
            yield return OpponentReturn(
                contactStart,
                opponentReturnTarget,
                opponentShot);
        }

        private IEnumerator OpponentReturn(
            Vector3 start,
            Vector3 target,
            OpponentShotType shot)
        {
            float duration;
            float arcHeight;
            bool isSmash = shot == OpponentShotType.Smash;

            switch (shot)
            {
                case OpponentShotType.Net:
                    duration = 1.15f;
                    arcHeight = 1.35f;
                    SetTrailColors(
                        new Color(0.25f, 1f, 0.35f, 0.82f),
                        new Color(0.2f, 0.8f, 0.25f, 0f));
                    break;
                case OpponentShotType.Drop:
                    duration = 1.35f;
                    arcHeight = 1.65f;
                    SetTrailColors(
                        new Color(0.25f, 1f, 0.35f, 0.82f),
                        new Color(0.2f, 0.8f, 0.25f, 0f));
                    break;
                case OpponentShotType.Smash:
                    duration = 0.85f;
                    arcHeight = 0.32f;
                    SetTrailColors(
                        new Color(1f, 0.12f, 0.08f, 0.9f),
                        new Color(0.85f, 0.02f, 0.02f, 0f));
                    break;
                default:
                    duration = shot == OpponentShotType.Lift ? 2.4f : 2.25f;
                    arcHeight = shot == OpponentShotType.Lift ? 5.25f : 4.85f;
                    SetTrailColors(
                        new Color(1f, 0.9f, 0.42f, 0.82f),
                        new Color(1f, 0.82f, 0.25f, 0f));
                    break;
            }

            yield return PlayIncomingShuttle(start, target, duration, arcHeight, isSmash);
        }

        private static Vector3 CreateOpponentReturnTarget(
            float sourceSide,
            OpponentShotType shot)
        {
            float targetDepth;
            switch (shot)
            {
                case OpponentShotType.Net:
                    targetDepth = Random.Range(1.45f, 1.95f);
                    break;
                case OpponentShotType.Drop:
                    targetDepth = Random.Range(1.75f, 2.25f);
                    break;
                case OpponentShotType.Smash:
                    targetDepth = Random.Range(3.4f, 5.2f);
                    break;
                default:
                    targetDepth = Random.Range(5.98f, 6.58f);
                    break;
            }

            return new Vector3(
                -sourceSide * Random.Range(0.85f, 2.45f),
                0.09f,
                -targetDepth * CourtLengthScale);
        }

        private Quaternion GetOpponentClearContactRotation(
            Vector3 contactPoint,
            Vector3 target)
        {
            Vector3 worldDirection = target - contactPoint;
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f)
            {
                return Quaternion.Euler(0f, 180f, 0f);
            }

            Vector3 localDirection = opponentPlayer.InverseTransformDirection(
                worldDirection.normalized);
            float yaw = Mathf.Atan2(localDirection.x, localDirection.z) *
                Mathf.Rad2Deg;
            return Quaternion.Euler(0f, yaw, 0f);
        }

        private void SetTrailForShot(ShotType shot)
        {
            switch (shot)
            {
                case ShotType.Clear:
                    SetTrailColors(
                        new Color(1f, 0.9f, 0.42f, 0.82f),
                        new Color(1f, 0.82f, 0.25f, 0f));
                    break;
                case ShotType.Smash:
                    SetTrailColors(
                        new Color(1f, 0.12f, 0.08f, 0.9f),
                        new Color(0.85f, 0.02f, 0.02f, 0f));
                    break;
                case ShotType.Drive:
                    SetTrailColors(
                        new Color(1f, 0.28f, 0.68f, 0.88f),
                        new Color(0.95f, 0.12f, 0.55f, 0f));
                    break;
                default:
                    SetTrailColors(
                        new Color(0.25f, 1f, 0.35f, 0.82f),
                        new Color(0.2f, 0.8f, 0.25f, 0f));
                    break;
            }
        }

        private void SetTrailColors(Color startColor, Color endColor)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(startColor, 0f),
                    new GradientColorKey(startColor, 0.4f),
                    new GradientColorKey(endColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(startColor.a, 0f),
                    new GradientAlphaKey(startColor.a * 0.72f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
            shuttleTrail.colorGradient = gradient;
            markerYellow.color = new Color(
                startColor.r,
                startColor.g,
                startColor.b,
                1f);
            trajectoryMaterial.color = new Color(
                startColor.r * 0.32f,
                startColor.g * 0.32f,
                startColor.b * 0.32f,
                1f);
        }

        private static Vector3 EvaluateArc(
            Vector3 start,
            Vector3 target,
            float t,
            float height,
            float apexT = 0.5f)
        {
            return ShuttleTrajectoryPlanner.Create(
                start,
                target,
                0f,
                height,
                apexT).Evaluate(t);
        }

        private static float FindDescendingContactProgress(
            Vector3 start,
            Vector3 target,
            float height,
            float apexT,
            float desiredHeight)
        {
            return ShuttleTrajectoryPlanner.Create(
                start,
                target,
                0f,
                height,
                apexT).FindDescendingContactProgress(desiredHeight);
        }

        private void MoveShuttle(Vector3 position, ref Vector3 previousPosition)
        {
            using (FlightMoveMarker.Auto())
            {
                bool crossedNet =
                    (previousPosition.z < 0f && position.z >= 0f) ||
                    (previousPosition.z > 0f && position.z <= 0f);
                if (crossedNet)
                {
                    float denominator = position.z - previousPosition.z;
                    float crossingT = Mathf.Abs(denominator) < 0.0001f
                        ? 0f
                        : Mathf.Clamp01(-previousPosition.z / denominator);
                    Vector3 crossingPoint = Vector3.Lerp(previousPosition, position, crossingT);
                    if (crossingPoint.y < 1.53f)
                    {
                        netFaultTriggered = true;
                        crossingPoint.z = currentFlightHitter == 1 ? -0.035f : 0.035f;
                        shuttle.position = crossingPoint;
                        previousPosition = crossingPoint;
                        return;
                    }

                    if (currentFlightHitter == 1 &&
                        temporarySlowMotionArmed &&
                        !temporarySlowMotionActive)
                    {
                        temporarySlowMotionArmed = false;
                        temporarySlowMotionActive = true;
                        Time.timeScale = 0.2f;
                    }
                    else if (currentFlightHitter == 2 &&
                        temporarySlowMotionActive)
                    {
                        temporarySlowMotionActive = false;
                        Time.timeScale = 1f;
                    }
                }

                Vector3 direction = position - previousPosition;
                if (direction.sqrMagnitude > 0.00001f)
                {
                    shuttle.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }

                Vector3 velocity = direction / Mathf.Max(Time.deltaTime, 0.001f);
                RecordShuttleFrame(position, velocity);
                shuttle.position = position;
                if (apexProjection.gameObject.activeSelf)
                {
                    apexProjection.position = new Vector3(
                        position.x,
                        0.034f,
                        position.z);
                    apexProjection.rotation = Quaternion.Euler(0f, 45f, 0f);
                }
                previousPosition = position;
            }
        }

        private IEnumerator ResolveNetFault()
        {
            HideLandingPrediction();
            playerPositionMarker.gameObject.SetActive(false);
            shuttleIncoming = false;
            incomingHighClear = false;
            hasPlayerContactPrediction = false;
            incomingOpponentSmash = false;
            smashReceiveReady = false;
            jumpActive = false;
            jumpOffset = 0f;

            Vector3 start = shuttle.position;
            Vector3 target = new Vector3(start.x, 0.09f, start.z);
            float elapsed = 0f;
            const float dropDuration = 0.38f;
            while (elapsed < dropDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dropDuration);
                shuttle.position = Vector3.Lerp(start, target, t);
                yield return null;
            }

            rallyWinner = CourtFaultResolver.ResolveNetFault(currentFlightHitter).RallyWinner;
            netFaultTriggered = false;
            yield return new WaitForSeconds(0.35f);
        }

    }
}
