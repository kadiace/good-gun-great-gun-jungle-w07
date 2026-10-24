using System.Collections;
using System.Diagnostics;
using UnityEngine;

namespace WeaponWorkshop
{
    // RaidEnemyCore가 파괴된 적 루트에 런타임으로 붙인다. 코어 오브젝트는 사망 즉시 꺼지므로 대기·삭제를 루트가 직접 돈다.
    // 루트를 한 프레임에 지우면 습격 중 수백 ms가 걸리므로, 대기 후 렌더러를 꺼 화면에서 바로 치우고
    // 파츠를 프레임당 SliceMilliseconds씩 나눠 지운 뒤 루트를 마지막에 지운다.
    [DisallowMultipleComponent]
    internal sealed class EnemyWreckRemoval : MonoBehaviour
    {
        private const double SliceMilliseconds = 1.0;

        internal void Begin(float delay) => StartCoroutine(Remove(delay));

        private IEnumerator Remove(float delay)
        {
            yield return new WaitForSeconds(delay);
            // 리지드바디 Destroy 비용은 질량·관성 텐서 자동 재계산이 지배하므로 삭제 직전에만 끈다.
            RaidEnemyCore.PrepareRootForDestroy(gameObject);
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            var parts = GetComponentsInChildren<DestructibleTarget>(true);
            long sliceTicks = (long)(Stopwatch.Frequency * SliceMilliseconds / 1000d);
            long started = Stopwatch.GetTimestamp();
            // 자식 파츠부터 지워 부모 파츠 삭제가 하위를 한꺼번에 끌고 가지 않게 한다.
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                var part = parts[i];
                if (part == null || part.gameObject == gameObject) continue;
                DestroyImmediate(part.gameObject);
                if (Stopwatch.GetTimestamp() - started < sliceTicks) continue;
                yield return null;
                started = Stopwatch.GetTimestamp();
            }
            Destroy(gameObject);
        }
    }
}
