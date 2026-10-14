using System;
using System.Linq;
using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent, RequireComponent(typeof(RaidPlayerHealth))]
    public sealed class PlayerRaidDeathLoot : MonoBehaviour
    {
        [SerializeField] private RaidDeathBundlePickup bundlePrefab;
        [SerializeField] private ModuleCreditPickup creditsPrefab;
        [SerializeField] private EquipableWeapon weaponPrefab;
        private FirstPersonPlayer player;
        private RaidPlayerHealth health;
        private int startingCredits;
        private int[] startingModules;
        private bool initialized, dropped;
        private PaintMaterialLibrary paints;

        private void Awake()
        {
            player = GetComponent<FirstPersonPlayer>();
            health = GetComponent<RaidPlayerHealth>();
            initialized = dropped = false;
        }
        private void OnEnable() => health.Died += DropOnDeath;
        private void OnDisable() => health.Died -= DropOnDeath;
        private void OnDestroy() => paints?.Dispose();

        public void BeginRaid()
        {
            startingCredits = player.GetComponent<PlayerWallet>().ModuleCredits;
            startingModules = player.GetComponent<PlayerModuleUnlocks>()?.CaptureUnlockedModules() ?? Array.Empty<int>();
            initialized = true;
            foreach (var drop in RaidDeathLoot.ForScene(gameObject.scene.name)) Spawn(drop);
        }

        private void DropOnDeath()
        {
            if (!initialized || dropped) return;
            var holder = player.GetComponent<WeaponHolder>();
            var weapon = holder.EquippedWeapon;
            WeaponWorldSave savedWeapon = null;
            if (weapon != null && !GameSaveController.TryCaptureCarried(weapon, out savedWeapon, out var error, true))
            { Debug.LogError(error, this); return; }
            var modules = player.GetComponent<PlayerModuleUnlocks>()?.CaptureUnlockedModules() ?? Array.Empty<int>();
            int credits = Mathf.Max(0, player.GetComponent<PlayerWallet>().ModuleCredits - startingCredits);
            var drop = new RaidDeathDropSave
            {
                id = Guid.NewGuid().ToString("N"), sceneName = gameObject.scene.name,
                position = player.transform.position, weapon = savedWeapon,
                modules = modules.Except(startingModules).ToArray(), credits = credits,
                bundleCollected = savedWeapon == null && !modules.Except(startingModules).Any(),
                creditsCollected = credits == 0
            };
            RaidDeathLoot.Add(drop);
            dropped = true;
            Spawn(drop);
            if (weapon != null) { holder.TakeWeapon(); Destroy(weapon.gameObject); }
            NightRaidController.Instance?.PersistDeathRecovery();
        }

        private void Spawn(RaidDeathDropSave drop)
        {
            if (!drop.bundleCollected && bundlePrefab != null)
                Instantiate(bundlePrefab, drop.position + Vector3.up * 0.35f, Quaternion.identity)
                    .Initialize(drop, this);
            if (!drop.creditsCollected && creditsPrefab != null)
            {
                var pickup = Instantiate(creditsPrefab, drop.position + Vector3.right * 0.5f + Vector3.up * 0.3f, Quaternion.identity);
                pickup.Release(drop.credits, Vector3.zero);
                pickup.Collected += () => drop.creditsCollected = true;
            }
        }

        public bool CanRecover(RaidDeathDropSave drop)
        {
            var holder = player.GetComponent<WeaponHolder>();
            return !health.IsDead && !drop.bundleCollected &&
                (drop.weapon == null || (holder != null && holder.CanEquip &&
                (holder.EquippedWeapon == null || !holder.EquippedWeapon.IsMoving)));
        }

        public bool Recover(RaidDeathDropSave drop)
        {
            if (!CanRecover(drop)) return false;
            if (drop.weapon != null)
            {
                if (paints == null) paints = PaintMaterialLibrary.CreateDefaults();
                if (!GameSaveController.TryRestoreRaidWeapon(drop.weapon, weaponPrefab, player, paints, false, out var restored, out var error))
                { FeedbackUI.For(this)?.ShowNotification(error); return false; }
                var holder = player.GetComponent<WeaponHolder>();
                var old = holder.EquippedWeapon;
                if (old != null)
                {
                    if (!old.Drop(player.transform.position + player.transform.forward, 0f))
                    { Destroy(restored.gameObject); return false; }
                    holder.TakeWeapon();
                }
                if (!holder.Equip(restored))
                {
                    if (old != null) holder.Equip(old);
                    Destroy(restored.gameObject);
                    return false;
                }
            }
            var unlocks = player.GetComponent<PlayerModuleUnlocks>();
            foreach (int id in drop.modules ?? Array.Empty<int>()) unlocks?.UnlockModule(id);
            drop.bundleCollected = true;
            return true;
        }
    }
}
