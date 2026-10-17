using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Weapon Workshop/Player Sound")]
    public sealed class PlayerSound : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, InspectorName("사운드 매니저")] private SoundManager soundManager;

        [Header("걷기")]
        [SerializeField, InspectorName("걷기 클립"), Tooltip("비어 있으면 걷기 소리를 재생하지 않습니다.")]
        private AudioClip walkingFootstepClip;
        [SerializeField, Range(0f, 1f), InspectorName("걷기 음량")] private float walkingFootstepVolume = 0.2f;
        [SerializeField, Min(0.05f), InspectorName("기본 발걸음 간격 (초)")]
        private float walkingSoundInterval = 0.45f;

        [Header("달리기")]
        [SerializeField, InspectorName("달리기 클립"), Tooltip("비어 있으면 달리기 발걸음 소리를 재생하지 않습니다.")]
        private AudioClip sprintingFootstepClip;
        [SerializeField, Range(0f, 1f), InspectorName("달리기 음량")] private float sprintingFootstepVolume = 0.2f;
        [SerializeField, Min(0.1f), InspectorName("달리기 간격 배율"),
         Tooltip("실제 이동 속도로 계산한 발걸음 간격에 곱합니다. 클수록 소리가 뜸하게 납니다.")]
        private float sprintSoundIntervalMultiplier = 2.5f;

        [Header("달리기 시작")]
        [SerializeField, InspectorName("달리기 시작 클립"), Tooltip("지상에서 이동하며 Sprint 상태로 진입할 때 한 번 재생합니다. 빈 슬롯은 무음입니다.")]
        private AudioClip sprintStartClip;
        [SerializeField, Range(0f, 1f), InspectorName("달리기 시작 음량")] private float sprintStartVolume = 0.6f;

        [Header("점프")]
        [SerializeField, InspectorName("점프 클립")] private AudioClip jumpClip;
        [SerializeField, Range(0f, 1f), InspectorName("점프 음량")] private float jumpVolume = 0.85f;

        private FirstPersonPlayer player;
        private float walkingSoundElapsed;
        private bool isRunning;

        private bool CanReceive => isActiveAndEnabled && soundManager != null && soundManager.IsActiveInstance;

        private void OnEnable()
        {
            ResetMovement();
            // 재활성화와 도메인 재진입 시 같은 핸들러가 중복 등록되지 않게 한다.
            Unsubscribe();
            WorkshopSoundEffects.PlayerMovementUpdated += OnMovementUpdated;
            WorkshopSoundEffects.PlayerMovementStopped += OnMovementStopped;
            WorkshopSoundEffects.PlayerJumped += OnJumped;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDisable()
        {
            Unsubscribe();
            ResetMovement();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            ResetMovement();
        }

        private void Unsubscribe()
        {
            WorkshopSoundEffects.PlayerMovementUpdated -= OnMovementUpdated;
            WorkshopSoundEffects.PlayerMovementStopped -= OnMovementStopped;
            WorkshopSoundEffects.PlayerJumped -= OnJumped;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void Update()
        {
            if (!CanUsePlayer(player)) ResetMovement();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) ResetMovement();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) ResetMovement();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ResetMovement();
        private void OnSceneUnloaded(Scene scene) => ResetMovement();
        private void OnActiveSceneChanged(Scene previous, Scene next) => ResetMovement();

        private bool CanUsePlayer(FirstPersonPlayer source) => CanReceive && source != null &&
            source.isActiveAndEnabled && Application.isFocused && !GameManager.GameplayBlocked;

        private void SelectPlayer(FirstPersonPlayer source)
        {
            if (player == source) return;
            ResetMovement();
            player = source;
        }

        private void ResetMovement()
        {
            walkingSoundElapsed = 0f;
            isRunning = false;
            player = null;
        }

        private void OnMovementStopped(FirstPersonPlayer source)
        {
            if (player == source) ResetMovement();
        }

        private void OnMovementUpdated(FirstPersonPlayer source, float horizontalSpeed, float walkingSpeed,
            bool grounded, bool sprinting, float deltaTime)
        {
            if (!CanUsePlayer(source) || !grounded || horizontalSpeed <= 0.05f)
            {
                OnMovementStopped(source);
                return;
            }

            SelectPlayer(source);
            if (sprinting && !isRunning)
                Play(source, sprintStartClip, sprintStartVolume);
            isRunning = sprinting;

            walkingSoundElapsed += deltaTime;
            // 기존 속도 보정과 달리기 배율을 그대로 유지한다.
            float baseInterval = Mathf.Max(0.05f, walkingSoundInterval);
            float referenceSpeed = Mathf.Max(0.01f, walkingSpeed);
            float interval = Mathf.Clamp(baseInterval * referenceSpeed / horizontalSpeed,
                Mathf.Min(0.12f, baseInterval), baseInterval * 2f);
            if (sprinting) interval *= Mathf.Max(0.1f, sprintSoundIntervalMultiplier);
            if (walkingSoundElapsed < interval) return;

            walkingSoundElapsed = 0f;
            Play(source, sprinting ? sprintingFootstepClip : walkingFootstepClip,
                sprinting ? sprintingFootstepVolume : walkingFootstepVolume);
        }

        private void OnJumped(FirstPersonPlayer source)
        {
            if (!CanUsePlayer(source)) return;
            SelectPlayer(source);
            Play(source, jumpClip, jumpVolume);
        }

        private void Play(FirstPersonPlayer source, AudioClip clip, float volume)
        {
            if (clip != null) soundManager.PlayClip(clip, source.transform.position, volume);
        }
    }
}
