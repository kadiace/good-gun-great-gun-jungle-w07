using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class ShootingTestController : MonoBehaviour
    {
        public enum TestState { Idle, Prepared, Running, Passed, Failed }
        [SerializeField] private TargetBell bell;
        [SerializeField] private Transform layoutPoint;
        [SerializeField, Min(.1f)] private float retryDistance = 3f;
        private ShootingTestBinding definition;
        private GameObject layout;
        private ShootingTestTarget[] targets;
        private DestructibleTarget[] destructibles;
        private FirstPersonPlayer player;
        private PlayerShootingTest playerTest;
        private WeaponHolder holder;
        private EquipableWeapon weapon;
        private string startedAssemblyKey;
        private Transform shotSource;
        private ShootingTestHUD hud;
        private readonly StringBuilder summary = new StringBuilder();
        private float nextSummary;
        public TargetBell Bell => bell;
        public TestState State { get; private set; }
        public ShootingTestBinding Definition => definition;
        public float RemainingSeconds { get; private set; }
        public string StatusText { get; private set; } = "";
        public string ResultReason { get; private set; } = "";
        public int NonForwardMuzzles { get; private set; }
        public bool IsActive => State == TestState.Prepared || State == TestState.Running;
        public bool CanRetryNearby => State == TestState.Failed && player != null && bell != null &&
            Vector3.Distance(player.transform.position, bell.transform.position) <= retryDistance;
        public bool CanRetryWithWeapon => CanRetryNearby && holder != null && holder.EquippedWeapon != null &&
            holder.EquippedWeapon.IsEquipped && !holder.EquippedWeapon.IsMoving;

        private void Awake() { State = TestState.Idle; RemainingSeconds = 0f; }

        public bool Prepare(ShootingTestBinding data, FirstPersonPlayer sourcePlayer, out string error)
        {
            error = null;
            if (IsActive) { error = "진행 중인 테스트를 먼저 종료하세요."; return false; }
            if (data == null || !data.IsAvailable || data.LayoutPrefab == null || layoutPoint == null || sourcePlayer == null || bell == null ||
                data.TimeLimit <= 0f || float.IsNaN(data.TimeLimit) || float.IsInfinity(data.TimeLimit))
            { error = "사격 테스트 설정을 확인하세요."; return false; }
            var tests = TableManager.Instance.ShootingTestTable;
            var unlocks = sourcePlayer.GetComponent<PlayerModuleUnlocks>();
            if (!tests.CanStart(data.Id, unlocks))
            { error = tests.UnlockDescription(data.Id, unlocks); return false; }
            var sourceHolder = sourcePlayer.GetComponent<WeaponHolder>();
            var equipped = sourceHolder != null ? sourceHolder.EquippedWeapon : null;
            if (equipped == null || equipped.IsMoving) { error = "테스트할 총을 손에 들고 시작하세요."; return false; }
            var assembly = equipped.GetComponentInChildren<CubeAssembly>();
            if (assembly == null) { error = "총의 조립 정보를 확인할 수 없습니다."; return false; }
            var paths = assembly.GetProjectilePaths();
            if (paths.Count == 0) { error = "발사 가능한 총구가 없습니다."; return false; }
            string assemblyKey = equipped.TestAssemblyKey;
            if (string.IsNullOrEmpty(assemblyKey)) { error = "총의 구성을 인증할 수 없습니다."; return false; }
            var sourcePlayerTest = sourcePlayer.GetComponent<PlayerShootingTest>();
            var sourceHUD = GameManager.Instance != null && GameManager.Instance.UI != null
                ? GameManager.Instance.UI.Screen<ShootingTestHUD>() : null;
            if (sourcePlayerTest == null || sourceHUD == null)
            { error = "플레이어와 사격 테스트 HUD 연결을 확인하세요."; return false; }
            NonForwardMuzzles = 0;
            foreach (var path in paths) if (path.direction != Vector3Int.forward) NonForwardMuzzles++;

            // Check with the previous transient layout already removed; unrelated practice boxes stay intact.
            DestroyLayout();
            if (bell.IsSpawnBlocked(data.LayoutPrefab, layoutPoint))
            { error = "테스트 배치 공간에 물체가 있습니다. 기존 연습 표적을 청소하세요."; return false; }
            if (hud != null) hud.Unbind(this);
            if (playerTest != null) playerTest.Unbind(this);
            ClearShots();
            UnsubscribeWeapon();
            player = sourcePlayer; holder = sourceHolder; weapon = equipped;
            startedAssemblyKey = assemblyKey;
            shotSource = equipped.GetComponentInChildren<PowerCore>()?.transform;
            definition = data;
            playerTest = sourcePlayerTest;
            hud = sourceHUD;
            if (hud.Controller != null && hud.Controller != this) hud.Controller.Cancel();
            ClearShots();
            // Inactive parent lets all targets remain protected until selection has closed.
            layout = new GameObject("ShootingTestRound");
            layout.AddComponent<ShootingTestLayout>();
            layout.transform.SetParent(layoutPoint, false);
            layout.SetActive(false);
            var stage = Instantiate(data.LayoutPrefab, layout.transform);
            stage.transform.localPosition = Vector3.zero;
            stage.transform.localRotation = Quaternion.identity;
            stage.SetActive(true);
            targets = stage.GetComponentsInChildren<ShootingTestTarget>(true);
            destructibles = stage.GetComponentsInChildren<DestructibleTarget>(true);
            int goals = 0;
            foreach (var target in targets)
            {
                target.Bind(this);
                if (!target.IsProtected && target.gameObject.activeSelf) goals++;
            }
            if (goals == 0) { DestroyLayout(); error = "테스트에 목표 표적이 없습니다."; return false; }
            foreach (var target in destructibles) target.DamageProtected = true;
            layout.SetActive(true);
            RemainingSeconds = data.TimeLimit;
            ResultReason = "";
            State = TestState.Prepared;
            holder.EquippedWeaponChanged += OnEquippedWeaponChanged;
            weapon.AssemblyChanged += OnAssemblyChanged;
            BuildSummary(0, goals);
            playerTest.Bind(this); hud.Bind(this);
            return true;
        }

        public void Begin()
        {
            if (State != TestState.Prepared || layout == null) return;
            OnboardingManager.NotifyShootingTestStarted(this);
            if (!HasOriginalWeapon()) { Finish(false, "테스트 중 총을 내려놓거나 교체했습니다."); return; }
            // 명중 시험은 충돌을 유지하고 모든 파괴·범위 피해를 차단한다.
            foreach (var target in destructibles) target.DamageProtected = definition.CountsHits;
            RemainingSeconds = definition.TimeLimit;
            nextSummary = 0f;
            State = TestState.Running;
        }

        public void Retry()
        {
            if (!CanRetryWithWeapon || definition == null) return;
            var retryData = definition;
            if (Prepare(retryData, player, out var error)) Begin();
            else FeedbackUI.For(this)?.ShowNotification(error);
        }

        internal bool CanCountBulletHit(ShootingTestTarget target, Transform weaponSource)
        {
            return State == TestState.Running && RemainingSeconds > 0f && definition != null && definition.CountsHits &&
                shotSource != null && weaponSource == shotSource && HasOriginalWeapon() && target != null &&
                !target.IsProtected && System.Array.IndexOf(targets, target) >= 0;
        }

        private void LateUpdate()
        {
            if (State != TestState.Running) return;
            if (!HasOriginalWeapon())
            { Finish(false, "테스트 중 총을 내려놓거나 교체했습니다."); return; }
            // Scaled time agrees with projectiles, explosions, and pause. GameplayBlocked alone does not stop physics.
            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Time.deltaTime);
            int complete = 0, goals = 0;
            foreach (var target in targets)
            {
                if (target == null) { Finish(false, "표적이 제거되어 테스트를 종료했습니다."); return; }
                if (target.IsProtected)
                {
                    if (target.ProtectionBroken) { Finish(false, "보호 표적 '" + target.DisplayName + "'이 파괴되었습니다."); return; }
                }
                else
                {
                    goals++;
                    if (definition.CountsHits ? target.HitCount >= definition.RequiredHits :
                        target.Destruction >= definition.RequiredDestruction) complete++;
                }
            }
            if (Time.time >= nextSummary)
            {
                BuildSummary(complete, goals);
                nextSummary = Time.time + .15f;
            }
            if (RemainingSeconds <= 0f) { BuildSummary(complete, goals); Finish(false, "제한 시간을 초과했습니다."); }
            else if (complete == goals) { BuildSummary(complete, goals); Finish(true, "사격 테스트를 통과했습니다."); }
        }

        private void BuildSummary(int complete, int goals)
        {
            summary.Clear();
            summary.Append("목표 ").Append(complete).Append('/').Append(goals);
            foreach (var target in targets)
            {
                if (target == null || target.IsProtected) continue;
                summary.Append('\n').Append(target.DisplayName).Append(' ');
                if (definition.CountsHits)
                    summary.Append("명중 ").Append(target.HitCount).Append('/').Append(definition.RequiredHits);
                else
                    summary.Append(Mathf.RoundToInt(target.Destruction * 100f)).Append("% (기준 ")
                        .Append(Mathf.RoundToInt(definition.RequiredDestruction * 100f)).Append("%)");
            }
            StatusText = summary.ToString();
        }

        private void Finish(bool passed, string reason)
        {
            if (!IsActive) return;
            if (passed && (!HasOriginalWeapon() || !weapon.RecordPassedTest(definition.Id, startedAssemblyKey)))
            { passed = false; reason = "총의 구성이 변경되어 다시 테스트해야 합니다."; }
            State = passed ? TestState.Passed : TestState.Failed;
            UnsubscribeWeapon();
            ResultReason = reason;
            if (!passed) RemainingSeconds = definition.TimeLimit;
            ClearShots(); DestroyLayout();
            FeedbackUI.For(this)?.ShowNotification(reason);
            OnboardingManager.NotifyShootingTestEnded(this, passed);
        }

        private bool HasOriginalWeapon() => holder != null && holder.isActiveAndEnabled && weapon != null &&
            holder.EquippedWeapon == weapon && weapon.IsEquipped && !weapon.IsMoving && weapon.gameObject.activeInHierarchy;
        private void OnEquippedWeaponChanged(EquipableWeapon previous, EquipableWeapon next)
        {
            if (IsActive) Finish(false, "총을 내려놓거나 교체했습니다.");
        }
        private void OnAssemblyChanged()
        {
            if (IsActive) Finish(false, "모듈 구성이 변경되었습니다.");
        }
        private void UnsubscribeWeapon()
        {
            if (holder != null) holder.EquippedWeaponChanged -= OnEquippedWeaponChanged;
            if (weapon != null) weapon.AssemblyChanged -= OnAssemblyChanged;
        }

        private void ClearShots()
        {
            if (shotSource == null) return;
            foreach (var shot in FindObjectsByType<ProjectileMotor>(FindObjectsSortMode.None))
                if (shot.WeaponSource == shotSource) PoolManager.Release(shot.gameObject);
            foreach (var wave in FindObjectsByType<ExpandingExplosion>(FindObjectsSortMode.None))
                if (wave.WeaponSource == shotSource) PoolManager.Release(wave.gameObject);
            foreach (var field in FindObjectsByType<GravityField>(FindObjectsSortMode.None))
                if (field.WeaponSource == shotSource) PoolManager.Release(field.gameObject);
        }

        private void DestroyLayout()
        {
            if (layout != null) { layout.SetActive(false); Destroy(layout); }
            layout = null; targets = null; destructibles = null;
        }

        public void Cancel()
        {
            bool wasActive = IsActive;
            if (wasActive) OnboardingManager.NotifyShootingTestEnded(this, false);
            State = TestState.Idle;
            UnsubscribeWeapon();
            if (wasActive) ClearShots();
            DestroyLayout();
            if (playerTest != null) playerTest.Unbind(this);
            if (hud != null) hud.Unbind(this);
            definition = null; weapon = null; shotSource = null; player = null; holder = null; startedAssemblyKey = null;
            RemainingSeconds = 0f; NonForwardMuzzles = 0; StatusText = "";
            ResultReason = "";
        }
        private void OnDisable() => Cancel();
    }
}
