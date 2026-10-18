using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
namespace WeaponWorkshop
{
    [Serializable] public sealed class WorldSaveBinding
    {
        public string id;
        public string legacyId;
        public string[] alternateIds = Array.Empty<string>();
        public WorldSaveIdentity target;
    }
    public sealed partial class GameSaveController
    {
        private static readonly ProfilerMarker ResolveWorldSaveIdMarker = new ProfilerMarker("GameSaveController.ResolveWorldSaveId");
        [Header("Save / Load")]
        [SerializeField] private bool restoreOnStart = true;
        [SerializeField] private bool confirmOverwrite = true;
        [SerializeField] private bool prettyPrint = true;
        [Header("World Data")]
        [SerializeField] private bool saveMapStructure = true;
        [SerializeField] private bool saveAllWeapons = true;
        [SerializeField] private bool saveTargetDamage = true;
        [SerializeField] private bool saveWeaponPhysics = true;
        [SerializeField, Min(1)] private int maximumWeapons = 256;
        [SerializeField, HideInInspector] private WorldSaveBinding[] mapObjects = Array.Empty<WorldSaveBinding>();
        private PaintMaterialLibrary paintLibrary;
        private readonly Dictionary<string, WorldSaveIdentity> worldBindingsById = new Dictionary<string, WorldSaveIdentity>(StringComparer.Ordinal);
        private readonly Dictionary<string, Material> resolvedPaints = new Dictionary<string, Material>(StringComparer.Ordinal);
        private readonly HashSet<string> unresolvedPaints = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Vector3Int[] paintFaces = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back };
        private void Awake()
        {
            ResolveSceneReferences();
            EnsureWorldBindings();
            RebuildWorldBindingLookup();
        }

        private void ResolveSceneReferences()
        {
            var scene = gameObject.scene;
            if (dayManager == null)
                dayManager = FindUniqueSceneComponent<_DayManager>(scene);
            if (player == null)
                player = FindUniqueSceneComponent<FirstPersonPlayer>(scene);
            if (workbenches == null || workbenches.Length == 0 || workbenches.All(value => value == null))
            {
                var candidates = FindObjectsByType<Workbench>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(value => value.gameObject.scene == scene).ToArray();
                if (candidates.Length == 1) workbenches = candidates;
            }
        }

