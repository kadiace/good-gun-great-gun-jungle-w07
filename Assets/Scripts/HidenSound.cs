using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class HidenSound : MonoBehaviour
    {
        [Header("발견 연출")]
        [SerializeField] private AudioClip _hidenOpenClip;
        [SerializeField, Range(0f, 1f)] private float _hidenOpenVolume = 1f;

        [Header("선물 수령")]
        [SerializeField] private AudioClip _giftOpenClip;
        [SerializeField, Range(0f, 1f)] private float _giftOpenVolume = 1f;

        private AudioSource _discoverySource;
        private bool _discoverySoundStarted;

        /// <summary>
        /// Starts the discovery clip once on the persistent SoundManager child.
        /// manager applies the tracked SFX channel and user mute state.
        /// </summary>
        internal void PlayDiscovery(SoundManager manager)
        {
            if (_discoverySoundStarted) return;
            _discoverySoundStarted = true;
            if (_hidenOpenClip == null) return;
            _discoverySource = manager.PlayPersistentEasterEggClip(_hidenOpenClip, transform, _hidenOpenVolume);
        }

        /// <summary>
        /// Stops and unregisters the discovery source when the day title appears or the return is abandoned.
        /// manager owns the source's SFX tracking entry.
        /// </summary>
        internal void StopDiscovery(SoundManager manager)
        {
            if (_discoverySource == null) return;
            manager.StopPersistentEasterEggClip(_discoverySource);
            _discoverySource = null;
        }

        /// <summary>
        /// Allows a later new-game discovery to play its clip once.
        /// Call after the previous discovery source has been stopped.
        /// </summary>
        internal void ResetDiscovery()
        {
            _discoverySoundStarted = false;
        }

        /// <summary>
        /// Plays the gift clip at the box through the ordinary tracked SFX path.
        /// position is the gift's world position.
        /// </summary>
        internal void PlayGiftOpen(SoundManager manager, Vector3 position)
        {
            manager.PlayClip(_giftOpenClip, position, _giftOpenVolume);
        }

        private void OnValidate()
        {
            _hidenOpenVolume = Mathf.Clamp01(_hidenOpenVolume);
            _giftOpenVolume = Mathf.Clamp01(_giftOpenVolume);
        }
    }
}
