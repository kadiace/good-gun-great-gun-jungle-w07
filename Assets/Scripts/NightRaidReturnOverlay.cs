using System.Collections;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>Hides the synchronous scene activation work while returning from a raid.</summary>
    public sealed class NightRaidReturnOverlay : MonoBehaviour
    {
        private static readonly ProfilerMarker FadeMarker = new ProfilerMarker("NightRaidReturnOverlay.Fade");
        private static readonly ProfilerMarker LoadWaitMarker = new ProfilerMarker("NightRaidReturnOverlay.LoadWait");
        private static readonly ProfilerMarker CollectMarker = new ProfilerMarker("NightRaidReturnOverlay.GCCollect");
        private const float FadeDuration = .3f;
        private const float TransitionGrace = .3f;
        private const float TransitionTimeout = 10f;
        private static NightRaidReturnOverlay instance;
        private Canvas overlayCanvas;
        private CanvasGroup overlayGroup;
        private bool started;

        public static void BeginReturn(string sceneName)
        {
            if (instance == null)
            {
                var root = new GameObject("NightRaidReturnOverlay");
                instance = root.AddComponent<NightRaidReturnOverlay>();
                DontDestroyOnLoad(root);
            }
            if (instance.started) return;
            instance.started = true;
            instance.StartCoroutine(instance.Return(sceneName));
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
            // The overlay always starts hidden; scene state is not relied on for initialization.
            overlayCanvas.enabled = false;
            canvasObject.GetComponent<GraphicRaycaster>().enabled = false;
        }

        private IEnumerator Return(string sceneName)
        {
            yield return Fade(0f, 1f);
            PoolManager.ClearActive();

            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError("공방 씬을 비동기로 불러오지 못했습니다: " + sceneName);
                yield return Fade(1f, 0f);
                Finish();
                yield break;
            }

            operation.allowSceneActivation = false;
            // Markers must begin and end in the same frame, so only the per-frame check is sampled.
            while (true)
            {
                bool loaded;
                using (LoadWaitMarker.Auto()) loaded = operation.progress >= .9f;
                if (loaded) break;
                yield return null;
            }

            // Keep the existing save/restore and transfer-consumption order; collect before activation.
            using (CollectMarker.Auto()) System.GC.Collect();
            operation.allowSceneActivation = true;
            while (!operation.isDone) yield return null;

            yield return ReleaseAfterWorkshopTransition();
            yield return Fade(1f, 0f);
            Finish();
        }

        private IEnumerator ReleaseAfterWorkshopTransition()
        {
            float elapsed = 0f;
            DayTransitionUI transition = null;
            while (elapsed < TransitionGrace)
            {
                transition = Object.FindFirstObjectByType<DayTransitionUI>();
                if (transition != null && transition.IsTransitioning) break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (transition != null && transition.IsTransitioning)
            {
                elapsed = 0f;
                while (transition != null && transition.IsTransitioning && elapsed < TransitionTimeout)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
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
