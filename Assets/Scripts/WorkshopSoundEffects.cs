using System;
using UnityEngine;

namespace WeaponWorkshop
{
    internal static class WorkshopSoundEffects
    {
        internal static event Action<Vector3> BlockInstalled;
        internal static event Action<Vector3> BlockRemoved;
        internal static event Action<Vector3> AssemblyCompleted;
        internal static event Action<Vector3> WeaponEquipped;
        internal static event Action<Vector3> WeaponDropped;
        internal static event Action<Vector3> WeaponDisplayed;
        internal static event Action<Vector3> WeaponPlacedOnTable;
        internal static event Action<Vector3> ShopShutterMoved;
        internal static event Action<Vector3> BusinessStarted;
        internal static event Action BusinessEnded;
        internal static event Action NightRaidStarted;
        internal static event Action<Vector3> BlockFragmented;
        internal static event Action<Vector3> TankAppeared;
        internal static event Action<Vector3> TankLanded;
        internal static event Action<Vector3> TankAttacked;
        internal static event Action<Transform> TankMovementStarted;
        internal static event Action<Transform> TankMovementStopped;
        internal static event Action<Transform> HelicopterAmbientStarted;
        internal static event Action<Transform> HelicopterAmbientStopped;
        internal static event Action<Vector3> HelicopterAttacked;
        internal static event Action<Vector3> WeaponSold;
        internal static event Action<Vector3> WeaponDisposed;
        internal static event Action<Vector3> CustomLimitReached;
        internal static event Action<Vector3> BasicShotFired;
        internal static event Action<Vector3> ChargeStarted;
        internal static event Action<Transform> ChargeLoopStarted;
        internal static event Action<Transform> ChargeLoopStopped;
        internal static event Action<Vector3> ChargeFullyLoaded;
        internal static event Action<Vector3> ChargedShotFired;
        internal static event Action<Vector3> ShotgunFired;
        internal static event Action<Vector3> WaterFired;
        internal static event Action<Vector3> ExplosionTriggered;
        internal static event Action<FirstPersonPlayer, float, float, bool, bool, float> PlayerMovementUpdated;
        internal static event Action<FirstPersonPlayer> PlayerMovementStopped;
        internal static event Action<FirstPersonPlayer> PlayerJumped;
        internal static event Action<GravityField> GravityFieldStarted;
        internal static event Action<GravityField> GravityFieldStopped;

