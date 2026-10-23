using UnityEngine;

namespace WeaponWorkshop
{
    // 씬을 떠나기 직전(복귀 언로드 전)에만 쓰는 정리 partial이다. 일반 파괴·복원 경로에서는 부르지 않는다.
    public sealed partial class DestructibleTarget
    {
        /// <summary>
        /// 씬 언로드 전 전용: 풀에서 런타임에 AddComponent로 만든 충돌 콜라이더만 즉시 파괴해
        /// UnloadScene의 Destroy 비용을 덜어낸다. 씬에 원래 배치된 원본 콜라이더는 남긴다.
        /// 지운 개수를 돌려준다.
        /// </summary>
        internal int ReleaseRuntimeCollisionCollidersForUnload()
        {
            int released = 0;
            for (int i = collisionColliderPool.Count - 1; i >= 0; i--)
            {
                BoxCollider collider = collisionColliderPool[i];
                // 이미 사라진 항목과 원본 콜라이더(오브젝트와 함께 언로드됨)는 건드리지 않는다.
                if (collider == null || originalColliders.ContainsKey(collider)) continue;
                // ColliderTargetIndex에 해제 API는 없다. 파괴된 항목은 기존 Sweep이 걷는다.
                Object.DestroyImmediate(collider);
                released++;
            }
            collisionColliderPool.Clear();
            collisionColliderCursor = 0;
            // 풀을 비웠으니 다음 사용 때 원본 콜라이더를 다시 편입시킨다(풀 오브젝트가 씬을 넘어 생존하는 경우 대비).
            collisionPoolMergedOriginals = false;
            // 충돌 열이 파괴된 콜라이더를 참조하지 않게 열을 걷고, 대기 중이던 재구축도 막는다.
            if (collisionColumns != null)
                foreach (var runs in collisionColumns)
                    if (runs != null)
                    {
                        runs.Clear();
                        if (collisionColumnListPool.Count < 512) collisionColumnListPool.Push(runs);
                    }
            collisionColumns = null;
            dirtyColumnFlags = null;
            dirtyColumns.Clear();
            collisionDirty = false;
            allColumnsDirty = true;
            collisionColumnCursor = 0;
            return released;
        }
    }
}
