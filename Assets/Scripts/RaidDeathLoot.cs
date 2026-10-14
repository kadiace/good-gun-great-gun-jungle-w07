using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WeaponWorkshop
{
    [Serializable]
    public sealed class RaidDeathDropSave
    {
        public string id, sceneName;
        public Vector3 position;
        public WeaponWorldSave weapon;
        public int[] modules = Array.Empty<int>();
        public int credits;
        public bool bundleCollected, creditsCollected;
    }

    public static class RaidDeathLoot
    {
        private static readonly List<RaidDeathDropSave> drops = new List<RaidDeathDropSave>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() => drops.Clear();
        public static void Restore(RaidDeathDropSave[] saved)
        {
            drops.Clear();
            if (saved != null) drops.AddRange(saved.Where(d => d != null));
        }
        public static RaidDeathDropSave[] Capture() => drops.Where(d => !d.bundleCollected || !d.creditsCollected).ToArray();
        public static void Add(RaidDeathDropSave drop) => drops.Add(drop);
        public static IEnumerable<RaidDeathDropSave> ForScene(string scene) => Capture().Where(d => d.sceneName == scene);
    }
}
