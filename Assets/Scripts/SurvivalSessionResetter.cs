using System.Collections;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop
{
    /// <summary>
    /// 생존 모드에서 플레이어 사망 시 저장을 거치지 않고 활성 씬을 새로고침하여 세션을 초기화하는 컴포넌트입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurvivalSessionResetter : MonoBehaviour
    {
        [Header("재시작 설정")]
        [SerializeField] private RaidPlayerHealth _playerHealth;
        [SerializeField, Min(0f)] private float _restartDelay = 1.5f;
        [SerializeField] private bool _showDeathMessage = true;
        [SerializeField] private string _deathMessage = "사망했습니다. 생존 세션을 재시작합니다...";

        private bool _isResetting;
        private Coroutine _resetCoroutine;

        public bool IsResetting => _isResetting;
        public float RestartDelay => _restartDelay;

        void Awake()
        {
            _isResetting = false;
            CachePlayerHealth();
        }

        void OnEnable()
        {
            CachePlayerHealth();
            if (_playerHealth != null)
            {
                _playerHealth.Died += OnPlayerDied;
            }
        }

        void OnDisable()
        {
            if (_playerHealth != null)
            {
                _playerHealth.Died -= OnPlayerDied;
            }
        }

        /// <summary>
        /// 씬 내의 RaidPlayerHealth 컴포넌트를 탐색하여 캐시합니다.
        /// </summary>
        private void CachePlayerHealth()
        {
            if (_playerHealth != null) return;
            _playerHealth = FindAnyObjectByType<RaidPlayerHealth>();
        }

        /// <summary>
        /// 플레이어 사망 이벤트 발생 시 세션 재시작 코루틴을 시작합니다.
        /// </summary>
        private void OnPlayerDied()
        {
            if (_isResetting) return;
            _isResetting = true;

            if (_showDeathMessage)
            {
                FeedbackUI.For(this)?.ShowNotification(_deathMessage);
            }

            _resetCoroutine = StartCoroutine(RestartSessionRoutine());
        }

        /// <summary>
        /// 지정된 지연 시간 대기 후 현재 생존 씬을 다시 로드하여 세션을 초기화합니다.
        /// </summary>
        private IEnumerator RestartSessionRoutine()
        {
            if (_restartDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(_restartDelay);
            }

            string currentSceneName = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(currentSceneName))
            {
                SceneManager.LoadScene(currentSceneName);
            }
        }
    }
}
