using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace WeaponWorkshop
{
    [Serializable] public sealed class MapObjectSave
    {
        public string id, parentId;
        public bool exists, active, root;
        public Vector3 position, scale;
        public Quaternion rotation;
    }
    [Serializable] public sealed class PaintFaceSave
    {
        public Vector3Int cell, face;
        public string materialKey;
    }
    [Serializable] public sealed class WeaponWorldSave
    {
        public string weaponName;
        public string testAssemblyKey;
        public string[] passedTestIds = Array.Empty<string>();
        public string parentId, rackId, benchId, returnBenchId;
        public bool held, active, dynamicBody, gravity, constructionLocked;
        public PaintFaceSave[] paint = Array.Empty<PaintFaceSave>();
        public Vector3 position, scale, velocity, angularVelocity;
        public Quaternion rotation;
        public int paidCost;
        public bool hasSelectedScopeCell;
        public Vector3Int selectedScopeCell;
        public SaveBlock[] blocks;
    }
    [Serializable] public sealed class CellExplosionSave
    {
        public Vector3Int coordinate;
        public double health;
    }
    [Serializable] public sealed class TargetWorldSave
    {
        public string id;
        public bool spawnedByBell, exists, subdivided;
        public int prefabSlot = -1;
        // 자기 자신과 같은 타입을 담으면 직렬화 계층이 무한히 늘어 JsonUtility의 깊이 제한(10)에서
        // 파싱이 중단되고, 그 뒤의 필드(timer·savedUtcTicks·blocks)가 비어 저장을 읽지 못한다.
        // 그래서 bells이 낳은 표적은 한 층에서 끊은 얕은 기록으로 담는다.
        public TargetSpawnSave[] spawnedTargets;
        public Vector3 meshSize, velocity, angularVelocity;
        public float penetration, explosion, heat, water;
        public Vector3 position, scale;
        public Quaternion rotation;
        public Vector3Int[] remaining;
        public CellExplosionSave[] explosionDamage;
    }
    /// <summary>표적 종이 새로 만들어 낸 표적 한 층. 재귀 필드가 없어 직렬화 깊이를 고정한다.</summary>
    [Serializable] public sealed class TargetSpawnSave
    {
        public string id;
        public bool spawnedByBell, exists, subdivided;
        public int prefabSlot = -1;
        public Vector3 meshSize, velocity, angularVelocity;
        public float penetration, explosion, heat, water;
        public Vector3 position, scale;
        public Quaternion rotation;
        public Vector3Int[] remaining;
        public CellExplosionSave[] explosionDamage;

        public static TargetSpawnSave From(TargetWorldSave saved) => saved == null ? null : new TargetSpawnSave
        {
            id = saved.id,
            spawnedByBell = saved.spawnedByBell,
            exists = saved.exists,
            subdivided = saved.subdivided,
            prefabSlot = saved.prefabSlot,
            meshSize = saved.meshSize,
            velocity = saved.velocity,
            angularVelocity = saved.angularVelocity,
            penetration = saved.penetration,
            explosion = saved.explosion,
            heat = saved.heat,
            water = saved.water,
            position = saved.position,
            scale = saved.scale,
            rotation = saved.rotation,
            remaining = saved.remaining,
            explosionDamage = saved.explosionDamage,
        };

        // 표적 복원 쪽은 TargetWorldSave를 받는다. 하위 계층은 애초에 없으므로 빈 참조로 되돌린다.
        public TargetWorldSave ToSave() => new TargetWorldSave
        {
            id = id,
            spawnedByBell = spawnedByBell,
            exists = exists,
            subdivided = subdivided,
            prefabSlot = prefabSlot,
            meshSize = meshSize,
            velocity = velocity,
            angularVelocity = angularVelocity,
            penetration = penetration,
            explosion = explosion,
            heat = heat,
            water = water,
            position = position,
            scale = scale,
            rotation = rotation,
            remaining = remaining,
            explosionDamage = explosionDamage,
        };
    }

    [Serializable] public sealed class CustomerSave
    {
        public int stage, line, price;
        public bool talked, satisfied;
        public Vector3 position;
    }
    [Serializable] public sealed class TimerSave
    {
        public float angle, target, speed;
        public bool rotating, unscaled, normalize;
    }
    [Serializable] public sealed class SaveBlock
    {
        public bool hasColor;
        public Color color;
        public int slot, paidCost, paidModuleCreditCost;
        public Vector3Int cell, output, normal;
    }
    [Serializable] public sealed class CustomerOrderSave
    {
        public CustomerOrderState orderState;
        public string profileId;
        public string[] requestIds;
        // New saves use stable profile/request IDs. Numeric slots only read pre-migration saves.
        public int sourceDay, sourceSlot = -1;
        public int[] requests = Array.Empty<int>();
        public string[] lines;
        public string happyLine, angryLine;
        public int portrait = -1, price;
    }

    [Serializable] public sealed class GameSaveData
    {
        public string[] weaponPresets;
        public string weaponName;
        // 로그용 진행 식별자(PlayerIdentity). 새 게임마다 새로 만들고 불러오면 이어 쓴다.
        public string saveId;
        [NonSerialized] public CustomerDay travelCustomerDay;
        public string testAssemblyKey;
        public string[] passedTestIds = Array.Empty<string>();
        public int version = 8, day, balance, paidCost;
        public int slotMachineSpins;
        public int moduleCredits;
        // 필드가 없는 이전 JSON은 0으로 읽힌다. 슬롯별 최고 클리어 습격 번호다.
        public int raidClearProgress;
        // 빈 배열은 신규 상태이며 DayFirst는 암묵적으로 해금된다. null은 구형 필드 부재다.
        public string[] unlockedMainTrackIds;
        public bool trackUnlocksMigrated;
        public string[] clearedRaidScenes = Array.Empty<string>();
        public int outstandingLoans, todayLoanRepayment, todayLoanInterest;
        public int todayLoanIncome;
        public int lastLoanDay = -1;
        public int[] unlockedModules = Array.Empty<int>();
        public CustomerOrderSave[] dailyOrders;
        public CustomerOrderSave[] deferredCustomers;
        public int dailyRent;
        public string sceneName;
        public bool savedMap, savedWeapons, savedTargets;
        public MapObjectSave[] map = Array.Empty<MapObjectSave>();
        public WeaponWorldSave[] weapons = Array.Empty<WeaponWorldSave>();
        public TargetWorldSave[] targets = Array.Empty<TargetWorldSave>();
        public int completed, dissatisfied, killed, income, period, nextCustomer, currentCustomer = -1;
        public int constructionCost;
        public int soldConstructionCost;
        public bool night, settlement, confirmation, inWorkshop, retired;
        public bool startedDayInDebt;
        public int workbench = -1;
        public Vector3 playerPosition, playerEuler, viewEuler, benchEuler;
        public TimerSave timer;
        public CustomerSave customer;
        public long savedUtcTicks;
        public bool hasWeapon;
        public SaveBlock[] blocks = Array.Empty<SaveBlock>();
    }

    public static class GameSaveFiles
    {
        public const int SlotCount = 3;
        public const int AutoSaveSlotIndex = SlotCount;
        public static GameSaveData PendingLoad { get; set; }
        private static string AutoSavePath => Path.Combine(Application.persistentDataPath, "Saves", "autosave.json");
        private static string RaidRecoveryPath => Path.Combine(Application.persistentDataPath, "Saves", "raid-recovery.json");
        public static bool HasRaidRecovery => File.Exists(RaidRecoveryPath);
        public static void ClearRaidRecovery()
        {
            if (HasRaidRecovery) File.Delete(RaidRecoveryPath);
            string temp = RaidRecoveryPath + ".tmp";
            if (File.Exists(temp)) File.Delete(temp);
        }
        private static string PathFor(int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(Application.persistentDataPath, "Saves", $"slot{slot + 1}.json");
        }
        public static bool Exists(int slot) => File.Exists(PathFor(slot));
        public static bool AutoSaveExists => File.Exists(AutoSavePath);
        public static bool TryReadSlot(int slot, out GameSaveData data)
        {
            if (slot == AutoSaveSlotIndex) return TryReadAutoSave(out data);
            if (slot < 0 || slot >= SlotCount) { data = null; return false; }
            return TryRead(slot, out data);
        }
        public static bool TryRead(int slot, out GameSaveData data)
            => TryReadPath(PathFor(slot), out data);
        public static bool TryReadAutoSave(out GameSaveData data)
            => TryReadPath(AutoSavePath, out data);

        /// <summary>
        /// 저장 JSON을 읽고 지원하는 버전 및 필드 범위를 확인한다.
        /// path에서 읽은 GameSaveData와 성공 여부를 반환한다.
        /// </summary>
        private static bool TryReadPath(string path, out GameSaveData data)
        {
            data = null;
            try
            {
                if (!File.Exists(path)) return false;
                var read = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path));
                if (read == null || (read.version < 2 || read.version > 8) || read.day < 0 ||
                    read.paidCost < 0 || read.savedUtcTicks <= 0 || read.savedUtcTicks > DateTime.MaxValue.Ticks ||
                    read.completed < 0 || read.dissatisfied < 0 || read.killed < 0 || read.income < 0 ||
                    read.dissatisfied + read.killed > read.completed || read.nextCustomer < 0 ||
                    (read.trackUnlocksMigrated && read.unlockedMainTrackIds == null) ||
                    read.timer == null || read.period < 0 || read.period > 3 ||
                    (read.currentCustomer >= 0 && (read.customer == null || read.customer.stage < 0 || read.customer.stage > 4 || read.customer.line < 0 || read.customer.price < 0)) ||
                    read.blocks == null || read.blocks.Length > 10000 ||
                    read.blocks.Any(b => b == null || b.slot < 1 || b.paidCost < 0 || b.cell == Vector3Int.zero) ||
                    read.blocks.Select(b => b.cell).Distinct().Count() != read.blocks.Length) return false;
                if (read.version >= 3 && (string.IsNullOrEmpty(read.sceneName) || read.map == null || read.weapons == null || read.targets == null ||
                    read.weapons.Any(w => w == null || w.blocks == null || w.blocks.Any(b => b == null || b.slot < 1 || b.paidCost < 0)))) return false;
                if (read.version < 4)
                {
                    int[] ids = { 0, 1, 2, 3, 4, 8, 5, 9, 11, 10, 16, 13, 14, 12, 15, 7, 17, 6, 19, 18 };
                    foreach (var block in read.blocks) if (block.slot < ids.Length) block.slot = ids[block.slot];
                    if (read.weapons != null)
                        foreach (var weapon in read.weapons)
                            foreach (var block in weapon.blocks) if (block.slot < ids.Length) block.slot = ids[block.slot];
                    if (read.unlockedModules != null)
                        read.unlockedModules = read.unlockedModules.Select(id => id > 0 && id < ids.Length ? ids[id] : id).ToArray();
                    if (read.version == 3) read.version = 4;
                }
                data = read;
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { return false; }
        }
        public static void Delete(int slot)
        {
            string path = PathFor(slot);
            string temp = path + ".tmp";
            if (File.Exists(temp)) File.Delete(temp);
            if (File.Exists(path)) File.Delete(path);
            if (LatestSlot() < 0) ClearRaidRecovery();
        }
        public static void DeleteAutoSave()
        {
            if (AutoSaveExists) File.Delete(AutoSavePath);
            string temp = AutoSavePath + ".tmp";
            if (File.Exists(temp)) File.Delete(temp);
            ClearRaidRecovery();
        }
        public static void DeleteAllSaves()
        {
            for (int slot = 0; slot < SlotCount; slot++) Delete(slot);
            DeleteAutoSave();
            PendingLoad = null;
            NightRaidTransfer.Abandon();
        }
        public static int LatestSlot()
        {
            int latest = -1; long ticks = 0;
            for (int i = 0; i < SlotCount; i++)
                if (TryRead(i, out var data) && data.savedUtcTicks > ticks) { ticks = data.savedUtcTicks; latest = i; }
            if (TryReadAutoSave(out var autoSave) && autoSave.savedUtcTicks > ticks)
                latest = AutoSaveSlotIndex;
            return latest;
        }
        public static void Write(int slot, GameSaveData data, bool prettyPrint = true)
            => WritePath(PathFor(slot), data, prettyPrint);

        public static void WriteAutoSave(GameSaveData data, bool prettyPrint = true)
            => WritePath(AutoSavePath, data, prettyPrint);

        private static void WritePath(string path, GameSaveData data, bool prettyPrint)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, prettyPrint));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        public static string Description(int slot)
        {
            if (!TryRead(slot, out var data)) return Exists(slot) ? "읽을 수 없는 저장 파일" : "빈 슬롯";
            return Describe(data);
        }

        public static string AutoSaveDescription()
        {
            if (!TryReadAutoSave(out var data)) return AutoSaveExists ? "읽을 수 없는 저장 파일" : "자동 저장 기록 없음";
            return Describe(data);
        }

        private static string Describe(GameSaveData data)
            => $"{data.day}일차 · $ {data.balance:N0}\n{new DateTime(data.savedUtcTicks, DateTimeKind.Utc).ToLocalTime():yyyy-MM-dd HH:mm} · 무기 {(data.hasWeapon ? "있음" : "없음")}{(data.retired ? " · 은퇴 성공" : "")}";
    }
}
