using System;

namespace WeaponWorkshop
{
    [Serializable] public sealed class SceneBootLog { public string scene; }
    [Serializable] public sealed class WeaponSaleLog { public int price; public bool satisfied; public int tagCount; }
    [Serializable] public sealed class RaidResultLog
    {
        public string scene;
        public bool survived;
        public float time;
        public float duration;
        public int[] moduleCrateRewards;
        public int moduleCrateCredits;
        public int materialCrateCredits;
        public int enemyKills;
        public float health;
    }
    [Serializable] public sealed class RaidBlockDestroyedLog { public string stageId; public float time; public string blockId; }
    [Serializable] public sealed class RaidEnemyLog { public float time; public string enemyId; }
    [Serializable] public sealed class RaidPlayerStateLog
    {
        public float time;
        public UnityEngine.Vector3 position;
        public UnityEngine.Vector3 cameraRotation;
        public float health;
        public int remainingModuleCrates;
        public int remainingMaterialCrates;
        public int tanks;
        public int helicopters;
    }
}
