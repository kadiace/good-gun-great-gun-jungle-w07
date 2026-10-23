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

            // UnloadScene의 Destroy 비용을 줄이려고 가림막이 덮인 뒤 적 삭제 준비를 먼저 끝낸다.
            RaidEnemyCore.PrepareAllForUnload();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 임시 진단: 복귀 멈춤 원인 확인 후 제거
            LogRaidSceneForUnload();
#endif

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
            Finish();
        }

        private IEnumerator ReleaseAfterWorkshopTransition()
        {
            float elapsed = 0f;
            DayTransitionUI transition = null;
            while (elapsed < TransitionGrace)
            {
                transition = Object.FindFirstObjectByType<DayTransitionUI>();
                // BeginReturn already covers the workshop. Hand over immediately so this
                // higher-sorting blackout does not hide its Day title and reveal fade.
                if (transition != null && transition.IsTransitioning) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Death returns and other paths without a day transition reveal the workshop here.
            yield return Fade(1f, 0f);
        }

        // 임시 진단: 복귀 멈춤 원인 확인 후 제거. 활성(습격) 씬의 루트별 물리 자산 규모를 한 번에 기록한다.
        private static void LogRaidSceneForUnload()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var report = new System.Text.StringBuilder();
            report.AppendLine($"[복귀 진단] 씬 '{scene.name}' 루트 {roots.Length}개");
            var sorted = new System.Collections.Generic.List<(int transforms, string line)>();
            foreach (var root in roots)
            {
                var transforms = root.GetComponentsInChildren<Transform>(true);
                var colliders = root.GetComponentsInChildren<Collider>(true);
                int activeColliders = 0;
                var bodyColliderCounts = new System.Collections.Generic.Dictionary<Rigidbody, int>();
                foreach (var collider in colliders)
                {
                    if (collider.enabled && collider.gameObject.activeInHierarchy) activeColliders++;
                    var body = collider.attachedRigidbody;
                    if (body == null) continue;
                    bodyColliderCounts.TryGetValue(body, out int count);
                    bodyColliderCounts[body] = count + 1;
                }
                var top = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<Rigidbody, int>>(bodyColliderCounts);
                top.Sort((a, b) => b.Value.CompareTo(a.Value));
                var line = new System.Text.StringBuilder();
                line.Append($"  '{root.name}': transforms={transforms.Length}")
                    .Append($", colliders={activeColliders}/{colliders.Length}")
                    .Append($", destructible={root.GetComponentsInChildren<DestructibleTarget>(true).Length}");
                for (int i = 0; i < top.Count && i < 5; i++)
                    line.Append($"\n    rb '{top[i].Key.name}' isKinematic={top[i].Key.isKinematic} colliders={top[i].Value}");
                sorted.Add((transforms.Length, line.ToString()));
            }
            sorted.Sort((a, b) => b.transforms.CompareTo(a.transforms));
            for (int i = 0; i < sorted.Count && i < 10; i++) report.AppendLine(sorted[i].line);
            watch.Stop();
            report.Append($"진단 집계 {watch.Elapsed.TotalMilliseconds:F1}ms");
            Debug.Log(report.ToString());
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
