using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace WeaponWorkshop
{
    // VisualDebrisCloud 갱신을 단일 LateUpdate 순회로 모은다. 클라우드 개수만큼 LateUpdate가 따로 돌지 않는다.
    // 등록은 OnEnable, 해제는 OnDisable 에서 하며 매니저는 씬 배치 없이 첫 등록 때 만든다.
    [DefaultExecutionOrder(10000)]
    internal sealed class VisualDebrisCloudManager : MonoBehaviour
    {
        // 기존 VisualDebrisCloud.LateUpdate 샘플을 한 자리에서 보인다.
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("VisualDebrisCloud.Tick");
        private static VisualDebrisCloudManager instance;
        private static readonly List<VisualDebrisCloud> clouds = new List<VisualDebrisCloud>(16);

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
                int frame = Time.frameCount;
                for (int i = clouds.Count - 1; i >= 0; i--)
                {
                    // 순회 중 등록·해제로 목록이 밀렸으면 남은 마지막 미방문 자리부터 이어 읽는다.
                    if (i >= clouds.Count) { i = clouds.Count; continue; }
                    VisualDebrisCloud cloud = clouds[i];
                    if (cloud == null) { RemoveAtSwap(i); i++; continue; }
                    if (cloud.tickFrame == frame) continue;
                    cloud.tickFrame = frame;
                    cloud.Tick(view, dt, gravity);
                }
            }
        }
    }
}
