using UnityEngine;
namespace WeaponWorkshop
{
    public sealed class TargetBell : MonoBehaviour, IInteractable
    {
        [SerializeField] private DestructibleTarget targetPrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private TargetSelectionUI selectionUI;
        [SerializeField] private ShootingTestController testController;
        [SerializeField] private Bell customerBell;
        public System.Collections.Generic.List<string> RequiredTestIds()
        {
            var ids = new System.Collections.Generic.List<string>();
            var customer = customerBell != null ? customerBell.Current : null;
            if (customer == null || !customer.HasActiveOrder || customer.Profile == null) return ids;
            foreach (var requestId in customer.Profile.RequestIds)
                foreach (var id in TableManager.Instance.CustomerRequest.FindRequest(requestId)?.requiredTestIds ?? System.Array.Empty<string>())
                    if (!ids.Contains(id)) ids.Add(id);
            return ids;
        }
        public bool AllowsTest(string testId)
        {
            var required = RequiredTestIds();
            return required.Count == 0 || required.Contains(testId);
        }
        public bool AllowsPracticeTargets => RequiredTestIds().Count == 0;
        public ShootingTestController TestController => testController;
        public string SpawnFailureReason { get; private set; }
        [SerializeField, Min(0.01f), Tooltip("기본 허용 높이와 폭발저항 1당 추가 허용 높이(m). 소환 표적에 적용. 허용 높이 = (폭발저항 + 1) × 이 값.")]
        private float fallMetersPerPower = 5f;

