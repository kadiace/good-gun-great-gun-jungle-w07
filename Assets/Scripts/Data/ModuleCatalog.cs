using System;
using UnityEngine;

namespace WeaponWorkshop
{
    // 공방에서 고를 수 있는 전체 모듈 목록. 모듈 ID = 목록 순서 + 1.
    // 1 기본 블럭, 2 연장, 3 두 방향 분할, 4 네 방향 분할, 5번부터 스탯 모듈.
    [CreateAssetMenu(fileName = "ModuleCatalog", menuName = "Weapon Workshop/Module Catalog")]
    public sealed class ModuleCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public ModuleData data;

            public string displayName => data != null ? data.DisplayName : string.Empty;
            public Color color => data != null ? data.Color : Color.white;
            public GameObject prefab => data != null ? data.Prefab : null;
            public bool useModuleCredits => data != null && data.UseModuleCredits;
            public bool initiallyLocked => data != null && data.InitiallyLocked;
            public int moduleCreditCost => data != null ? data.ModuleCreditCost : 0;
        }

        [SerializeField] private Entry[] entries = new Entry[0];

        public int Count => entries != null ? entries.Length : 0;

        public bool TryGet(int moduleId, out Entry entry)
        {
            bool valid = moduleId >= 1 && moduleId <= Count;
            entry = valid ? entries[moduleId - 1] : default;
            return valid && entry.data != null;
        }

        public GameObject GetPrefab(int moduleId) => TryGet(moduleId, out Entry entry) ? entry.prefab : null;
    }
}
