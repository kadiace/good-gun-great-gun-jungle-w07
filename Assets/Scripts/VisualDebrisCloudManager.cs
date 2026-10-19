using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace WeaponWorkshop
{
    // VisualDebrisCloud 갱신을 단일 LateUpdate 순회로 모은다. 클라우드 개수만큼 LateUpdate가 따로 돌지 않는다.
    // 등록은 OnEnable, 해제는 OnDisable 에서 하며 매니저는 씬 배치 없이 첫 등록 때 만든다.
    // 폭발이 겹치는 프레임에 모든 구름이 매번 전체 계산을 하면 Tick이 프레임 상위 비용이 되므로,
    // 먼 구름은 프레임을 건너 계산하고 프레임당 계산 시간 예산을 넘기면 남은 구름을 다음 프레임으로 미룬다(공정 순환).
    // 양보한 프레임의 시간은 구름에 누적해 다음 계산에 한꺼번에 더하므로 파편 움직임 속도는 변하지 않는다.
    [DefaultExecutionOrder(10000)]
    internal sealed class VisualDebrisCloudManager : MonoBehaviour
    {
        // 기존 VisualDebrisCloud.LateUpdate 샘플을 한 자리에서 보인다.
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("VisualDebrisCloud.Tick");
        private static VisualDebrisCloudManager instance;
        private static readonly List<VisualDebrisCloud> clouds = new List<VisualDebrisCloud>(16);

        // 프레임당 파편 구름 계산 예산(ms). 폭발 창에서 구름이 많을 때 Tick 합이 프레임을 잠식하지 않게 한다.
        private const float TickBudgetMilliseconds = 4f;
        // 이 거리(m) 밖 구름은 프레임을 건너 계산한다. 구름 내부의 48m 밖 격프레임 갱신과 합쳐 4프레임 주기가 된다.
        private const float StaggeredUpdateDistanceSq = 96f * 96f;
        private int tickCursor;  // 다음 프레임에 계산 우선권을 받을 자리.
        private readonly System.Diagnostics.Stopwatch tickWatch = new System.Diagnostics.Stopwatch();

        // 도메인 리로드 꺼짐·씬 전환 뒤에도 이전 세션의 클라우드를 순회하지 않도록 정적 목록을 비운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            clouds.Clear();
        }

        internal static void Register(VisualDebrisCloud cloud)
        {
            if (!Application.isPlaying || cloud == null || cloud.managerIndex >= 0) return;
            GetOrCreate();
            cloud.managerIndex = clouds.Count;
            clouds.Add(cloud);
        }

        internal static void Unregister(VisualDebrisCloud cloud)
        {
            if (cloud == null) return;
            int index = cloud.managerIndex;
            cloud.managerIndex = -1;
            if (index < 0 || index >= clouds.Count) return;
            // 목록이 초기화돼 자리 정보가 어긋났으면 이 클라우드의 자리는 이미 없다.
            if (clouds[index] != cloud) return;
            RemoveAtSwap(index);
        }

        private static void RemoveAtSwap(int index)
        {
            int last = clouds.Count - 1;
            if (index != last)
            {
                VisualDebrisCloud moved = clouds[last];
                clouds[index] = moved;
                if (moved != null) moved.managerIndex = index;
            }
            clouds.RemoveAt(last);
        }

        private static VisualDebrisCloudManager GetOrCreate()
        {
            if (instance != null) return instance;
            GameObject managerObject = new GameObject("VisualDebrisCloudManager");
            DontDestroyOnLoad(managerObject);
            instance = managerObject.AddComponent<VisualDebrisCloudManager>();
            return instance;
        }

        private void OnDisable()
        {
            if (instance == this) instance = null;
        }

        private void LateUpdate()
        {
            if (clouds.Count == 0) return;
            using (TickMarker.Auto())
            {
                // 시선·시간·중력은 프레임당 한 번만 읽는다.
                Camera view = Camera.main;
                float dt = Time.deltaTime;
                Vector3 gravity = Physics.gravity;
                Vector3 viewPosition = view != null ? view.transform.position : Vector3.zero;
                int frame = Time.frameCount;
                float budgetLeft = TickBudgetMilliseconds;
                int firstDeferred = -1;  // 예산을 넘겨 처음 양보한 자리. 다음 프레임은 여기부터 다시 시작한다.
                int count = clouds.Count;
                if (tickCursor >= count) tickCursor = 0;
                for (int n = 0; n < count; n++)
                {
                    // 순회 중 등록·해제로 목록이 밀리면 자리를 현재 크기로 감싸 되돌린다.
                    // 두 번 읽힌 구름은 tickFrame 스탬프로 걸러진다.
                    if (clouds.Count == 0) break;
                    int i = (tickCursor + n) % clouds.Count;
                    VisualDebrisCloud cloud = clouds[i];
                    if (cloud == null) { RemoveAtSwap(i); continue; }
                    if (cloud.tickFrame == frame) continue;

                    // 먼 구름은 프레임을 건너 계산하고, 예산이 끝난 뒤의 구름은 다음 프레임으로 미룬다.
                    // 둘 다 직전 그리기 결과만 다시 제출해 화면에서 사라지지 않게 한다. 카메라가 없으면 원래대로 매번 계산한다.
                    bool deferred = false;
                    if (view != null)
                    {
                        bool far = (cloud.FocusPosition - viewPosition).sqrMagnitude > StaggeredUpdateDistanceSq;
                        if (far && ((frame + i) & 1) != 0) deferred = cloud.TrySubmitCache();
                        else if (budgetLeft <= 0f) deferred = cloud.TrySubmitCache();
                    }
                    if (deferred)
                    {
                        cloud.pendingDeltaTime += dt;
                        cloud.tickFrame = frame;
                        if (firstDeferred < 0) firstDeferred = i;
                        continue;
                    }

                    float combined = dt + cloud.pendingDeltaTime;
                    cloud.pendingDeltaTime = 0f;
                    cloud.tickFrame = frame;
                    tickWatch.Restart();
                    cloud.Tick(view, combined, gravity);
                    tickWatch.Stop();
                    budgetLeft -= (float)tickWatch.Elapsed.TotalMilliseconds;
                }
                tickCursor = firstDeferred >= 0 ? firstDeferred : tickCursor + 1;
            }
        }
    }
}
