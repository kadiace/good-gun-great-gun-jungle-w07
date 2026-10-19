using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class SlotMachineSound : MonoBehaviour
    {
        private static SlotMachineSound _instance;

        [Header("Lever")]
        [SerializeField] private AudioClip _leverClip;
        [SerializeField, Range(0f, 1f)] private float _leverVolume = 1f;

        [Header("Rotation")]
        [SerializeField] private AudioClip _rotationClip;
        [SerializeField, Range(0f, 1f)] private float _rotationVolume = 1f;

        [Header("Reel Stop")]
        [SerializeField] private AudioClip _reelStopClip;
        [SerializeField, Range(0f, 1f)] private float _reelStopVolume = 1f;

        [Header("Payout")]
        [SerializeField] private AudioClip _payoutClip;
        [SerializeField, Range(0f, 1f)] private float _payoutVolume = 1f;

        [Header("Fail")]
        [SerializeField] private AudioClip _failClip;
        [SerializeField, Range(0f, 1f)] private float _failVolume = 1f;

        [Header("Destroy")]
        [SerializeField] private AudioClip _destroyClip;
        [SerializeField, Range(0f, 1f)] private float _destroyVolume = 1f;

        private AudioSource _rotationSource;
        private AudioSource _resultSource;

        private void OnEnable()
        {
            _instance = this;
        }

        private void OnDisable()
        {
            StopRotationLoop();
            StopResultSound();
            if (_instance == this) _instance = null;
        }

        private void OnDestroy()
        {
            StopRotationLoop();
            StopResultSound();
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// 레버 당김 효과음을 슬롯머신 위치에서 한 번 재생한다.
        /// position을 사용하며 클립이 지정되지 않았으면 재생하지 않는다.
        /// </summary>
        internal static void PlayLever(Vector3 position)
        {
            if (_instance == null) return;
            _instance.PlayOneShot(_instance._leverClip, position, _instance._leverVolume);
        }

        /// <summary>
        /// 이전 결과음을 끊고 릴 회전음을 한 번 시작한 뒤 사용할 회전 시간을 반환한다.
        /// followTarget을 AudioSource 위치 기준으로 사용하며 음원이 없거나 길이를 쓸 수 없으면 fallbackDuration을 반환한다.
        /// </summary>
        internal static float StartRotation(Transform followTarget, float fallbackDuration)
        {
            if (_instance == null) return fallbackDuration;
            _instance.StopResultSound();
            _instance.StopRotationLoop();
            AudioClip rotationClip = _instance._rotationClip;
            float duration = rotationClip != null ? rotationClip.length : fallbackDuration;
            if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration)) duration = fallbackDuration;

            SoundManager soundManager = SoundManager.ActiveInstance;
            if (soundManager != null && rotationClip != null)
            {
                _instance._rotationSource = soundManager.CreateLoop(
                    rotationClip, followTarget, _instance._rotationVolume);
                if (_instance._rotationSource != null) _instance._rotationSource.loop = false;
            }

            return duration;
        }

        /// <summary>
        /// 릴 하나가 결과 위치에 멈출 때 정지 효과음을 슬롯머신 위치에서 한 번 재생한다.
        /// position을 사용하며 클립이 지정되지 않았으면 재생하지 않는다.
        /// </summary>
        internal static void PlayReelStop(Vector3 position)
        {
            if (_instance == null) return;
            _instance.PlayOneShot(_instance._reelStopClip, position, _instance._reelStopVolume);
        }

        /// <summary>
        /// 현재 릴 회전 반복음을 멈추고 해당 AudioSource 오브젝트를 제거한다.
        /// 슬롯머신 회전 완료나 비활성화 시 호출한다.
        /// </summary>
        internal static void StopRotation()
        {
            if (_instance != null) _instance.StopRotationLoop();
        }

        /// <summary>
        /// 당첨금 수령 효과음을 슬롯머신 위치에서 한 번 재생한다.
        /// position을 사용하며 클립이 지정되지 않았으면 재생하지 않는다.
        /// </summary>
        internal static void PlayPayout(Vector3 position)
        {
            if (_instance == null) return;
            _instance.PlayResult(_instance._payoutClip, position, _instance._payoutVolume);
        }

        /// <summary>
        /// 모든 릴이 멈춘 뒤 꽝 결과의 실패 효과음을 슬롯머신 위치에서 한 번 재생한다.
        /// position을 사용하며 클립이 지정되지 않았으면 재생하지 않는다.
        /// </summary>
        internal static void PlayFail(Vector3 position)
        {
            if (_instance == null) return;
            _instance.PlayResult(_instance._failClip, position, _instance._failVolume);
        }

        /// <summary>
        /// 실제 파괴 판정 시 회전음과 결과음을 끊고 position에서 파괴음을 한 번 재생한다.
        /// 클립이나 활성 사운드 설정이 없으면 파괴음은 재생하지 않는다.
        /// </summary>
        internal static void PlayDestroyed(Vector3 position)
        {
            if (_instance == null) return;
            _instance.StopRotationLoop();
            _instance.StopResultSound();
            _instance.PlayOneShot(_instance._destroyClip, position, _instance._destroyVolume);
        }

        /// <summary>
        /// 기존 결과음을 끊고 clip을 position과 volume으로 한 번 재생한다.
        /// 다음 회전에서 정지할 수 있도록 공통 SFX 경로가 반환한 AudioSource를 보관한다.
        /// </summary>
        private void PlayResult(AudioClip clip, Vector3 position, float volume)
        {
            StopResultSound();
            SoundManager soundManager = SoundManager.ActiveInstance;
            if (clip != null && soundManager != null)
                _resultSource = soundManager.PlayClip(clip, position, volume);
        }

        /// <summary>
        /// 보관한 당첨금 수령음 또는 실패음을 즉시 멈추고 SFX 추적과 오브젝트를 정리한다.
        /// 다음 회전 시작과 컴포넌트 비활성화·파괴 시 결과음 참조를 비운다.
        /// </summary>
        private void StopResultSound()
        {
            if (_resultSource == null) return;
            SoundManager soundManager = SoundManager.ActiveInstance;
            if (soundManager != null) soundManager.StopLoop(_resultSource);
            else
            {
                _resultSource.Stop();
                Destroy(_resultSource.gameObject);
            }
            _resultSource = null;
        }

        /// <summary>
        /// 지정한 클립을 SoundManager의 공통 SFX 경로로 재생한다.
        /// clip, position, volume을 사용하며 클립 또는 활성 SoundManager가 없으면 재생하지 않는다.
        /// </summary>
        private void PlayOneShot(AudioClip clip, Vector3 position, float volume)
        {
            SoundManager soundManager = SoundManager.ActiveInstance;
            if (clip != null && soundManager != null) soundManager.PlayClip(clip, position, volume);
        }

        /// <summary>
        /// 소유 중인 회전 AudioSource를 SoundManager에서 해제하고 참조를 비운다.
        /// </summary>
        private void StopRotationLoop()
        {
            if (_rotationSource == null) return;
            SoundManager soundManager = SoundManager.ActiveInstance;
            if (soundManager != null) soundManager.StopLoop(_rotationSource);
            else
            {
                _rotationSource.Stop();
                Destroy(_rotationSource.gameObject);
            }
            _rotationSource = null;
        }
    }
}
