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
        private void UpdateSwitchStyleCamera(bool immediate)
        {
            if (!useSwitchStyleCamera)
            {
                return;
            }

            ApplySwitchCameraPreset();

            if (gameplayCamera == null)
            {
                gameplayCamera = ResolveGameplayCamera();
                if (gameplayCamera == null)
                {
                    return;
                }
            }

            float racketX = racketFace != null ? racketFace.position.x : playerGroundPosition.x;
            float followSourceX = playerGroundPosition.x * 0.68f + racketX * 0.32f;
            float followX = Mathf.Clamp(
                followSourceX * Mathf.Max(0.78f, switchCameraLateralFollow),
                -switchCameraMaxLateralOffset,
                switchCameraMaxLateralOffset);
            float racketZ = racketFace != null ? racketFace.position.z : playerGroundPosition.z;
            float followSourceZ = playerGroundPosition.z * 0.82f + racketZ * 0.18f;
            float defaultPlayerZ = -2.7f * CourtLengthScale;
            float followZ = Mathf.Clamp(
                (followSourceZ - defaultPlayerZ) * switchCameraDepthFollow,
                -switchCameraMaxDepthOffset,
                switchCameraMaxDepthOffset);
            Vector3 targetPosition = switchCameraPosition + new Vector3(followX, 0f, followZ);
            Vector3 lookAt = switchCameraLookAt + new Vector3(followX * 0.9f, 0f, followZ * 0.72f);
            float blend = immediate
                ? 1f
                : 1f - Mathf.Exp(-switchCameraFollowSpeed * Time.deltaTime);

            Transform cameraTransform = gameplayCamera.transform;
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, targetPosition, blend);
            Vector3 lookDirection = lookAt - cameraTransform.position;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                cameraTransform.rotation = Quaternion.Slerp(
                    cameraTransform.rotation,
                    Quaternion.LookRotation(lookDirection.normalized, Vector3.up),
                    blend);
            }

            gameplayCamera.orthographic = false;
            gameplayCamera.fieldOfView = switchCameraFieldOfView;
        }

        private Camera ResolveGameplayCamera()
        {
            if (gameplayCameraOverride != null)
            {
                return gameplayCameraOverride;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera;
            }

            Camera[] cameras = FindObjectsOfType<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].enabled && cameras[i].gameObject.activeInHierarchy)
                {
                    return cameras[i];
                }
            }

            return null;
        }

        private void ApplySwitchCameraPreset()
        {
            if (!forceSwitchCameraPreset && switchCameraPresetVersion >= SwitchCameraPresetVersion)
            {
                return;
            }

            switchCameraPosition = new Vector3(0f, 6.9f, -12.1f);
            switchCameraLookAt = new Vector3(0f, 1.05f, -2.35f);
            switchCameraFieldOfView = 40f;
            switchCameraLateralFollow = 0.78f;
            switchCameraMaxLateralOffset = 2.4f;
            switchCameraDepthFollow = 0.28f;
            switchCameraMaxDepthOffset = 0.9f;
            switchCameraFollowSpeed = 12f;
            switchCameraPresetVersion = SwitchCameraPresetVersion;
        }

    }
}
