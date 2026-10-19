using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // Shared collider -> owner lookup for combat casts. A collision collider is created inside the object it
    // belongs to (a target's CellRoot or its own mesh object) and never moves to another target, so a resolved
    // mapping stays valid for the collider's lifetime. Only results that found nothing are re-checked, and only
    // every NegativeValidFrames frames, so a component attached later is still picked up. Pooled combat objects
    // are reparented on every rent, so colliders under them are always resolved fresh.
    internal static class ColliderTargetIndex
    {
        // 구조체 항목: 파괴 구간에 콜라이더가 수천 개씩 늘어나도 항목 하나하나가 힙 할당을 만들지 않는다.
        private struct Entry
        {
            public DestructibleTarget structure;
            public EndChestCore core;
            public EndChestShield shield;
            public int structureFrame;
            public int endChestFrame;
            // 풀 오브젝트(PooledCombatObject) 아래 콜라이더는 대여 때마다 부모가 바뀌므로 캐시하지 않는다.
            public bool pooled;
        }

        // "소속 없음" 판정만 다시 확인한다. 만료는 콜라이더 단위이고 표 전체를 주기적으로 훑지 않는다.
        private const int NegativeValidFrames = 60;
        private const int MinEntriesBeforeSweep = 4096;
        // 등록으로 채운 소속은 콜라이더 수명 동안 변하지 않는다. 만료 검사가 항상 실패하게 만드는 값이며,
        // 표적이 사라지면(구조 null) 만료 절차 없이 곧바로 null을 낸다.
        private const int StructureNeverExpires = int.MaxValue;

        // UnityEngine.Object 기본 비교는 판정마다 네이티브 Equals를 부른다. 참조 비교로 해시 조회를 싸게 유지한다.
        private sealed class ColliderIdentityComparer : IEqualityComparer<Collider>
        {
            public static readonly ColliderIdentityComparer Instance = new ColliderIdentityComparer();
            public bool Equals(Collider left, Collider right) => ReferenceEquals(left, right);
            public int GetHashCode(Collider collider) => collider.GetInstanceID();
        }

        private static readonly Dictionary<Collider, Entry> entries =
            new Dictionary<Collider, Entry>(1024, ColliderIdentityComparer.Instance);
        private static readonly List<Collider> staleColliders = new List<Collider>();
        // 같은 콜라이더를 다시 묻는 호출(필터→표적 해결)은 해시 조회조차 건너뛴다.
        private static Collider memoCollider;
        private static Entry memoEntry;
        private static int sweepAt = MinEntriesBeforeSweep;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetIndex()
        {
            entries.Clear(); staleColliders.Clear();
            memoCollider = null; memoEntry = default;
            sweepAt = MinEntriesBeforeSweep;
        }

        internal static DestructibleTarget Structure(Collider collider)
        {
            if (collider == null) return null;
            if (!TryPeek(collider, out Entry entry)) entry = NewEntry(collider);
            // 등록·캐시된 소속이 제일 싼 경로라서 먼저 본다. 풀 오브젝트는 규칙상 매번 부모를 다시 탄다.
            if (entry.structure != null) return entry.structure;
            if (entry.pooled) return FindInParents<DestructibleTarget>(collider);
            if (Time.frameCount - entry.structureFrame < NegativeValidFrames) return null;
            entry.structure = FindInParents<DestructibleTarget>(collider);
            entry.structureFrame = Time.frameCount;
            PutEntry(collider, entry);
            return entry.structure;
        }

        // 표적이 자기 전투 콜라이더(미분할 원본·CellRoot 풀 콜라이더)를 만들 때 곧장 등록한다.
        // 등록된 콜라이더를 묻는 CollisionQuery는 부모 탐색도, 항목 생성도, 음수 만료도 없이 소속을 돌려받는다.
        // 파괴 구간에 콜라이더가 계속 늘어나도 조회 쪽에 탐색·할당 비용이 붙지 않는 게 이 등록의 요점이다.
        internal static void RegisterStructure(Collider collider, DestructibleTarget owner)
        {
            if (collider == null || owner == null) return;
            if (!TryPeek(collider, out Entry entry))
                entry = new Entry { structureFrame = -NegativeValidFrames, endChestFrame = -NegativeValidFrames };
            // 풀 소속으로 판정된 콜라이더는 대여 때마다 부모가 바뀌므로 고정 등록하지 않는다.
            if (entry.pooled) return;
            entry.structure = owner;
            entry.structureFrame = StructureNeverExpires;
            PutEntry(collider, entry);
        }

        // 코어·방어막은 기존 판정처럼 "해당 레이어 조상이 있을 때"만 최근 성분을 쓴다.
        internal static void ResolveEndChest(Collider collider, int coreLayer, int shieldLayer,
            out EndChestCore core, out EndChestShield shield)
        {
            core = null; shield = null;
            if (collider == null) return;
            if (!TryPeek(collider, out Entry entry)) entry = NewEntry(collider);
            if (!entry.pooled && (entry.core != null || entry.shield != null))
            {
                core = entry.core; shield = entry.shield;
                return;
            }
            if (!entry.pooled && Time.frameCount - entry.endChestFrame < NegativeValidFrames) return;
            EndChestCore coreComponent = null;
            EndChestShield shieldComponent = null;
            bool coreLayerFound = false, shieldLayerFound = false;
            for (Transform current = collider.transform; current != null; current = current.parent)
            {
                if (coreComponent == null) current.TryGetComponent(out coreComponent);
                if (shieldComponent == null) current.TryGetComponent(out shieldComponent);
                int layer = current.gameObject.layer;
                if (layer == coreLayer) coreLayerFound = true;
                if (layer == shieldLayer) shieldLayerFound = true;
            }
            entry.core = coreLayerFound ? coreComponent : null;
            entry.shield = shieldLayerFound ? shieldComponent : null;
            entry.endChestFrame = Time.frameCount;
            PutEntry(collider, entry);
            core = entry.core; shield = entry.shield;
        }

        // 새 항목을 만들어 저장한다. 이 시점에만 풀 소속 판정을 위해 루트까지 부모를 탄다.
        private static Entry NewEntry(Collider collider)
        {
            var entry = new Entry
            {
                pooled = FindInParents<PooledCombatObject>(collider) != null,
                structureFrame = -NegativeValidFrames,
                endChestFrame = -NegativeValidFrames
            };
            PutEntry(collider, entry);
            return entry;
        }

        // 기존 항목을 꺼내기만 한다. 새 항목은 소비자 규칙(NewEntry·RegisterStructure)대로 만든다.
        private static bool TryPeek(Collider collider, out Entry entry)
        {
            if (ReferenceEquals(collider, memoCollider)) { entry = memoEntry; return true; }
            if (!entries.TryGetValue(collider, out entry)) return false;
            memoCollider = collider;
            memoEntry = entry;
            return true;
        }

        // 항목을 쓰고, 메모가 같은 콜라이더면 함께 갱신해 낡은 복사본을 남기지 않는다.
        private static void PutEntry(Collider collider, Entry entry)
        {
            entries[collider] = entry;
            if (entries.Count >= sweepAt) Sweep();
            if (ReferenceEquals(collider, memoCollider)) memoEntry = entry;
        }

        // 사라진 콜라이더의 항목만 지운다. 정리 기준을 넘기면 그 시점 항목 수의 두 배만큼 기다린다.
        private static void Sweep()
        {
            staleColliders.Clear();
            foreach (var pair in entries) if (pair.Key == null) staleColliders.Add(pair.Key);
            for (int i = 0; i < staleColliders.Count; i++) entries.Remove(staleColliders[i]);
            staleColliders.Clear();
            sweepAt = Mathf.Max(MinEntriesBeforeSweep, entries.Count * 2);
        }

        private static T FindInParents<T>(Collider collider) where T : Component
        {
            for (Transform current = collider.transform; current != null; current = current.parent)
                if (current.TryGetComponent(out T component)) return component;
            return null;
        }
    }
}
