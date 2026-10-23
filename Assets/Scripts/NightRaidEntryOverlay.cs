using System;
using System.Collections;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>Hides the synchronous raid entry work behind a black cover.</summary>
    public sealed class NightRaidEntryOverlay : MonoBehaviour
    {
        private static readonly ProfilerMarker FadeMarker = new ProfilerMarker("NightRaidEntryOverlay.Fade");
        private static readonly ProfilerMarker LoadWaitMarker = new ProfilerMarker("NightRaidEntryOverlay.LoadWait");
        private static readonly ProfilerMarker ActivationWaitMarker = new ProfilerMarker("NightRaidEntryOverlay.ActivationWait");
        private static readonly ProfilerMarker CollectMarker = new ProfilerMarker("NightRaidEntryOverlay.GCCollect");
        private static readonly ProfilerMarker BgmPreloadMarker = new ProfilerMarker("NightRaidEntryOverlay.BgmPreload");
        private const float FadeDuration = .25f;
        private const float LoadTimeoutSeconds = 30f;
        private const int HoldFramesAfterActivation = 2;
        private static NightRaidEntryOverlay instance;
        private Canvas overlayCanvas;
        private CanvasGroup overlayGroup;
        private bool started;

        // GameManager가 커버가 떠 있는 동안 게임 조작/일시정지를 막는 데 쓴다.
        public static bool InputBlocked => instance != null && instance.started;

        public static void BeginEntry(string sceneName, Func<bool> onReady, Action onFailed)
        {
            if (instance == null)
            {
                var root = new GameObject("NightRaidEntryOverlay");
                instance = root.AddComponent<NightRaidEntryOverlay>();
                DontDestroyOnLoad(root);
            }
            if (instance.started) return;
            instance.started = true;
            instance.StartCoroutine(instance.Enter(sceneName, onReady, onFailed));
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            var canvasObject = new GameObject("Blackout", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(Image));
            canvasObject.transform.SetParent(transform, false);
            overlayCanvas = canvasObject.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = short.MaxValue;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var rect = canvasObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = canvasObject.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true;
            overlayGroup = canvasObject.GetComponent<CanvasGroup>();
            overlayGroup.alpha = 0f;
            overlayGroup.blocksRaycasts = true;
            overlayGroup.interactable = false;
            // 커버는 항상 숨김으로 시작한다. 씬에 저장된 활성 상태를 신뢰하지 않는다.
            overlayCanvas.enabled = false;
            canvasObject.GetComponent<GraphicRaycaster>().enabled = false;
        }

        private IEnumerator Enter(string sceneName, Func<bool> onReady, Action onFailed)
        {
            yield return Fade(0f, 1f);
            PoolManager.ClearActive();

            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError("습격 씬을 비동기로 불러오지 못했습니다: " + sceneName);
                yield return Fail(onFailed);
                yield break;
            }

            operation.allowSceneActivation = false;
            // 화면이 완전히 덮인 뒤에만 동기 로드를 시작한다. 페이드 중이면 페이드가 멈춰 보인다.
            using (BgmPreloadMarker.Auto()) SoundManager.PreloadRaidMusic(sceneName);

            float startedAt = Time.realtimeSinceStartup;
            while (true)
            {
                bool loaded;
                using (LoadWaitMarker.Auto()) loaded = operation.progress >= .9f;
                if (loaded) break;
                if (Time.realtimeSinceStartup - startedAt >= LoadTimeoutSeconds)
                {
                    Debug.LogError("습격 씬 로드가 제한 시간을 넘겼습니다: " + sceneName);
                    yield return Fail(onFailed);
                    yield break;
                }
                yield return null;
            }

            // 훈련 습격 준비처럼 새 씬 Start 전에만 확정해야 하는 상태 변경을 커버 밑에서 처리한다.
            if (onReady != null && !onReady())
            {
                yield return Fail(onFailed);
                yield break;
            }

            using (CollectMarker.Auto()) System.GC.Collect();
            operation.allowSceneActivation = true;
            while (true)
            {
                bool done;
                using (ActivationWaitMarker.Auto()) done = operation.isDone;
                if (done) break;
                yield return null;
            }
            // 활성화 프레임의 Awake/OnEnable/Start와 첫 Update까지 덮는다.
            for (int i = 0; i < HoldFramesAfterActivation; i++) yield return null;

            yield return Fade(1f, 0f);
            Finish();
        }

        private IEnumerator Fail(Action onFailed)
        {
            onFailed?.Invoke();
            yield return Fade(1f, 0f);
            Finish();
        }

        private IEnumerator Fade(float from, float to)
        {
            overlayCanvas.enabled = true;
            GetComponentInChildren<GraphicRaycaster>(true).enabled = true;
            overlayGroup.alpha = from;
            float elapsed = 0f;
            while (elapsed < FadeDuration)
            {
                using (FadeMarker.Auto())
                {
                    elapsed += Time.unscaledDeltaTime;
                    overlayGroup.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / FadeDuration));
                }
                yield return null;
            }
            overlayGroup.alpha = to;
        }

        private void Finish()
        {
            if (overlayCanvas != null) overlayCanvas.enabled = false;
            var raycaster = GetComponentInChildren<GraphicRaycaster>(true);
            if (raycaster != null) raycaster.enabled = false;
            if (instance == this) instance = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
