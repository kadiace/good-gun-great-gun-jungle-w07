using System;
using System.Collections.Generic;
using System.Collections;
using Unity.Profiling;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class EquipableWeapon : MonoBehaviour, IInteractable
    {
        private static readonly ProfilerMarker IgnorePlayerCollisionsMarker = new ProfilerMarker("EquipableWeapon.IgnorePlayerCollisions");
        private static readonly ProfilerMarker IgnoreBlockPlayerCollisionsMarker = new ProfilerMarker("EquipableWeapon.IgnoreBlockPlayerCollisions");
        // Runtime state belongs to this gun. Only world/travel saves restore it, never blueprints or prefabs.
        private readonly HashSet<string> passedTestIds = new HashSet<string>(StringComparer.Ordinal);
        private string certifiedAssemblyKey;
        private string currentAssemblyKey;
        public event Action AssemblyChanged;

        public string TestAssemblyKey
        {
            get
            {
                if (currentAssemblyKey == null) currentAssemblyKey = ReadAssemblyKey();
                return currentAssemblyKey;
            }
        }
        private string ReadAssemblyKey()
        {
            try { return WeaponShareCode.GetAssemblyKey(this); }
            catch (ArgumentException) { return null; }
        }
        public void NotifyAssemblyChanged()
        {
            passedTestIds.Clear(); certifiedAssemblyKey = null; currentAssemblyKey = null;
            AssemblyChanged?.Invoke();
        }
        public bool RecordPassedTest(string testId, string startedAssemblyKey)
        {
            if (!TableManager.Instance.ShootingTestTable.IsAvailable(testId)) return false;
            string actual = ReadAssemblyKey();
            if (actual == null || actual != startedAssemblyKey) return false;
            currentAssemblyKey = actual;
            if (certifiedAssemblyKey != actual) passedTestIds.Clear();
            certifiedAssemblyKey = actual;
            passedTestIds.Add(testId);
            return true;
        }
        public bool HasPassedTest(string testId) => TableManager.Instance.ShootingTestTable.IsAvailable(testId) &&
            certifiedAssemblyKey != null && certifiedAssemblyKey == TestAssemblyKey && passedTestIds.Contains(testId);

        public string[] CapturePassedTests(out string assemblyKey)
        {
            assemblyKey = null;
            if (passedTestIds.Count == 0) return Array.Empty<string>();
            if (ReadAssemblyKey() != certifiedAssemblyKey)
            { passedTestIds.Clear(); certifiedAssemblyKey = null; currentAssemblyKey = null; return Array.Empty<string>(); }
            assemblyKey = certifiedAssemblyKey;
            var ids = new List<string>();
            foreach (string id in passedTestIds) if (TableManager.Instance.ShootingTestTable.IsAvailable(id)) ids.Add(id);
            ids.Sort(StringComparer.Ordinal);
            return ids.ToArray();
        }
        public void RestorePassedTests(string[] ids, string assemblyKey)
        {
            passedTestIds.Clear(); certifiedAssemblyKey = null; currentAssemblyKey = ReadAssemblyKey();
            if (ids == null || string.IsNullOrEmpty(assemblyKey) || currentAssemblyKey != assemblyKey) return;
            foreach (string id in ids) if (TableManager.Instance.ShootingTestTable.IsAvailable(id)) passedTestIds.Add(id);
            if (passedTestIds.Count > 0) certifiedAssemblyKey = assemblyKey;
        }

        [SerializeField] private string weaponName = "나만의 총";
        public string WeaponName => string.IsNullOrWhiteSpace(weaponName) ? "나만의 총" : weaponName;
        public void SetWeaponName(string value)
        {
            value = (value ?? "").Trim().Replace("\n", " ").Replace("\r", " ");
            weaponName = string.IsNullOrWhiteSpace(value) ? "나만의 총" : value.Substring(0, Mathf.Min(40, value.Length));
        }
        [SerializeField, Min(0.01f)] private float equippedScale = 1f;
        [SerializeField] private Vector3 equippedPosition = new Vector3(0.15f, 0.2f, 0.25f);
        [SerializeField] private Vector3 equippedEulerAngles;
        [SerializeField] private PowerCore powerCore;
        [SerializeField] private Workbench workbench;
        [SerializeField, HideInInspector] private bool constructionLocked;
        [SerializeField, HideInInspector] private Vector3Int selectedScopeCell;
        [SerializeField, HideInInspector] private bool hasSelectedScopeCell;
        public void SetWorkbench(Workbench bench) => workbench = bench;
        public Vector3Int SelectedScopeCell => selectedScopeCell;
        public bool HasSelectedScopeCell => hasSelectedScopeCell;
        public void RestoreSelectedScopeCell(Vector3Int cell, bool hasSelection)
        {
            selectedScopeCell = cell;
            hasSelectedScopeCell = hasSelection;
        }
        public Vector3 UnequippedWorldScale => IsEquipped ? originalWorldScale : transform.lossyScale;
        public Workbench Workbench => workbench;
        public void RestorePlacement(Transform parent, Vector3 position, Quaternion rotation, Vector3 scale,
            bool dynamicBody, bool gravity, Vector3 velocity, Vector3 angularVelocity)
        {
            if (movement != null) StopCoroutine(movement);
            movement = null; IsMoving = false; IsEquipped = false; equippedGripPoint = null; disposing = false;
            RestorePhysics(); dropped = dynamicBody;
            SetDroppedCollisionLayer(dynamicBody);
            transform.SetParent(parent, false);
            transform.localPosition = position; transform.localRotation = rotation; transform.localScale = scale;
            var body = GetComponent<Rigidbody>();
            if (dynamicBody && body == null) body = gameObject.AddComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = !dynamicBody; body.useGravity = gravity;
                if (dynamicBody) { body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; body.linearVelocity = velocity; body.angularVelocity = angularVelocity; }
            }
        }
        public bool ConstructionLocked => constructionLocked;
        public void LockConstruction() => constructionLocked = true;
        public void UnlockConstruction() => constructionLocked = false;
        private Transform originalParent;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Vector3 originalScale;
        private Vector3 originalWorldScale;
        private bool dropped;
        public bool IsDropped => dropped && !IsEquipped && !IsMoving;
        private Transform equippedGripPoint;
        private readonly Dictionary<Collider, bool> colliderStates = new Dictionary<Collider, bool>();
        private readonly Dictionary<Collider, bool> triggerStates = new Dictionary<Collider, bool>();
        private readonly Dictionary<Rigidbody, bool> rigidbodyStates = new Dictionary<Rigidbody, bool>();
        private readonly Dictionary<GameObject, int> droppedColliderLayers = new Dictionary<GameObject, int>();
        private readonly List<Collider> playerColliders = new List<Collider>();
        private int collisionIgnoreBatchDepth;
        private bool collisionIgnoreCacheReady;
        public bool IsEquipped { get; private set; }
        public bool IsMoving { get; private set; }
        private Coroutine movement;
        private bool disposing;
        private PlayerWallet disposalRefundWallet;
        // 실제 결제 금액 중 아직 환불하지 않은 제작비입니다.
        public int PaidConstructionCost { get; private set; }
        public void RecordConstructionPurchase(int amount)
        {
            if (amount > 0) PaidConstructionCost += amount;
        }

        public void RestoreConstructionPurchaseCost(int amount)
        {
            PaidConstructionCost = Mathf.Max(0, amount);
        }

        public int RefundModulePurchases(int amount, PlayerWallet wallet)
        {
            if (wallet == null || amount <= 0) return 0;
            int refund = Mathf.Min(amount, PaidConstructionCost);
            PaidConstructionCost -= refund;
            wallet.RefundConstruction(refund);
            return refund;
        }

        private void RefundConstructionCost()
        {
            PlayerWallet wallet = disposalRefundWallet;
            disposalRefundWallet = null;
            if (wallet == null) return;
            int refund = PaidConstructionCost;
            foreach (PlacedBlock block in GetComponentsInChildren<PlacedBlock>(true))
                wallet.CreditModuleCredits(block.TakeModuleCreditCost());
            PaidConstructionCost = 0;
            wallet.RefundConstruction(refund);
        }
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private Vector3 targetScale;
        public Transform OutlineRoot => transform;
        public bool CanInteract(Interaction interactor)
        {
            if (!isActiveAndEnabled || IsEquipped || IsMoving || interactor == null || interactor.Player == null) return false;
            WeaponHolder holder = interactor.Player.GetComponent<WeaponHolder>();
            if (holder == null || !holder.CanEquip) return false;
            // A rack-stored weapon can only be taken when the player's hand is empty.
            return GetComponentInParent<GunRack>() == null || holder.EquippedWeapon == null;
        }
        private ProjectileStatsSource statsSource;
        public void IgnorePlayerCollisions()
        {
            using (IgnorePlayerCollisionsMarker.Auto())
            {
                if (collisionIgnoreBatchDepth > 0) { collisionIgnoreCacheReady = false; return; }
                var weaponColliders = GetComponentsInChildren<Collider>(true);
                if (!collisionIgnoreCacheReady) RefreshPlayerColliderCache();
                foreach (var playerCollider in playerColliders)
                {
                    if (playerCollider == null || playerCollider.gameObject.scene != gameObject.scene) continue;
                    foreach (var weaponCollider in weaponColliders)
                        if (weaponCollider != null && playerCollider != weaponCollider) Physics.IgnoreCollision(playerCollider, weaponCollider, true);
                }
            }
        }
        internal void IgnorePlayerCollisions(PlacedBlock block)
        {
            using (IgnoreBlockPlayerCollisionsMarker.Auto())
            {
                if (block == null) return;
                if (collisionIgnoreBatchDepth > 0) { collisionIgnoreCacheReady = false; return; }
                if (!collisionIgnoreCacheReady) RefreshPlayerColliderCache();
                var blockColliders = block.GetComponentsInChildren<Collider>(true);
                foreach (var playerCollider in playerColliders)
                {
                    if (playerCollider == null || playerCollider.gameObject.scene != gameObject.scene) continue;
                    foreach (var blockCollider in blockColliders)
                        if (blockCollider != null && playerCollider != blockCollider) Physics.IgnoreCollision(playerCollider, blockCollider, true);
                }
            }
        }
        public IDisposable DeferPlayerCollisionIgnore()
        {
            collisionIgnoreBatchDepth++;
            return new CollisionIgnoreBatch(this);
        }
        private void EndCollisionIgnoreBatch()
        {
            if (collisionIgnoreBatchDepth == 0 || --collisionIgnoreBatchDepth > 0) return;
            collisionIgnoreCacheReady = false;
            IgnorePlayerCollisions();
        }
        private void RefreshPlayerColliderCache()
        {
            playerColliders.Clear();
            foreach (var player in UnityEngine.Object.FindObjectsByType<FirstPersonPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (player.gameObject.scene != gameObject.scene) continue;
                foreach (var playerCollider in player.GetComponentsInChildren<Collider>(true))
                    if (playerCollider != null && playerCollider.GetComponentInParent<EquipableWeapon>() == null)
                        playerColliders.Add(playerCollider);
            }
            collisionIgnoreCacheReady = true;
        }
        private sealed class CollisionIgnoreBatch : IDisposable
        {
            private EquipableWeapon owner;
            public CollisionIgnoreBatch(EquipableWeapon owner) => this.owner = owner;
            public void Dispose()
            {
                if (owner == null) return;
                owner.EndCollisionIgnoreBatch();
                owner = null;
            }
        }
        private void OnEnable()
        {
            collisionIgnoreCacheReady = false;
            IgnorePlayerCollisions();
        }
        private void Awake()
        {
            if (powerCore == null) powerCore = GetComponentInChildren<PowerCore>(true);
            statsSource = powerCore != null ? powerCore.GetComponent<ProjectileStatsSource>() : null;
        }
        public bool TryFire() => IsEquipped && !IsMoving && powerCore != null && powerCore.TryFire();

        // Charge weapons fire on release instead of while held.
        public bool UsesCharge => statsSource != null && statsSource.UsesCharge;
        public void HoldCharge(float deltaTime)
        {
            if (IsEquipped && !IsMoving && statsSource != null) statsSource.HoldCharge(deltaTime);
        }
        public bool ReleaseCharge()
        {
            if (statsSource == null) return false;
            if (!IsEquipped || IsMoving) { statsSource.ResetCharge(); return false; }
            return statsSource.ReleaseCharge();
        }
        public void CancelCharge() => statsSource?.ResetCharge();
        public bool IsCharging => IsEquipped && statsSource != null && statsSource.IsCharging;
        public float ChargeProgress => statsSource != null ? statsSource.ChargeProgress : 0f;
        public int ChargeLevel => statsSource != null ? statsSource.CurrentChargeLevel : 0;
        public int MaxChargeLevel => statsSource != null ? statsSource.MaxChargeLevel : 0;
        public float ChargeShakeDistance => statsSource != null ? statsSource.Tuning.chargeShakeDistance : 0f;
        public ProjectileTuning Tuning => statsSource != null ? statsSource.Tuning : null;
        public float SpreadDegrees => powerCore != null ? powerCore.MaxSpreadDegrees : 0f;
        public bool HasHoming => powerCore != null && powerCore.GetCurrentStats().homing > 0f;

        // Scope modules are a sight for the whole weapon (right-click zoom), not a projectile stat.
        public float ScopePower
        {
            get
            {
                if (powerCore == null) return 0f;
                StatCollector collector = powerCore.GetComponent<StatCollector>();
                if (collector != null) return collector.CurrentStats.scope;
                float scope = 0f;
                foreach (ModuleStats module in powerCore.GetComponentsInChildren<ModuleStats>())
                    if (module.Data != null) scope += module.Data.GetStats().scope;
                return scope;
            }
        }

        // One scope view: the rearmost cell of a straight run of scopes facing the same way.
        public struct ScopeView
        {
            public Vector3Int Cell;
            public Vector3 Position;
            public Vector3 EyePosition;
            public Quaternion Rotation;
            public int Count;
        }

        public int ResolveSelectedScopeIndex(List<ScopeView> views)
        {
            if (views == null || views.Count == 0) return -1;
            if (hasSelectedScopeCell)
                for (int i = 0; i < views.Count; i++)
                    if (views[i].Cell == selectedScopeCell) return i;
            SetSelectedScopeIndex(views, 0);
            return 0;
        }

        public void SetSelectedScopeIndex(List<ScopeView> views, int index)
        {
            if (views == null || views.Count == 0) return;
            index = (index % views.Count + views.Count) % views.Count;
            selectedScopeCell = views[index].Cell;
            hasSelectedScopeCell = true;
        }

        private readonly Dictionary<Vector3Int, Vector3Int> scopeCells = new Dictionary<Vector3Int, Vector3Int>();
        private readonly Dictionary<Vector3Int, Transform> scopeModules = new Dictionary<Vector3Int, Transform>();
        private readonly List<Vector3Int> scopeStarts = new List<Vector3Int>();

        // Only scopes in a line along their own arrow stack (magnification per run); any other scope is its own view.
        // Each view faces its arrow; the camera eye sits behind the actual scope mesh. The weapon itself is not moved.
        public void GetScopeViews(List<ScopeView> views)
        {
            views.Clear();
            if (powerCore == null) return;
            scopeCells.Clear();
            scopeModules.Clear();
            Transform space = null;
            foreach (ModuleStats module in powerCore.GetComponentsInChildren<ModuleStats>())
            {
                if (module.Data == null || module.Data.ModuleType != StatType.Scope) continue;
                PlacedBlock block = module.GetComponent<PlacedBlock>();
                ExtensionModule extension = module.GetComponent<ExtensionModule>();
                if (block == null || extension == null || block.transform.parent == null) continue;
                space = block.transform.parent;
                scopeCells[block.Cell] = extension.OutputDirection;
                scopeModules[block.Cell] = module.transform;
            }
            scopeStarts.Clear();
            foreach (var entry in scopeCells)
                if (!scopeCells.TryGetValue(entry.Key - entry.Value, out Vector3Int behind) || behind != entry.Value)
                    scopeStarts.Add(entry.Key);
            // Stable order keeps each view in the same screen cell.
            scopeStarts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y != b.y ? a.y.CompareTo(b.y) : a.z.CompareTo(b.z));
            foreach (Vector3Int start in scopeStarts)
            {
                Vector3Int axis = scopeCells[start];
                int count = 1;
                while (scopeCells.TryGetValue(start + axis * count, out Vector3Int next) && next == axis) count++;
                Vector3 up = Mathf.Abs(axis.y) == 1 ? Vector3.forward : Vector3.up;
                Vector3 position = space.TransformPoint(start);
                Quaternion rotation = Quaternion.LookRotation(space.TransformDirection(axis), space.TransformDirection(up));
                views.Add(new ScopeView
                {
                    Cell = start,
                    Position = position,
                    EyePosition = GetScopeEyePosition(scopeModules[start], position, rotation),
                    Rotation = rotation,
                    Count = count
                });
            }
        }

        private static Vector3 GetScopeEyePosition(Transform scopeRoot, Vector3 scopeCenter, Quaternion scopeRotation)
        {
            Vector3 forward = scopeRotation * Vector3.forward;
            float rearMost = float.PositiveInfinity;
            float frontMost = float.NegativeInfinity;
            float largestRendererDimension = 0f;
            foreach (Renderer renderer in scopeRoot.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Bounds bounds = renderer.localBounds;
                Transform rendererTransform = renderer.transform;
                Vector3 center = rendererTransform.TransformPoint(bounds.center);
                Vector3 halfX = rendererTransform.TransformVector(Vector3.right * bounds.extents.x);
                Vector3 halfY = rendererTransform.TransformVector(Vector3.up * bounds.extents.y);
                Vector3 halfZ = rendererTransform.TransformVector(Vector3.forward * bounds.extents.z);
                largestRendererDimension = Mathf.Max(largestRendererDimension,
                    2f * Mathf.Max(halfX.magnitude, Mathf.Max(halfY.magnitude, halfZ.magnitude)));
                float projectedCenter = Vector3.Dot(center, forward);
                float projectedExtent = Mathf.Abs(Vector3.Dot(halfX, forward)) +
                                        Mathf.Abs(Vector3.Dot(halfY, forward)) +
                                        Mathf.Abs(Vector3.Dot(halfZ, forward));
                rearMost = Mathf.Min(rearMost, projectedCenter - projectedExtent);
                frontMost = Mathf.Max(frontMost, projectedCenter + projectedExtent);
            }

            if (float.IsInfinity(rearMost))
            {
                Vector3 scale = scopeRoot.lossyScale;
                float size = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                return scopeCenter - forward * size * 2.5f;
            }

            float depth = Mathf.Max(0f, frontMost - rearMost);
            float eyeRelief = Mathf.Max(depth, largestRendererDimension) * 2f;
            float offset = Mathf.Max(0f, Vector3.Dot(scopeCenter, forward) - rearMost) + eyeRelief;
            return scopeCenter - forward * offset;
        }

        public void AimAt(Vector3 aimPoint, Vector3 up)
        {
            if (!IsEquipped || IsMoving || powerCore == null) return;
            Vector3 direction = aimPoint - powerCore.transform.position;
            if (direction.sqrMagnitude < 0.000001f) return;
            // Rotate the whole assembly around the installed trigger so its grip stays in the hand.
            Quaternion correction = Quaternion.LookRotation(direction.normalized, up) * Quaternion.Inverse(powerCore.transform.rotation);
            Vector3 pivot = equippedGripPoint != null ? equippedGripPoint.position : transform.position;
            transform.position = pivot + correction * (transform.position - pivot);
            transform.rotation = correction * transform.rotation;
        }
        public bool Interact(Interaction interactor)
        {
            WeaponHolder holder = interactor.Player.GetComponent<WeaponHolder>();
            return holder != null && holder.Equip(this);
        }

        public bool Equip(Transform handPoint)
        {
            if (IsEquipped || IsMoving || handPoint == null || handPoint.IsChildOf(transform)) return false;
            SetDroppedCollisionLayer(false);
            originalParent = transform.parent;
            originalPosition = transform.localPosition;
            originalRotation = transform.localRotation;
            originalScale = transform.localScale;
            originalWorldScale = transform.lossyScale;
            colliderStates.Clear();
            triggerStates.Clear();
            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            {
                colliderStates.Add(collider, collider.enabled);
                triggerStates.Add(collider, collider.isTrigger);
                if (collider.GetComponentInParent<PlacedBlock>() != null)
                    collider.isTrigger = true;
                else collider.enabled = false;
            }
            rigidbodyStates.Clear();
            IgnorePlayerCollisions();
            foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>(true))
            {
                rigidbodyStates.Add(body, body.isKinematic);
                body.isKinematic = true;
            }
            TriggerModule trigger = GetComponentInChildren<TriggerModule>();
            equippedGripPoint = trigger != null ? trigger.GripPoint : null;
            Quaternion targetRotation = Quaternion.Euler(equippedEulerAngles);
            Vector3 targetPosition = equippedPosition;
            if (equippedGripPoint != null)
            {
                Vector3 gripLocalPosition = transform.InverseTransformPoint(equippedGripPoint.position);
                Vector3 scaledGripLocalPosition = Vector3.Scale(gripLocalPosition, originalScale * equippedScale);
                targetPosition = -(targetRotation * scaledGripLocalPosition);
            }
            IsEquipped = true;
            MoveTo(handPoint, targetPosition, targetRotation, originalScale * equippedScale);
            return true;
        }

        public void ReturnToWorkbench()
        {
            if (!IsEquipped) return;
            if (workbench != null && workbench.CurrentWeapon != null && workbench.CurrentWeapon != this)
            {
                // A swapped weapon must not return on top of the weapon now occupying its old bench.
                if (movement != null) StopCoroutine(movement);
                movement = null;
                IsMoving = false;
                Drop(transform.position + transform.forward, 0f);
                return;
            }
            if (dropped && workbench != null) { workbench.PlaceWeapon(this); return; }
            IsEquipped = false;
            equippedGripPoint = null;
            if (workbench != null) workbench.ResetRotation();
            MoveTo(originalParent, originalPosition, originalRotation, originalScale);
        }

        public void PlaceOnWorkbench(Transform parent)
        {
            SetDroppedCollisionLayer(false);
            Vector3 scale = IsEquipped || colliderStates.Count > 0 ? originalScale : transform.localScale;
            IsEquipped = false;
            equippedGripPoint = null;
            dropped = false;
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                if (rigidbodyStates.ContainsKey(body)) rigidbodyStates[body] = true;
            }
            MoveTo(parent, Vector3.zero, Quaternion.identity, scale);
        }

        public bool Drop(Vector3 aimPoint, float speed)
        {
            if (!IsEquipped || IsMoving) return false;
            IsEquipped = false;
            equippedGripPoint = null;
            dropped = true;
            Vector3 direction = aimPoint - transform.position;
            if (direction.sqrMagnitude < 0.000001f) direction = transform.forward;
            transform.SetParent(null, true);
            transform.localScale = originalWorldScale;
            RestorePhysics();
            SetDroppedCollisionLayer(true);
            Rigidbody body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = direction.normalized * speed;
            body.angularVelocity = Vector3.zero;
            return true;
        }

        /// <summary>
        /// 작업대 파괴 시 거치되어 있던 무기를 월드 바닥으로 떨어뜨려 물리 및 습득 가능 상태로 전환합니다.
        /// </summary>
        /// <returns>낙하 처리가 성공하면 true, 이미 장착 중이면 false를 반환합니다.</returns>
        public bool DropFromWorkbench()
        {
            if (IsEquipped) return false;
            if (movement != null)
            {
                StopCoroutine(movement);
                movement = null;
            }
            IsMoving = false;
            workbench = null;
            equippedGripPoint = null;
            dropped = true;
            transform.SetParent(null, true);
            transform.localScale = originalWorldScale;
            RestorePhysics();
            SetDroppedCollisionLayer(true);
            Rigidbody body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = Vector3.down * 0.2f;
            body.angularVelocity = Vector3.zero;
            return true;
        }

        private void SetDroppedCollisionLayer(bool isDropped)
        {
            if (!isDropped)
            {
                foreach (var entry in droppedColliderLayers)
                    if (entry.Key != null) entry.Key.layer = entry.Value;
                droppedColliderLayers.Clear();
                return;
            }

            int layer = LayerMask.NameToLayer("DroppedGun");
            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            {
                GameObject part = collider.gameObject;
                if (!droppedColliderLayers.ContainsKey(part)) droppedColliderLayers.Add(part, part.layer);
                part.layer = layer;
            }
        }

        private void RestorePhysics()
        {
            foreach (var entry in triggerStates)
                if (entry.Key != null) entry.Key.isTrigger = entry.Value;
            foreach (var entry in colliderStates)
                if (entry.Key != null) entry.Key.enabled = entry.Value;
            foreach (var entry in rigidbodyStates)
                if (entry.Key != null) entry.Key.isKinematic = entry.Value;
            colliderStates.Clear();
            triggerStates.Clear();
            rigidbodyStates.Clear();
            IgnorePlayerCollisions();
        }

        public bool DisposeTo(Transform destination, PlayerWallet refundWallet = null)
        {
            if (!IsEquipped || IsMoving || destination == null) return false;
            IsEquipped = false;
            equippedGripPoint = null;
            disposing = true;
            disposalRefundWallet = refundWallet;
            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            MoveTo(destination, Vector3.zero, Quaternion.identity, originalWorldScale);
            return true;
        }

        private void MoveTo(Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (movement != null) StopCoroutine(movement);
            movement = null;
            transform.SetParent(parent, true);
            targetPosition = position;
            targetRotation = rotation;
            targetScale = scale;
            if (!Application.isPlaying || !isActiveAndEnabled) { FinishMovement(); return; }
            IsMoving = true;
            movement = StartCoroutine(Move());
        }

        private IEnumerator Move()
        {
            Vector3 startPosition = transform.localPosition;
            Quaternion startRotation = transform.localRotation;
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.5f));
                transform.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
                transform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }
            FinishMovement();
        }

        private void FinishMovement()
        {
            transform.localPosition = targetPosition;
            transform.localRotation = targetRotation;
            transform.localScale = targetScale;
            movement = null;
            IsMoving = false;
            if (disposing)
            {
                disposing = false;
                RefundConstructionCost();
                Destroy(gameObject);
                return;
            }
            if (!IsEquipped) RestorePhysics();
        }

        private void OnDisable()
        {
            if (!IsMoving) return;
            if (movement != null) StopCoroutine(movement);
            FinishMovement();
        }
    }
}