        internal static void RaiseBlockInstalled(Vector3 position) => BlockInstalled?.Invoke(position);
        internal static void RaiseBlockRemoved(Vector3 position) => BlockRemoved?.Invoke(position);
        internal static void RaiseAssemblyCompleted(Vector3 position) => AssemblyCompleted?.Invoke(position);
        internal static void RaiseWeaponEquipped(Vector3 position) => WeaponEquipped?.Invoke(position);
        internal static void RaiseWeaponDropped(Vector3 position) => WeaponDropped?.Invoke(position);
        internal static void RaiseWeaponDisplayed(Vector3 position) => WeaponDisplayed?.Invoke(position);
        internal static void RaiseWeaponPlacedOnTable(Vector3 position) => WeaponPlacedOnTable?.Invoke(position);
        internal static void RaiseShopShutterMoved(Vector3 position) => ShopShutterMoved?.Invoke(position);
        internal static void RaiseBusinessStarted(Vector3 position) => BusinessStarted?.Invoke(position);
        internal static void RaiseBusinessEnded() => BusinessEnded?.Invoke();
        internal static void RaiseNightRaidStarted() => NightRaidStarted?.Invoke();
        internal static void RaiseBlockFragmented(Vector3 position) => BlockFragmented?.Invoke(position);
        internal static void RaiseTankAppeared(Vector3 position) => TankAppeared?.Invoke(position);
        internal static void RaiseTankLanded(Vector3 position) => TankLanded?.Invoke(position);
        internal static void RaiseTankAttacked(Vector3 position) => TankAttacked?.Invoke(position);
        internal static void RaiseTankMovementStarted(Transform tank) => TankMovementStarted?.Invoke(tank);
        internal static void RaiseTankMovementStopped(Transform tank) => TankMovementStopped?.Invoke(tank);
        internal static void RaiseHelicopterAmbientStarted(Transform helicopter) => HelicopterAmbientStarted?.Invoke(helicopter);
        internal static void RaiseHelicopterAmbientStopped(Transform helicopter) => HelicopterAmbientStopped?.Invoke(helicopter);
        internal static void RaiseHelicopterAttacked(Vector3 position) => HelicopterAttacked?.Invoke(position);
        internal static void RaiseWeaponSold(Vector3 position) => WeaponSold?.Invoke(position);
        internal static void RaiseWeaponDisposed(Vector3 position) => WeaponDisposed?.Invoke(position);
        internal static void RaiseCustomLimitReached(Vector3 position) => CustomLimitReached?.Invoke(position);
        internal static void RaiseBasicShotFired(Vector3 position) => BasicShotFired?.Invoke(position);
        internal static void RaiseChargeStarted(Vector3 position) => ChargeStarted?.Invoke(position);
        internal static void RaiseChargeLoopStarted(Transform source) => ChargeLoopStarted?.Invoke(source);
        internal static void RaiseChargeLoopStopped(Transform source) => ChargeLoopStopped?.Invoke(source);
        internal static void RaiseChargeFullyLoaded(Vector3 position) => ChargeFullyLoaded?.Invoke(position);
        internal static void RaiseChargedShotFired(Vector3 position) => ChargedShotFired?.Invoke(position);
        internal static void RaiseShotgunFired(Vector3 position) => ShotgunFired?.Invoke(position);
        internal static void RaiseWaterFired(Vector3 position) => WaterFired?.Invoke(position);
        internal static void RaiseExplosionTriggered(Vector3 position) => ExplosionTriggered?.Invoke(position);
        internal static void RaisePlayerMovementUpdated(FirstPersonPlayer player, float horizontalSpeed,
            float walkingSpeed, bool grounded, bool sprinting, float deltaTime)
            => PlayerMovementUpdated?.Invoke(player, horizontalSpeed, walkingSpeed, grounded, sprinting, deltaTime);
        internal static void RaisePlayerMovementStopped(FirstPersonPlayer player) => PlayerMovementStopped?.Invoke(player);
        internal static void RaisePlayerJumped(FirstPersonPlayer player) => PlayerJumped?.Invoke(player);
        internal static void RaiseGravityFieldStarted(GravityField field) => GravityFieldStarted?.Invoke(field);
        internal static void RaiseGravityFieldStopped(GravityField field) => GravityFieldStopped?.Invoke(field);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubscribers()
        {
            BlockInstalled = null;
            BlockRemoved = null;
            AssemblyCompleted = null;
            WeaponEquipped = null;
            WeaponDropped = null;
            WeaponDisplayed = null;
            WeaponPlacedOnTable = null;
            ShopShutterMoved = null;
            BusinessStarted = null;
            BusinessEnded = null;
            NightRaidStarted = null;
            BlockFragmented = null;
            TankAppeared = null;
            TankLanded = null;
            TankAttacked = null;
            TankMovementStarted = null;
            TankMovementStopped = null;
            HelicopterAmbientStarted = null;
            HelicopterAmbientStopped = null;
            HelicopterAttacked = null;
            WeaponSold = null;
            WeaponDisposed = null;
            CustomLimitReached = null;
            BasicShotFired = null;
            ChargeStarted = null;
            ChargeLoopStarted = null;
            ChargeLoopStopped = null;
            ChargeFullyLoaded = null;
            ChargedShotFired = null;
            ShotgunFired = null;
            WaterFired = null;
            ExplosionTriggered = null;
            PlayerMovementUpdated = null;
            PlayerMovementStopped = null;
            PlayerJumped = null;
            GravityFieldStarted = null;
            GravityFieldStopped = null;
        }
    }
}
