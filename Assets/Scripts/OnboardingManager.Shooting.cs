using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop
{
    // 사격 목표는 수동 튜토리얼을 점유하지 않는다. 짧은 모듈/커스텀 안내가 끝나면 같은 목표를 다시 표시한다.
    public sealed partial class OnboardingManager
    {
        private enum ShootingPhase { None, Introduction, Preparation, Selection, Running, Result }

        private static bool shootingGuideCompleted;
        private ShootingPhase shootingPhase;
        private Customer shootingCustomer;
        private Workbench shootingWorkbench;
        private TargetBell shootingBell;
        private ShootingTestController shootingController;
        private string shootingTestId;
        private int shootingScene;
        private string shootingObjective;
        private Component shootingTarget;
        private string shootingTargetLabel;
        private RectTransform shootingTestButton;
        private float nextShootingWeaponCheck;

        private OnboardingData.ShootingGuide ShootingSettings => TableManager.Instance.Onboarding.shootingGuide;
        private bool ShootingCountsHits => TableManager.Instance.ShootingTestTable.Find(shootingTestId).CountsHits();
        private string ShootingPrepareObjective => ShootingCountsHits
            ? ShootingSettings.hitCountPrepareObjective : ShootingSettings.prepareObjective;

        public static bool IsShootingGuideActive => instance != null &&
            instance.shootingPhase != ShootingPhase.None && instance.shootingCustomer != null &&
            instance.shootingCustomer.isActiveAndEnabled && instance.shootingCustomer.HasActiveOrder;

        public static bool IsShootingGuideVisible => IsShootingGuideActive &&
            instance.shootingPhase != ShootingPhase.Introduction && instance.shootingPhase != ShootingPhase.Result &&
            instance.current == null && instance.waiting.Count == 0 &&
            (TutorialManager.Instance == null || !TutorialManager.Instance.IsTutorialActive);

        public static string ShootingObjective => IsShootingGuideVisible ? instance.shootingObjective : null;
        public static Component ShootingTarget => IsShootingGuideVisible ? instance.shootingTarget : null;
        public static string ShootingTargetLabel => IsShootingGuideVisible ? instance.shootingTargetLabel : null;
        public static RectTransform ShootingTestButton => IsShootingGuideVisible ? instance.shootingTestButton : null;

        public static void NotifyShootingOrder(Customer customer)
        {
            if (!Application.isPlaying || shootingGuideCompleted || customer == null ||
                !customer.HasTalked || customer.Profile == null ||
                TutorialManager.Instance == null || TutorialManager.Instance.IsGuidedDayZero) return;
            if (instance != null && instance.shootingPhase != ShootingPhase.None) return;

            var settings = TableManager.Instance.Onboarding.shootingGuide;
            if (settings == null || settings.minimumPenetration <= 0f) return;
            string chosen = null;
            foreach (string requestId in customer.Profile.RequestIds)
            {
                // 주문에 숨겨진 추가 요구만으로는 안내하지 않는다. 질문/달성으로 공개된 요구만 대상이다.
                if (!customer.IsRequestVisible(requestId)) continue;
                var request = TableManager.Instance.CustomerRequest.FindRequest(requestId);
                foreach (string id in request?.requiredTestIds ?? Array.Empty<string>())
                {
                    if (Array.IndexOf(settings.testIds ?? Array.Empty<string>(), id) < 0 ||
                        !TableManager.Instance.ShootingTestTable.IsAvailable(id)) continue;
                    // 배열 순서가 아닌 명시된 우선 ID로 동시 요구를 처리한다.
                    if (chosen == null || id == settings.preferredTestId ||
                        chosen != settings.preferredTestId && string.CompareOrdinal(id, chosen) < 0) chosen = id;
                }
            }
            if (chosen == null) return;
            var workbench = TutorialManager.Instance.TutorialWorkbench;
            var testBell = FindFirstObjectByType<TargetBell>();
            if (workbench == null || testBell == null)
            {
                Debug.LogWarning("사격 온보딩을 시작할 작업대·테스트 장치 연결이 없습니다.", customer);
                return;
            }
            var manager = EnsureInstance();
            manager.shootingCustomer = customer;
            manager.shootingScene = customer.gameObject.scene.handle;
            manager.shootingTestId = chosen;
            manager.shootingWorkbench = workbench;
            manager.shootingBell = testBell;
            manager.shootingPhase = ShootingPhase.Introduction;
            manager.QueueShootingPrompt(false, manager.ShootingCountsHits ? settings.hitCountIntroduction : settings.introduction);
        }

        private string ShootingText(string template)
        {
            var test = TableManager.Instance.ShootingTestTable.Find(shootingTestId);
            return string.Format(template ?? "", test?.name ?? "", ShootingSettings.minimumPenetration, test?.instructions ?? "");
        }

        private void QueueShootingPrompt(bool result, string text)
        {
            shootingObjective = null;
            shootingTarget = null;
            shootingTestButton = null;
            waiting.Enqueue(new Prompt
            {
                Id = result ? "shooting-result" : "shooting-introduction",
                Description = ShootingText(text),
                IsShooting = true,
                IsShootingResult = result
            });
        }

        private void PrioritizeShortPrompts()
        {
            if (waiting.Count < 2 || !waiting.Peek().IsShooting) return;
            var prompts = waiting.ToArray();
            waiting.Clear();
            foreach (var prompt in prompts) if (!prompt.IsShooting) waiting.Enqueue(prompt);
            foreach (var prompt in prompts) if (prompt.IsShooting) waiting.Enqueue(prompt);
        }

        private void CompleteShootingPrompt(bool result, bool succeeded)
        {
            if (shootingPhase == ShootingPhase.None) return;
            if (!succeeded) { AbortShootingGuide(); return; }
            if (result)
            {
                shootingGuideCompleted = true;
                AbortShootingGuide();
                return;
            }
            shootingPhase = ShootingPhase.Preparation;
            nextShootingWeaponCheck = 0f;
        }

        private void UpdateShootingGuide(FirstPersonPlayer player)
        {
            if (shootingPhase == ShootingPhase.None) return;
            if (shootingCustomer == null || !shootingCustomer.isActiveAndEnabled || !shootingCustomer.HasActiveOrder ||
                !shootingCustomer.gameObject.scene.isLoaded || shootingCustomer.gameObject.scene.handle != shootingScene)
            {
                AbortShootingGuide();
                return;
            }
            if (shootingPhase != ShootingPhase.Preparation || !IsShootingGuideVisible ||
                Time.unscaledTime < nextShootingWeaponCheck) return;
            nextShootingWeaponCheck = Time.unscaledTime + .1f;
            if (player == null) return;

            var holder = player.GetComponent<WeaponHolder>();
            var held = holder != null ? holder.EquippedWeapon : null;
            var workbench = session != null && session.ActiveWorkbench != null ? session.ActiveWorkbench : shootingWorkbench;
            var placed = workbench != null ? workbench.CurrentWeapon : null;
            if ((session == null || !session.IsInWorkshop) && held != null && held.IsEquipped && HasShootingWeapon(held))
                SetShootingGoal(ShootingSettings.openObjective, shootingBell, "테스트 장치");
            else if (HasShootingWeapon(placed))
                SetShootingGoal(ShootingSettings.equipObjective, placed, "테스트할 총");
            else
                SetShootingGoal(ShootingPrepareObjective, workbench, "작업대");
        }

        private void SetShootingGoal(string text, Component target, string label)
        {
            shootingObjective = ShootingText(text);
            shootingTarget = target;
            shootingTargetLabel = label;
        }

        private bool HasShootingWeapon(EquipableWeapon weapon)
        {
            if (weapon == null || weapon.IsMoving) return false;
            var assembly = weapon.GetComponentInChildren<CubeAssembly>();
            var core = assembly != null ? assembly.GetComponent<PowerCore>() : null;
            if (core == null) return false;
            if (ShootingCountsHits) return assembly.GetProjectilePaths().Count > 0;
            foreach (var path in assembly.GetProjectilePaths())
                if (core.GetMuzzleStats(path.path).penetration >= ShootingSettings.minimumPenetration) return true;
            return false;
        }

        public static bool AllowsShootingGuideTest(TargetBell bell, string id) =>
            !IsShootingGuideActive || instance.shootingBell != bell || instance.shootingTestId == id;

        public static bool CanPrepareShootingGuideTest(TargetBell bell, string id, FirstPersonPlayer player, out string error)
        {
            error = null;
            if (!IsShootingGuideActive || instance.shootingBell != bell) return true;
            if (id != instance.shootingTestId)
            {
                error = instance.ShootingText(instance.ShootingSettings.selectObjective);
                return false;
            }
            var holder = player != null ? player.GetComponent<WeaponHolder>() : null;
            if (holder != null && holder.EquippedWeapon != null && holder.EquippedWeapon.IsEquipped &&
                instance.HasShootingWeapon(holder.EquippedWeapon)) return true;
            error = instance.ShootingText(instance.ShootingPrepareObjective);
            return false;
        }

        public static void NotifyShootingSelectionOpened(TargetBell bell, RectTransform button)
        {
            if (!IsShootingGuideActive || instance.shootingBell != bell ||
                instance.shootingPhase != ShootingPhase.Preparation) return;
            instance.shootingPhase = ShootingPhase.Selection;
            instance.shootingTestButton = button;
            instance.SetShootingGoal(instance.ShootingSettings.selectObjective, null, null);
        }

        public static void NotifyShootingSelectionClosed(TargetBell bell)
        {
            if (IsShootingGuideActive && instance.shootingBell == bell &&
                instance.shootingPhase == ShootingPhase.Selection) instance.FinishShootingGuide(false);
        }

        public static void NotifyShootingTestStarted(ShootingTestController controller)
        {
            if (!IsShootingGuideActive || controller == null || controller.Bell != instance.shootingBell ||
                controller.Definition?.Id != instance.shootingTestId ||
                instance.shootingPhase == ShootingPhase.Introduction || instance.shootingPhase == ShootingPhase.Result) return;
            instance.shootingController = controller;
            instance.shootingPhase = ShootingPhase.Running;
            instance.shootingTestButton = null;
            instance.SetShootingGoal(instance.ShootingSettings.fireObjective, null, null);
        }

        public static void NotifyShootingTestEnded(ShootingTestController controller, bool passed)
        {
            if (IsShootingGuideActive && instance.shootingPhase == ShootingPhase.Running &&
                instance.shootingController == controller) instance.FinishShootingGuide(passed);
        }

        private void FinishShootingGuide(bool passed)
        {
            shootingPhase = ShootingPhase.Result;
            QueueShootingPrompt(true, passed ? ShootingSettings.passed : ShootingSettings.failed);
        }

        private void AbortShootingGuide()
        {
            shootingPhase = ShootingPhase.None;
            shootingCustomer = null;
            shootingWorkbench = null;
            shootingBell = null;
            shootingController = null;
            shootingTestId = null;
            shootingObjective = null;
            shootingTarget = null;
            shootingTargetLabel = null;
            shootingTestButton = null;
            nextShootingWeaponCheck = 0f;
            int count = waiting.Count;
            for (int i = 0; i < count; i++)
            {
                var prompt = waiting.Dequeue();
                if (!prompt.IsShooting) waiting.Enqueue(prompt);
            }
            if (current != null && current.IsShooting) TutorialManager.Instance?.Cancel(this);
        }

        private void OnEnable() => SceneManager.activeSceneChanged += OnShootingSceneChanged;
        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnShootingSceneChanged;
            AbortShootingGuide();
        }
        private void OnShootingSceneChanged(Scene previous, Scene next) => AbortShootingGuide();

        public static void ResetShootingOnboarding()
        {
            shootingGuideCompleted = false;
            if (instance != null) instance.AbortShootingGuide();
            var bell = FindFirstObjectByType<Bell>();
            if (bell != null && bell.Current != null && bell.Current.HasActiveOrder) NotifyShootingOrder(bell.Current);
        }
    }
}
