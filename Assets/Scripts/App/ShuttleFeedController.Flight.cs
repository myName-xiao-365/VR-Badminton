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
            LogSensorHitDebug(
                "incoming_plan",
                $"opponentShot={opponentShot}|start={V(start)}|target={V(target)}|duration={F(duration)}|arcHeight={F(arcHeight)}|sourceSide={F(sourceSide)}");

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
            LogSensorHitDebug(
                "player_serve_plan",
                $"power={F(power)}|start={V(start)}|target={V(target)}|duration={F(duration)}|arcHeight={F(arcHeight)}|serviceSide={F(serviceSide)}");

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
                        -8f,
                        10f,
                        -5f);
                }
                else if (backhandNetPrepared)
                {
                    AlignOpponentRacketFace(
                        contactPoint,
                        GetOpponentBackhandNetWaitingRotation());
                    opponentBody.localRotation = Quaternion.Euler(
                        -8f,
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
                    ? Mathf.Max(1f, opponentSmashSpeedBeforeNet)
                    : 1f;
                if (previousPosition.z <= 0f)
                {
                    movementScale = isOpponentSmash
                        ? Mathf.Max(1f, opponentSmashSpeedAfterNet)
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
            pendingSwingPowerSpeed = Mathf.Max(pendingSwingPowerSpeed, pendingSwingSpeed);
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
            ShotType resolverShot = shot;
            ShuttlePlayerReturnPlan returnPlan;
            if (inputMode == BadmintonInputMode.Sensor)
            {
                bool sensorJumpSmash = !incomingFrontCourt &&
                    jumpActive &&
                    jumpOffset >= jumpHeight * 0.25f;
                returnPlan = shuttleReturnPlanner.CreateSensorPlayerReturnPlan(
                    start,
                    lastHitResult,
                    pendingSwingPowerSpeed,
                    CourtLengthScale,
                    mediumSwingSpeed,
                    fastSwingSpeed,
                    sensorReturnPowerExponent,
                    sensorAssistPowerRetention,
                    sensorMagnetPowerRetention,
                    sensorMaxAimYawDegrees,
                    sensorJumpSmash);
                shot = returnPlan.Shot;
            }
            else
            {
                returnPlan = shuttleReturnPlanner.CreatePlayerReturnPlan(
                    shot,
                    CourtLengthScale,
                    minimumSwingSpeed,
                    fastSwingSpeed,
                    pendingSwingSpeed);
                shot = returnPlan.Shot;
            }

            Vector3 target = returnPlan.Target;
            float duration = returnPlan.Duration;
            float arcHeight = returnPlan.ArcHeight;
            temporarySlowMotionArmed =
                temporarySlowMotionEnabled &&
                shot != ShotType.Miss;
            SetTrailForShot(shot);

            bool returnUsesClearArc = returnPlan.UsesHighArc;
            float returnApexT = returnPlan.ApexT;
            ShowLandingPrediction(start, target, returnApexT, returnUsesClearArc);

            float progress = 0f;
            Vector3 previousPosition = start;
            float opponentContactProgress = returnUsesClearArc
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
            LogSensorHitDebug(
                "player_return_plan",
                $"resolverShot={resolverShot}|shot={shot}|start={V(start)}|target={V(target)}|duration={F(duration)}|arcHeight={F(arcHeight)}|apexT={F(returnApexT)}|usesHighArc={B(returnUsesClearArc)}|rawPower01={F(returnPlan.RawPower01)}|effectivePower01={F(returnPlan.EffectivePower01)}|retention={F(returnPlan.ContactRetention)}|aim01={F(returnPlan.Aim01)}|aimYaw={F(returnPlan.AimYawDegrees)}|elevation01={F(returnPlan.Elevation01)}|lift01={F(returnPlan.Lift01)}|attack01={F(returnPlan.Attack01)}|jumpSmash={B(!incomingFrontCourt && jumpActive && jumpOffset >= jumpHeight * 0.25f)}|powerSpeed={F(pendingSwingPowerSpeed)}|opponentContactT={F(opponentContactProgress)}|opponentContact={V(opponentContactPoint)}|opponentReady={V(opponentReadyPosition)}|plannedOpponentShot={plannedOpponentShot}|plannedBackhand={B(plannedOverheadBackhand)}");

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
                LogSensorHitDebug(
                    "player_return_result",
                    $"result=opponent_miss|shot={shot}|progress={F(progress)}|opponentContact={V(opponentContactPoint)}|opponentReady={V(opponentReadyPosition)}|opponentDistance={F(OpponentDistanceTo(opponentReadyPosition))}");
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
                LogSensorHitDebug(
                    "player_return_result",
                    $"result=smash_winner|shot={shot}|progress={F(progress)}|opponentContact={V(opponentContactPoint)}");
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
                LogSensorHitDebug(
                    "player_return_result",
                    $"result=opponent_stamina_empty|shot={shot}|plannedOpponentShot={opponentShot}|opponentStamina={F(opponentStamina)}");
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
                opponentBody.localRotation = Quaternion.Euler(-8f, 10f, -5f);
            }
            else if (backhandNetPrepared)
            {
                AlignOpponentRacketFace(
                    opponentContactPoint,
                    GetOpponentBackhandNetWaitingRotation());
                opponentBody.localRotation = Quaternion.Euler(-8f, -45f, 5f);
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
            LogSensorHitDebug(
                "player_return_result",
                $"result=opponent_return|shot={shot}|opponentShot={opponentShot}|opponentReturnStart={V(contactStart)}|opponentReturnTarget={V(opponentReturnTarget)}|useBackhand={B(useBackhand)}|jumpSmash={B(jumpSmash)}");
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
            ShuttleOpponentReturnFlightPlan returnPlan =
                shuttleReturnPlanner.CreateOpponentReturnFlight(shot);
            LogSensorHitDebug(
                "opponent_return_plan",
                $"opponentShot={shot}|start={V(start)}|target={V(target)}|duration={F(returnPlan.Duration)}|arcHeight={F(returnPlan.ArcHeight)}|isSmash={B(returnPlan.IsSmash)}");
            SetTrailColors(
                returnPlan.Trail.StartColor,
                returnPlan.Trail.EndColor);

            yield return PlayIncomingShuttle(
                start,
                target,
                returnPlan.Duration,
                returnPlan.ArcHeight,
                returnPlan.IsSmash);
        }

        private Vector3 CreateOpponentReturnTarget(
            float sourceSide,
            OpponentShotType shot)
        {
            return shuttleReturnPlanner.CreateOpponentReturnTarget(
                sourceSide,
                shot,
                CourtLengthScale);
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
            ShuttleTrailPalette trail =
                shuttleReturnPlanner.GetPlayerTrailPalette(shot);
            SetTrailColors(trail.StartColor, trail.EndColor);
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
                ShuttleFlightMoveState state = new ShuttleFlightMoveState(
                    netFaultTriggered,
                    temporarySlowMotionArmed,
                    temporarySlowMotionActive);
                shuttleFlightRunner.Move(
                    shuttle,
                    apexProjection,
                    position,
                    ref previousPosition,
                    currentFlightHitter,
                    ref state,
                    RecordShuttleFrame);
                netFaultTriggered = state.NetFaultTriggered;
                temporarySlowMotionArmed = state.TemporarySlowMotionArmed;
                temporarySlowMotionActive = state.TemporarySlowMotionActive;
                if (state.TimeScaleChanged)
                {
                    Time.timeScale = state.TimeScale;
                }
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
