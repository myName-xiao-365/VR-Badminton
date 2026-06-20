using System.Collections;
using UnityEngine;

namespace VRBadminton.App
{
    internal sealed class OpponentPoseAnimator
    {
        public IEnumerator AnimatePose(
            Transform opponentPlayer,
            Transform opponentBody,
            Transform opponentRacket,
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
    }
}
