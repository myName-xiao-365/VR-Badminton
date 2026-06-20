using System;
using UnityEngine;

namespace VRBadminton.App
{
    internal struct ShuttleFlightMoveState
    {
        public ShuttleFlightMoveState(
            bool netFaultTriggered,
            bool temporarySlowMotionArmed,
            bool temporarySlowMotionActive)
        {
            NetFaultTriggered = netFaultTriggered;
            TemporarySlowMotionArmed = temporarySlowMotionArmed;
            TemporarySlowMotionActive = temporarySlowMotionActive;
            TimeScaleChanged = false;
            TimeScale = 1f;
        }

        public bool NetFaultTriggered;
        public bool TemporarySlowMotionArmed;
        public bool TemporarySlowMotionActive;
        public bool TimeScaleChanged;
        public float TimeScale;
    }

    internal sealed class ShuttleFlightRunner
    {
        public void Move(
            Transform shuttle,
            Transform apexProjection,
            Vector3 position,
            ref Vector3 previousPosition,
            int currentFlightHitter,
            ref ShuttleFlightMoveState state,
            Action<Vector3, Vector3> recordShuttleFrame)
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
                    state.NetFaultTriggered = true;
                    crossingPoint.z = currentFlightHitter == 1 ? -0.035f : 0.035f;
                    shuttle.position = crossingPoint;
                    previousPosition = crossingPoint;
                    return;
                }

                if (currentFlightHitter == 1 &&
                    state.TemporarySlowMotionArmed &&
                    !state.TemporarySlowMotionActive)
                {
                    state.TemporarySlowMotionArmed = false;
                    state.TemporarySlowMotionActive = true;
                    state.TimeScale = 0.2f;
                    state.TimeScaleChanged = true;
                }
                else if (currentFlightHitter == 2 &&
                         state.TemporarySlowMotionActive)
                {
                    state.TemporarySlowMotionActive = false;
                    state.TimeScale = 1f;
                    state.TimeScaleChanged = true;
                }
            }

            Vector3 direction = position - previousPosition;
            if (direction.sqrMagnitude > 0.00001f)
            {
                shuttle.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            Vector3 velocity = direction / Mathf.Max(Time.deltaTime, 0.001f);
            recordShuttleFrame(position, velocity);
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
}
