using System.Collections;

using Unity.Profiling;

using UnityEngine;
using UnityEngine.SceneManagement;

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
        // 런타임 충돌 콜라이더가 많으면 프레임을 나누는 구간 크기.
        private const int ColliderReleaseSegment = 2000;
        private static NightRaidReturnOverlay instance;
        private RaidTransitionView m_view;
        private bool started;

        public static void BeginReturn(string sceneName)
        {
            if (instance == null)
            {
                GameObject root = RaidTransitionView.InstantiateCover("NightRaidReturnOverlay");
                if (root == null)
                {
                    Debug.LogError(
                        "[NightRaidReturnOverlay] 전환 가림막 프리팹이 등록되지 않았습니다. " +
                        "NightRaidUI의 m_transitionCoverPrefab 연결과 Tools/UI/Authoring/Raid/Transition을 확인하세요.");
                    root = new GameObject("NightRaidReturnOverlay");
                }

                instance = root.GetComponent<NightRaidReturnOverlay>();
                if (instance == null)
                {
                    instance = root.AddComponent<NightRaidReturnOverlay>();
                }

                DontDestroyOnLoad(root);
            }

            if (instance.started)
            {
                return;
            }

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
            m_view = GetComponent<RaidTransitionView>();
            if (m_view == null)
            {
                m_view = GetComponentInChildren<RaidTransitionView>(true);
            }

            if (m_view == null)
            {
                Debug.LogError(
                    "[NightRaidReturnOverlay] RaidTransitionView가 없습니다. " +
                    "전환 프리팹 저작 여부를 확인하세요.", this);
                return;
            }

            m_view.Initialize();
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

            // 정리가 여러 프레임에 걸치므로 그 사이 새 씬이 활성화되지 않게 먼저 막는다.
            operation.allowSceneActivation = false;
            // 로드 실패로 습격 씬에 머물 때 콜라이더가 사라지면 안 되므로, 로드가 확정된 뒤
            // 활성화 대기 전에 언로드 대상 씬의 런타임 충돌 콜라이더를 미리 지운다.
            yield return ReleaseRuntimeCollidersBeforeUnload();

            // Markers must begin and end in the same frame, so only the per-frame check is sampled.
            while (true)
            {
                bool loaded;
                using (LoadWaitMarker.Auto())
                {
                    loaded = operation.progress >= .9f;
                }

                if (loaded)
                {
                    break;
                }

                yield return null;
            }

            // Keep the existing save/restore and transfer-consumption order; collect before activation.
            using (CollectMarker.Auto())
            {
                System.GC.Collect();
            }

            operation.allowSceneActivation = true;
            while (!operation.isDone)
            {
                yield return null;
            }

            yield return ReleaseAfterWorkshopTransition();
            Finish();
        }

        // 언로드 전에 런타임 충돌 콜라이더를 선제 파괴해 UnloadScene의 Destroy 비용을 덜어낸다.
        // 가림막 아래라 한 프레임에 몰아도 되지만, 개수가 크면 구간마다 프레임을 나누고
        // 구간별 ms를 같은 로그 줄에 나열해 Destroy 시간의 제곱 증가 여부를 판단한다.
        private IEnumerator ReleaseRuntimeCollidersBeforeUnload()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var segmentWatch = System.Diagnostics.Stopwatch.StartNew();
            var segments = new System.Text.StringBuilder();
            int released = 0, nextSplit = ColliderReleaseSegment;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var targets = root.GetComponentsInChildren<DestructibleTarget>(true);
                for (int i = 0; i < targets.Length; i++)
                {
                    released += targets[i].ReleaseRuntimeCollisionCollidersForUnload();
                    if (released < nextSplit)
                    {
                        continue;
                    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    segments.Append(segmentWatch.Elapsed.TotalMilliseconds.ToString("F1")).Append('/');
#endif
                    nextSplit += ColliderReleaseSegment;
                    yield return null;
                    segmentWatch.Restart();
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            watch.Stop();
            // 임시 진단: 복귀 멈춤 원인 확인 후 제거
            if (segments.Length > 0)
            {
                segments.Append(segmentWatch.Elapsed.TotalMilliseconds.ToString("F1"));
            }

            Debug.Log(segments.Length > 0
                ? $"[복귀 진단] 런타임 콜라이더 정리 {released}개 {watch.Elapsed.TotalMilliseconds:F1}ms (구간 ms {segments})"
                : $"[복귀 진단] 런타임 콜라이더 정리 {released}개 {watch.Elapsed.TotalMilliseconds:F1}ms");
#endif
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
                if (transition != null && transition.IsTransitioning)
                {
                    yield break;
                }

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
                    if (collider.enabled && collider.gameObject.activeInHierarchy)
                    {
                        activeColliders++;
                    }

                    var body = collider.attachedRigidbody;
                    if (body == null)
                    {
                        continue;
                    }

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
                {
                    line.Append($"\n    rb '{top[i].Key.name}' isKinematic={top[i].Key.isKinematic} colliders={top[i].Value}");
                }

                sorted.Add((transforms.Length, line.ToString()));
            }

            sorted.Sort((a, b) => b.transforms.CompareTo(a.transforms));
            for (int i = 0; i < sorted.Count && i < 10; i++)
            {
                report.AppendLine(sorted[i].line);
            }

            watch.Stop();
            report.Append($"진단 집계 {watch.Elapsed.TotalMilliseconds:F1}ms");
            Debug.Log(report.ToString());
        }

        private IEnumerator Fade(float from, float to)
        {
            if (m_view != null)
            {
                m_view.SetVisible(true);
                m_view.SetAlpha(from);
            }

            float elapsed = 0f;
            while (elapsed < FadeDuration)
            {
                using (FadeMarker.Auto())
                {
                    elapsed += Time.unscaledDeltaTime;
                    if (m_view != null)
                    {
                        m_view.SetAlpha(Mathf.Lerp(from, to,
                            Mathf.SmoothStep(0f, 1f, elapsed / FadeDuration)));
                    }
                }

                yield return null;
            }

            if (m_view != null)
            {
                m_view.SetAlpha(to);
            }
        }

        private void Finish()
        {
            if (m_view != null)
            {
                m_view.SetVisible(false);
            }

            if (instance == this)
            {
                instance = null;
            }

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
