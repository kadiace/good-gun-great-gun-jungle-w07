using System;
using UnityEngine;

namespace WeaponWorkshop
{
    // Serialization only. TableManager owns the instance; CustomerRequestInfo owns queries and rules.
    [Serializable]
    public sealed class CustomerRequestData
    {
        [Serializable] public sealed class Condition
        {
            public string id, label;
            public GunMeasure measure;
            public StatType stat;
            public float min, max = float.MaxValue;
            public bool maxExclusive;
        }
        [Serializable] public sealed class Request
        {
            public string id, name, phrase, tagId;
            public int unlockDay = 1;
            public bool saleBonus = true;
            public string[] excludes = Array.Empty<string>();
            public string[] requiredTestIds = Array.Empty<string>();
            public ModuleKind[] requiredModules = Array.Empty<ModuleKind>();
            public Condition[] conditions = Array.Empty<Condition>();
        }
        [Serializable] public sealed class Tag
        {
            public string id, name;
            public Color color;
            public float sellMultiplier = 1f;
            public Condition[] conditions = Array.Empty<Condition>();
        }
        [Serializable] public sealed class LegacySlot
        {
            public string poolId, requestId;
            public int index;
        }
        public Request[] requests = Array.Empty<Request>();
        public Tag[] tags = Array.Empty<Tag>();
        public LegacySlot[] legacySlots = Array.Empty<LegacySlot>();
    }
}