        internal bool IsSpawnBlocked(GameObject prefab, Transform point)
        {
            Bounds localBounds = default;
            bool found = false;
            foreach (var box in prefab.GetComponentsInChildren<BoxCollider>(true))
            {
                if (!box.enabled || box.isTrigger || !box.gameObject.activeInHierarchy) continue;
                for (int x=-1;x<=1;x+=2) for (int y=-1;y<=1;y+=2) for (int z=-1;z<=1;z+=2)
                {
                    Vector3 corner = box.center + Vector3.Scale(box.size*0.5f,new Vector3(x,y,z));
                    Vector3 local = prefab.transform.InverseTransformPoint(box.transform.TransformPoint(corner));
                    if (!found) { localBounds = new Bounds(local,Vector3.zero); found = true; }
                    else localBounds.Encapsulate(local);
                }
            }
            if (!found)
                foreach (var mesh in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mesh.sharedMesh == null) continue;
                    var bounds = mesh.sharedMesh.bounds;
                    for (int x=-1;x<=1;x+=2) for (int y=-1;y<=1;y+=2) for (int z=-1;z<=1;z+=2)
                    {
                        Vector3 corner = bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z));
                        Vector3 local = prefab.transform.InverseTransformPoint(mesh.transform.TransformPoint(corner));
                        if (!found) { localBounds=new Bounds(local,Vector3.zero); found=true; }
                        else localBounds.Encapsulate(local);
                    }
                }
            if (!found) return false;
            Vector3 scale = prefab.transform.localScale;
            scale = Vector3.Scale(scale, point.lossyScale);
            Vector3 center = point.position + point.rotation * Vector3.Scale(localBounds.center,scale);
            Vector3 extents = Vector3.Scale(localBounds.extents,new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
            extents = Vector3.Max(Vector3.one*0.001f,extents-Vector3.one*0.001f);
            Physics.SyncTransforms();
            return Physics.CheckBox(center,extents,point.rotation,~LayerMask.GetMask("Energy"),QueryTriggerInteraction.Ignore);
        }
        private Transform spawnedRoot;
        private Customer cleanedTestOrder;
        private readonly System.Collections.Generic.Dictionary<DestructibleTarget, int> spawnedPrefabs = new System.Collections.Generic.Dictionary<DestructibleTarget, int>();
        public TargetWorldSave CaptureSave()
        {
            var states = new System.Collections.Generic.List<TargetSpawnSave>();
            foreach (var pair in spawnedPrefabs)
            {
                if (pair.Key == null) continue;
                var state = pair.Key.CaptureSave();
                state.prefabSlot = pair.Value;
                var body = pair.Key.GetComponent<Rigidbody>();
                if (body != null) { state.velocity = body.linearVelocity; state.angularVelocity = body.angularVelocity; }
                states.Add(TargetSpawnSave.From(state));
            }
            return new TargetWorldSave { spawnedByBell = true, exists = states.Count > 0, spawnedTargets = states.ToArray() };
        }
        public void RestoreSave(TargetWorldSave state)
        {
            ClearSpawned();
            if (state == null || !state.exists || spawnPoint == null) return;
            ResolveSelectionUI();
            // Older saves contain one default target instead of a collection.
            var states = state.spawnedTargets ?? new[] { TargetSpawnSave.From(state) };
            foreach (var saved in states)
            {
                var prefab = saved.prefabSlot == -1 ? DefaultTargetPrefab : selectionUI != null ? selectionUI.GetSavePrefab(saved.prefabSlot) : null;
                if (prefab == null) { Debug.LogWarning("저장된 사격장 표적 프리팹 연결을 확인하세요.", this); continue; }
                EnsureSpawnedRoot();
                var prototype = Instantiate(prefab); prototype.SetActive(false);
                if (saved.prefabSlot == -2) prototype.GetComponent<DestructibleTarget>().ConfigureCustom(saved.meshSize, saved.penetration, saved.explosion, saved.heat, saved.water);
                var instance = Instantiate(prototype, spawnPoint.position, spawnPoint.rotation, spawnedRoot);
                Destroy(prototype);
                var target = instance.GetComponent<DestructibleTarget>();
                if (instance.GetComponent<Rigidbody>() == null) instance.AddComponent<Rigidbody>();
                var falling = instance.GetComponent<FallingRangeTarget>();
                if (falling == null) falling = instance.AddComponent<FallingRangeTarget>();
                falling.SetFallMetersPerPower(fallMetersPerPower);
                instance.SetActive(true); target.RestoreSave(saved.ToSave());
                var body = instance.GetComponent<Rigidbody>(); body.linearVelocity = saved.velocity; body.angularVelocity = saved.angularVelocity;
                spawnedPrefabs[target] = saved.prefabSlot;
            }
        }
        private void EnsureSpawnedRoot()
        {
            if (spawnedRoot != null) return;
            spawnedRoot = new GameObject("SpawnedRangeTargets").transform;
            spawnedRoot.SetParent(transform.parent, true);
        }
        public Transform OutlineRoot => transform;
        public bool CanInteract(Interaction interactor) => isActiveAndEnabled &&
            !GameManager.GameplayBlocked && spawnPoint != null && (testController == null || !testController.IsActive);
        public bool Interact(Interaction interactor)
        {
            if (!CanInteract(interactor)) return false;
            ResolveSelectionUI();
            if (!AllowsPracticeTargets)
            {
                if (ReadyRequiredTest(interactor.Player))
                {
                    if (testController.CanRetryWithWeapon) testController.Retry();
                    else FeedbackUI.For(this)?.ShowNotification("테스트할 총을 손에 들고 시작하세요.");
                }
                else FeedbackUI.For(this)?.ShowNotification(SpawnFailureReason);
                return true;
            }
            return selectionUI != null && selectionUI.Open(this);
        }

        public bool ReadyRequiredTest(FirstPersonPlayer player)
        {
            var required = RequiredTestIds();
            if (required.Count == 0) return false;
            var customer = customerBell.Current;
            // 첫 가이드 노출 여부와 무관하게 사격 요구가 있는 주문마다 한 번 청소한다.
            if (cleanedTestOrder != customer)
            {
                ClearSpawned();
                cleanedTestOrder = customer;
            }
            ResolveSelectionUI();
            string preferred = TableManager.Instance.Onboarding.shootingGuide?.preferredTestId;
            required.Sort(System.StringComparer.Ordinal);
            string id = required.Contains(preferred) ? preferred : required[0];
            var equipped = player != null ? player.GetComponent<WeaponHolder>()?.EquippedWeapon : null;
            // 시험이 여러 개인 기존 주문은 아직 합격하지 않은 시험을 우선한다.
            if (equipped != null && equipped.HasPassedTest(id))
                foreach (string candidate in required)
                    if (!equipped.HasPassedTest(candidate)) { id = candidate; break; }
            var data = selectionUI != null ? selectionUI.FindTest(id) : null;
            SpawnFailureReason = "주문에서 요구한 사격 테스트 연결을 확인하세요.";
            if (testController == null || data == null) return false;
            bool ready = testController.ReadyForOrder(data, player, customer, out var error);
            SpawnFailureReason = error;
            return ready;
        }

        private void ResolveSelectionUI()
        {
            if (selectionUI == null)
            {
                UIManager ui = GameManager.Instance != null ? GameManager.Instance.UI : null;
                if (ui != null) selectionUI = ui.Screen<TargetSelectionUI>();
            }
        }
        public bool Spawn(GameObject prefab) => Spawn(prefab, selectionUI != null ? selectionUI.GetSavePrefabIndex(prefab) : -1);
        private bool Spawn(GameObject prefab, int prefabSlot)
        {
            if (!isActiveAndEnabled || spawnPoint == null || prefab == null) return false;
            SpawnFailureReason = null;
            if (!AllowsPracticeTargets)
            { SpawnFailureReason = "현재 주문에서 요구하는 사격 테스트만 진행할 수 있습니다."; return false; }
            testController?.Cancel();
            if (IsSpawnBlocked(prefab, spawnPoint)) { SpawnFailureReason = "스폰 공간에 물체가 있습니다."; return false; }
            EnsureSpawnedRoot();
            var instance = Instantiate(prefab,spawnPoint.position,spawnPoint.rotation);
            instance.transform.SetParent(spawnedRoot,true);
            instance.name = prefab.name;
            if (instance.GetComponent<Rigidbody>() == null) instance.AddComponent<Rigidbody>();
            if (instance.GetComponent<FallingRangeTarget>() == null) instance.AddComponent<FallingRangeTarget>();
            instance.GetComponent<FallingRangeTarget>().SetFallMetersPerPower(fallMetersPerPower);
            instance.SetActive(true);
            var target = instance.GetComponent<DestructibleTarget>();
            if (target != null) spawnedPrefabs[target] = prefabSlot;
            return true;
        }

        public bool SpawnCustom(GameObject prefab, Vector3 size, float[] resistances)
        {
            if (prefab == null || resistances == null || resistances.Length != 4) return false;
            // Inactive prototype lets size/resistances be set before target Awake computes its grid.
            var prototype = Instantiate(prefab);
            prototype.SetActive(false);
            prototype.name = prefab.name;
            prototype.GetComponent<DestructibleTarget>().ConfigureCustom(size,
                resistances[0],resistances[1],resistances[2],resistances[3]);
            bool result = Spawn(prototype, -2);
            Destroy(prototype);
            return result;
        }

        public void ClearSpawned()
        {
            testController?.Cancel();
            spawnedPrefabs.Clear();
            if (spawnedRoot == null) return;
            for (int i=spawnedRoot.childCount-1;i>=0;i--)
            {
                var target = spawnedRoot.GetChild(i).gameObject;
                target.SetActive(false);
                Destroy(target);
            }
        }
        private void OnDestroy()
        {
            testController?.Cancel();
            if (spawnedRoot != null) Destroy(spawnedRoot.gameObject);
        }
        public GameObject DefaultTargetPrefab => targetPrefab != null ? targetPrefab.gameObject : null;
        public bool PrepareTest(ShootingTestBinding data, FirstPersonPlayer player)
        {
            SpawnFailureReason = null;
            if (!isActiveAndEnabled || testController == null)
            { SpawnFailureReason = "이 표적 버튼에는 시간제한 테스트가 연결되지 않았습니다."; return false; }
            bool ready = testController.Prepare(data, player, out var error);
            SpawnFailureReason = error;
            return ready;
        }
        private void OnDisable() => testController?.Cancel();
    }
}