        private T FindUniqueSceneComponent<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
        {
            var matches = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(value => value.gameObject.scene == scene).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }
        private void OnDestroy() { paintLibrary?.Dispose(); }
        private static PaintFaceSave[] CapturePaint(CubeAssembly assembly)
        {
            var faces = new List<PaintFaceSave>();
            foreach (var block in assembly.GetComponentsInChildren<PlacedBlock>(true))
            {
                if (block.transform.parent != assembly.transform) continue;
                var paint = block.GetComponent<ModuleFacePaint>(); if (paint == null) continue;
                foreach (var face in paintFaces)
                {
                    var material = paint.GetMaterial(face); if (material == null) continue;
                    faces.Add(new PaintFaceSave { cell = block.Cell, face = face, materialKey = material.name });
                }
            }
            return faces.ToArray();
        }
        private Material ResolvePaint(string key)
        {
            if (key != null && resolvedPaints.TryGetValue(key, out var cached)) return cached;
            if (key != null && unresolvedPaints.Contains(key)) return null;
            if (paintLibrary == null) paintLibrary = PaintMaterialLibrary.CreateDefaults();
            var match = paintLibrary.Options.FirstOrDefault(p => p.material != null && p.material.name == key);
            if (match != null) { if (key != null) resolvedPaints[key] = match.material; return match.material; }
            const string custom = "Paint - Custom RGB #";
            if (key != null && key.StartsWith(custom, StringComparison.Ordinal) &&
                ColorUtility.TryParseHtmlString("#" + key.Substring(custom.Length), out var color))
            {
                var material = paintLibrary.GetCustomColorMaterial(color);
                if (material != null) resolvedPaints[key] = material;
                return material;
            }
            if (key != null) unresolvedPaints.Add(key);
            return null;
        }
        public bool ConfirmOverwrite => confirmOverwrite;
        public string SaveDirectory => System.IO.Path.Combine(Application.persistentDataPath, "Saves");
        private T[] InScene<T>() where T : Component => FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(c => c.gameObject.scene == gameObject.scene).ToArray();
        private string Identity(Component component) => component != null ? component.GetComponent<WorldSaveIdentity>()?.Id : null;
        private WorldSaveIdentity Resolve(string id)
        {
            using (ResolveWorldSaveIdMarker.Auto())
            {
            if (string.IsNullOrEmpty(id)) return null;
            if (worldBindingsById.Count == 0) RebuildWorldBindingLookup();
            return worldBindingsById.TryGetValue(id, out var target) ? target : null;
            }
        }
        private void RebuildWorldBindingLookup()
        {
            worldBindingsById.Clear();
            foreach (var binding in mapObjects)
            {
                if (binding == null) continue;
                AddBindingKey(binding.id, binding.target);
                AddBindingKey(binding.legacyId, binding.target);
                if (binding.alternateIds == null) continue;
                foreach (var id in binding.alternateIds) AddBindingKey(id, binding.target);
            }
        }
        private void AddBindingKey(string id, WorldSaveIdentity target)
        {
            if (!string.IsNullOrEmpty(id) && !worldBindingsById.ContainsKey(id)) worldBindingsById.Add(id, target);
        }
        private static SaveBlock[] CaptureBlocks(CubeAssembly.LayoutSnapshot layout) => layout.blocks.Select(b => new SaveBlock
        { slot = b.slot, cell = b.cell, output = b.outputDirection, normal = b.orientationNormal,
            paidCost = b.paidPurchaseCost, paidModuleCreditCost = b.paidModuleCreditCost, hasColor = b.hasColor, color = b.color }).ToArray();
        private void EnsureWorldBindings()
        {
            if (mapObjects != null && mapObjects.Length > 0) return;
            var bindings = new List<WorldSaveBinding>();
            foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponentInParent<UnityEngine.Canvas>() != null || t.GetComponentInParent<UnityEngine.EventSystems.EventSystem>() != null ||
                    t.GetComponentInParent<FirstPersonPlayer>() != null || t.GetComponentInParent<EquipableWeapon>() != null ||
                    t.GetComponentInParent<Customer>() != null || t.GetComponent<Camera>() != null ||
                    t.GetComponentInParent<ShootingTestLayout>() != null ||
                    t.GetComponent<GameSaveController>() != null || t.GetComponent<GameManager>() != null || t.GetComponent<_DayManager>() != null ||
                    t.GetComponentInParent<PoolManager>() != null) continue;
                var identity = t.GetComponent<WorldSaveIdentity>();
                if (identity == null) identity = t.gameObject.AddComponent<WorldSaveIdentity>();
                string path = "";
                for (var node = t; node != null; node = node.parent) path = node.GetSiblingIndex() + "/" + path;
                identity.SetFallbackId(gameObject.scene.name + ":" + path);
                bindings.Add(new WorldSaveBinding { id = identity.Id, target = identity });
            }
            mapObjects = bindings.ToArray();
            RebuildWorldBindingLookup();
        }
        private bool CaptureWorld(GameSaveData data, out string error)
        {
            error = null;
            data.savedMap = saveMapStructure; data.savedWeapons = saveAllWeapons; data.savedTargets = saveTargetDamage;
            EnsureWorldBindings();
            if (saveMapStructure) data.map = mapObjects.Where(b => b.target == null || b.target.GetComponent<Camera>() == null).Select(b => b.target == null ? new MapObjectSave { id = b.id, exists = false } :
                new MapObjectSave { id = b.id, exists = true, parentId = Identity(b.target.transform.parent),
                    root = b.target.transform.parent == null, active = b.target.gameObject.activeSelf, position = b.target.transform.localPosition,
                    rotation = b.target.transform.localRotation, scale = b.target.transform.localScale }).ToArray();
            if (saveAllWeapons)
            {
                var weapons = InScene<EquipableWeapon>().Where(w => w.GetComponent<PresetWeaponPreview>() == null).ToArray();
                if (weapons.Length > maximumWeapons) { error = "저장 가능한 무기 수를 초과했습니다."; return false; }
                var states = new List<WeaponWorldSave>();
                foreach (var weapon in weapons)
                {
                    if (weapon.IsMoving) { error = "모든 무기의 이동이 끝난 후 저장하세요."; return false; }
                    var assembly = weapon.GetComponentInChildren<CubeAssembly>(true);
                    if (assembly == null || !assembly.TryCaptureLayout(out var layout)) { error = "무기 구성을 저장할 수 없습니다."; return false; }
                    var rack = weapon.GetComponentInParent<GunRack>();
                    var bench = weapon.GetComponentInParent<Workbench>();
                    var body = weapon.GetComponent<Rigidbody>();
                    if ((rack != null && string.IsNullOrEmpty(Identity(rack))) || (bench != null && string.IsNullOrEmpty(Identity(bench))))
                    { error = "무기 거치 위치의 저장 ID 연결을 확인하세요."; return false; }
                    bool hasSavedParent = !string.IsNullOrEmpty(Identity(weapon.transform.parent));
                    var passedTests = weapon.CapturePassedTests(out var testKey);
                    states.Add(new WeaponWorldSave { weaponName = weapon.WeaponName, passedTestIds = passedTests, testAssemblyKey = testKey,
                        held = weapon.IsEquipped, rackId = Identity(rack), benchId = Identity(bench),
                        returnBenchId = Identity(weapon.Workbench), parentId = Identity(weapon.transform.parent), active = weapon.gameObject.activeSelf,
                        position = hasSavedParent ? weapon.transform.localPosition : weapon.transform.position, rotation = hasSavedParent ? weapon.transform.localRotation : weapon.transform.rotation,
                        scale = weapon.IsEquipped ? weapon.UnequippedWorldScale : weapon.transform.localScale,
                        dynamicBody = body != null && !body.isKinematic, gravity = body != null && body.useGravity,
                        velocity = saveWeaponPhysics && body != null && !body.isKinematic ? body.linearVelocity : Vector3.zero,
                        angularVelocity = saveWeaponPhysics && body != null && !body.isKinematic ? body.angularVelocity : Vector3.zero,
                        paidCost = layout.paidConstructionCost, blocks = CaptureBlocks(layout), constructionLocked = weapon.ConstructionLocked,
                        hasSelectedScopeCell = weapon.HasSelectedScopeCell, selectedScopeCell = weapon.SelectedScopeCell, paint = CapturePaint(assembly) });
                }
                data.weapons = states.ToArray();
            }
            if (saveTargetDamage)
            {
                var states = new List<TargetWorldSave>();
                foreach (var binding in mapObjects.Where(b => b.target != null))
                {
                    var bell = binding.target.GetComponent<TargetBell>();
                    var target = binding.target.GetComponent<DestructibleTarget>();
                    var state = bell != null ? bell.CaptureSave() : target != null ? target.CaptureSave() : null;
                    if (state == null) continue;
                    state.id = binding.id; states.Add(state);
                }
                data.targets = states.ToArray();
            }
            return true;
        }
        private bool RestoreWorldSave(GameSaveData data)
        {
            using (dayManager != null ? dayManager.SuppressShopLogs() : null) return RestoreWorldSaveCore(data);
        }
        /// <summary>
        /// 저장된 월드·플레이어·곡 해금 데이터를 현재 씬에 복원한다.
        /// data의 저장 오브젝트를 복원하고 성공 여부를 반환한다.
        /// </summary>
        private bool RestoreWorldSaveCore(GameSaveData data)
        {
            EnsureWorldBindings();
            if (data.sceneName != gameObject.scene.name || dayManager == null || !dayManager.CanRestoreSave(data) || player == null || corePrefab == null ||
                player.GetComponent<PlayerWallet>() == null || player.GetComponent<WeaponHolder>() == null)
            { Debug.LogError("저장된 씬/일차와 저장 관리자 연결을 확인하세요.", this); return false; }
            var template = corePrefab.GetComponentInChildren<CubeAssembly>(true);
            bool invalidWeapons = false;
            if (data.savedWeapons)
            {
                if (data.weapons.Length > maximumWeapons || template == null) invalidWeapons = true;
                int heldWeapons = 0;
                for (int i = 0; !invalidWeapons && i < data.weapons.Length; i++)
                {
                    var saved = data.weapons[i];
                    if (saved.held && ++heldWeapons > 1) { invalidWeapons = true; break; }
                    foreach (var block in saved.blocks)
                        if (template.GetPrefab(block.slot) == null) { invalidWeapons = true; break; }
                    if (invalidWeapons) break;
                    if (saved.paint != null)
                        foreach (var face in saved.paint)
                            if (face == null || ResolvePaint(face.materialKey) == null) { invalidWeapons = true; break; }
                    if (invalidWeapons) break;
                    if (!string.IsNullOrEmpty(saved.rackId) && Resolve(saved.rackId)?.GetComponent<GunRack>() == null) invalidWeapons = true;
                    if (!string.IsNullOrEmpty(saved.benchId) && Resolve(saved.benchId)?.GetComponent<Workbench>() == null) invalidWeapons = true;
                }
            }
            if (invalidWeapons)
            { Debug.LogError("저장 무기의 모듈 또는 거치 위치를 복원할 수 없습니다.", this); return false; }
            bool invalidMap = false;
            if (data.savedMap)
                foreach (var saved in data.map)
                    if (saved.exists && Resolve(saved.id) == null) { invalidMap = true; break; }
            if (invalidMap)
            { Debug.LogError("저장된 맵 오브젝트 ID가 현재 씬에 없습니다.", this); return false; }
            var session = player.GetComponent<WorkshopSession>();
            if (data.inWorkshop && (workbenches == null || data.workbench < 0 || data.workbench >= workbenches.Length || workbenches[data.workbench] == null))
            { Debug.LogError("저장된 작업대가 없습니다.", this); return false; }
            GameManager.Instance?.UI?.GetComponent<ShootingTestHUD>()?.CancelTest();
            if (data.savedWeapons)
            {
                player.GetComponent<WeaponHolder>()?.TakeWeapon();
                foreach (var weapon in InScene<EquipableWeapon>().Where(w => w.GetComponent<PresetWeaponPreview>() == null)) { weapon.gameObject.SetActive(false); Destroy(weapon.gameObject); }
            }
            if (data.savedMap)
            {
                foreach (var state in data.map.Where(m => m.exists))
                {
                    var obj = Resolve(state.id); var parent = Resolve(state.parentId);
                    if (parent != null || state.root) obj.transform.SetParent(parent != null ? parent.transform : null, false);
                    obj.transform.localPosition = state.position; obj.transform.localRotation = state.rotation; obj.transform.localScale = state.scale;
                }
                foreach (var state in data.map)
                {
                    var obj = Resolve(state.id); if (obj == null) continue;
                    if (!state.exists) { obj.gameObject.SetActive(false); Destroy(obj.gameObject); }
                    else obj.gameObject.SetActive(state.active);
                }
            }
            player.RestorePose(data.playerPosition, data.playerEuler, data.viewEuler);
            if (data.savedWeapons)
            foreach (var state in data.weapons)
            {
                var weapon = Instantiate(corePrefab);
                weapon.SetWeaponName(state.weaponName);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(weapon.gameObject, gameObject.scene);
                var assembly = weapon.GetComponentInChildren<CubeAssembly>(true);
                var blocks = new CubeAssembly.BlockSnapshot[state.blocks.Length];
                for (int i = 0; i < blocks.Length; i++)
                {
                    var saved = state.blocks[i];
                    blocks[i] = new CubeAssembly.BlockSnapshot { slot = saved.slot, prefab = assembly.GetPrefab(saved.slot),
                        cell = saved.cell, outputDirection = saved.output, orientationNormal = saved.normal, paidPurchaseCost = saved.paidCost,
                        paidModuleCreditCost = saved.paidModuleCreditCost, hasColor = saved.hasColor, color = saved.color };
                }
                using (weapon.DeferPlayerCollisionIgnore())
                if (!assembly.RestoreLayout(new CubeAssembly.LayoutSnapshot(blocks, state.paidCost)))
                { Destroy(weapon.gameObject); Debug.LogError("저장 무기 조립 복원에 실패했습니다.", this); return false; }
                weapon.RestorePassedTests(state.passedTestIds, state.testAssemblyKey);
                var blocksByCell = new Dictionary<Vector3Int, PlacedBlock>();
                foreach (var block in assembly.GetComponentsInChildren<PlacedBlock>(true))
                    if (block.transform.parent == assembly.transform && !blocksByCell.ContainsKey(block.Cell)) blocksByCell.Add(block.Cell, block);
                foreach (var face in state.paint ?? Array.Empty<PaintFaceSave>())
                {
                    if (!blocksByCell.TryGetValue(face.cell, out var block)) continue;
                    var paint = block.GetComponent<ModuleFacePaint>(); if (paint == null) paint = block.gameObject.AddComponent<ModuleFacePaint>();
                    paint.SetMaterial(face.face, ResolvePaint(face.materialKey));
                }
                if (state.constructionLocked) weapon.LockConstruction();
                weapon.RestoreSelectedScopeCell(state.selectedScopeCell, state.hasSelectedScopeCell);
                var rack = Resolve(state.rackId)?.GetComponent<GunRack>();
                var bench = Resolve(state.benchId)?.GetComponent<Workbench>();
                Transform parent = rack != null ? rack.DisplayPoint : bench != null ? bench.RotationTarget : Resolve(state.parentId)?.transform;
                weapon.SetWorkbench(Resolve(state.returnBenchId)?.GetComponent<Workbench>());
                weapon.RestorePlacement(parent, state.position, state.rotation, state.scale, state.dynamicBody, state.gravity, state.velocity, state.angularVelocity);
                weapon.gameObject.SetActive(state.active);
                if (state.held) player.GetComponent<WeaponHolder>().Equip(weapon);
            }
            if (data.savedTargets)
            foreach (var state in data.targets)
            {
                var obj = Resolve(state.id); if (obj == null) continue;
                if (state.spawnedByBell) obj.GetComponent<TargetBell>()?.RestoreSave(state);
                else obj.GetComponent<DestructibleTarget>()?.RestoreSave(state);
            }
            if (data.inWorkshop)
            {
                var bench = workbenches[data.workbench]; bench.RotationTarget.localRotation = Quaternion.Euler(data.benchEuler);
                session?.Enter(bench, false);
            }
            player.GetComponent<PlayerWallet>().RestoreBalance(data.balance);
            FindUniqueSceneComponent<SlotMachineReels>(gameObject.scene)?.RestoreSpins(data.slotMachineSpins);
            player.GetComponent<PlayerWallet>().RestoreLoans(data.outstandingLoans);
            player.GetComponent<PlayerWallet>().RestoreLastLoanDay(data.lastLoanDay);
            player.GetComponent<PlayerWallet>().RestoreModuleCredits(data.moduleCredits);
            player.GetComponent<PlayerModuleUnlocks>()?.RestoreUnlockedModules(data.unlockedModules);
            player.GetComponent<WeaponPresets>()?.Restore(data.weaponPresets);
            dayManager.RestoreSavedState(data);
            RestoreRaidProgress(data);
            RestoreTrackUnlocks(data);
            retired = data.retired;
            retirementSavedDay = retired ? data.day : -1;
            RefreshRetireButton();
            return true;
        }
    }
}
