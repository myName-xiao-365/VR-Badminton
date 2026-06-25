using UnityEngine;

namespace VRBadminton.App
{
    public sealed partial class ShuttleFeedController
    {
        private const string PowerHitAudioResource = "Audio/BadmintonPowerHit";

        private AudioSource hitAudioSource;
        private AudioClip powerHitAudioClip;

        private void CreateHitAudio()
        {
            powerHitAudioClip = Resources.Load<AudioClip>(PowerHitAudioResource);
            if (powerHitAudioClip == null)
            {
                Debug.LogWarning($"Hit audio resource not found: {PowerHitAudioResource}", this);
                return;
            }

            hitAudioSource = gameObject.AddComponent<AudioSource>();
            hitAudioSource.playOnAwake = false;
            hitAudioSource.loop = false;
            hitAudioSource.spatialBlend = 0f;
            hitAudioSource.dopplerLevel = 0f;
        }

        private void PlayHitAudio(float powerQuality)
        {
            if (hitAudioSource == null || powerHitAudioClip == null)
            {
                return;
            }

            float power = Mathf.Clamp01(powerQuality);
            hitAudioSource.pitch = Mathf.Lerp(0.92f, 1.06f, power);
            hitAudioSource.PlayOneShot(powerHitAudioClip, Mathf.Lerp(0.48f, 1f, power));
        }
    }
}
