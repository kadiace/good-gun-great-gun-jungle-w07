using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class PlayerModuleUnlocks : MonoBehaviour
    {
        [SerializeField] private ModuleCatalog catalog;
        private readonly HashSet<int> unlocked = new HashSet<int>();
        public event Action Changed;
        public event Action<int> ModuleUnlocked;
        public string GetModuleName(int moduleId) => catalog != null && catalog.TryGet(moduleId, out var entry)
            ? entry.displayName : moduleId.ToString();

        private void Awake() => unlocked.Clear();

        public bool IsUnlocked(int moduleId) => catalog != null && catalog.TryGet(moduleId, out var entry)
            && (!entry.initiallyLocked || unlocked.Contains(moduleId));

        public bool IsUnlocked(ModuleData module)
        {
            if (module == null || catalog == null) return false;
            for (int id = 1; id <= catalog.Count; id++)
                if (catalog.TryGet(id, out var entry) && entry.data == module) return IsUnlocked(id);
            return false;
        }

        public bool IsUnlocked(ModuleKind kind)
        {
            if (catalog == null) return false;
            for (int id = 1; id <= catalog.Count; id++)
                if (catalog.TryGet(id, out var entry) && IsUnlocked(id) && entry.prefab != null &&
                    ModuleCounter.TryGetKind(entry.prefab.GetComponent<PlacedBlock>(), out var candidate) && candidate == kind)
                    return true;
            return false;
        }

        // 해금 여부와 무관하게 해당 종류의 모듈 데이터(가격 조회용).
        public ModuleData FindModule(ModuleKind kind)
        {
            if (catalog == null) return null;
            for (int id = 1; id <= catalog.Count; id++)
                if (catalog.TryGet(id, out var entry) && entry.prefab != null &&
                    ModuleCounter.TryGetKind(entry.prefab.GetComponent<PlacedBlock>(), out var candidate) && candidate == kind)
                    return entry.data;
            return null;
        }

        public bool HasUnlockedStatModule()
        {
            if (catalog == null) return false;
            for (int id = 1; id <= catalog.Count; id++)
                if (catalog.TryGet(id, out var entry) && IsUnlocked(id) && entry.prefab != null &&
                    ModuleCounter.TryGetKind(entry.prefab.GetComponent<PlacedBlock>(), out var kind) && ModuleCounter.IsStatModule(kind))
                    return true;
            return false;
        }

        // Future module-crate rewards call this on the collecting player.
        public bool UnlockModule(int moduleId)
        {
            if (catalog == null || !catalog.TryGet(moduleId, out _) || IsUnlocked(moduleId) || !unlocked.Add(moduleId)) return false;
            Changed?.Invoke();
            ModuleUnlocked?.Invoke(moduleId);
            return true;
        }

        public int UnlockAllLockedModules()
        {
            if (catalog == null) return 0;

            int unlockedCount = 0;
            for (int moduleId = 1; moduleId <= catalog.Count; moduleId++)
            {
                if (!catalog.TryGet(moduleId, out ModuleCatalog.Entry entry) || !entry.initiallyLocked) continue;
                if (unlocked.Add(moduleId)) unlockedCount++;
            }

            if (unlockedCount > 0) Changed?.Invoke();
            return unlockedCount;
        }

        public int[] CaptureUnlockedModules()
        {
            var result = new int[unlocked.Count];
            unlocked.CopyTo(result);
            Array.Sort(result);
            return result;
        }

        public void RestoreUnlockedModules(int[] moduleIds)
        {
            unlocked.Clear();
            if (moduleIds != null && catalog != null)
                foreach (int id in moduleIds)
                    if (catalog.TryGet(id, out _)) unlocked.Add(id);
            Changed?.Invoke();
        }
    }
}
