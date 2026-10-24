using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class OnboardingManager : MonoBehaviour
    {
        private const float DialogueInputLockSeconds = 0.5f;

        private sealed class Prompt
        {
            public string Id;
            public string Description;
            public int ModuleId;
            public CubeBuilder Builder;
            public bool IsCustom;
            public bool IsStroke;
        }

        private static OnboardingManager instance;
        private static readonly HashSet<int> shownModules = new HashSet<int>();
        private static bool customStarted;
        private readonly Queue<Prompt> waiting = new Queue<Prompt>();
        private TutorialSequence runningSequence;
        private Prompt current;
        private WorkshopSession session;

        public static bool IsChoosingModuleDirection(CubeBuilder builder) =>
            instance != null && instance.current != null && !instance.current.IsCustom &&
            !instance.current.IsStroke && instance.current.Builder == builder &&
            DialogueUI.Instance != null && DialogueUI.Instance.IsOpen;

        public static void NotifyModulePlacementAttempt(int moduleId, CubeBuilder builder)
            => QueueModulePlacementAttempt(moduleId, builder, false);

        public static bool NotifyModuleStrokeAttempt(int moduleId, CubeBuilder builder)
            => QueueModulePlacementAttempt(moduleId, builder, true);

        private static bool QueueModulePlacementAttempt(int moduleId, CubeBuilder builder, bool stroke)
        {
            if (!Application.isPlaying || builder == null || shownModules.Contains(moduleId)) return false;
            var tutorial = TutorialManager.Instance;
            if (tutorial == null || tutorial.IsTutorialActive) return false;
            foreach (var module in TableManager.Instance.Onboarding.modules)
            {
                if (module == null || module.moduleId != moduleId) continue;
                shownModules.Add(moduleId);
                EnsureInstance().waiting.Enqueue(new Prompt
                {
                    Id = "module-" + moduleId,
                    Description = module.title + "\n" + module.description,
                    ModuleId = moduleId,
                    Builder = builder,
                    IsStroke = stroke
                });
                return true;
            }
            return false;
        }

        public static void NotifyCustomOrder()
        {
            if (!Application.isPlaying || customStarted) return;
            EnsureInstance();
        }

        private static bool HasCurrentCustomOrder()
        {
            var bell = FindFirstObjectByType<Bell>();
            var customer = bell != null ? bell.Current : null;
            if (customer == null || !customer.HasActiveOrder || customer.Profile == null) return false;

            var onboarding = TableManager.Instance.Onboarding;
            var requests = TableManager.Instance.CustomerRequest;
            foreach (string requestId in customer.Profile.RequestIds)
            {
                string canonical = requests.FindRequest(requestId)?.id;
                if (System.Array.IndexOf(onboarding.customRequestIds, canonical) >= 0) return true;
            }
            return false;
        }

        public static void NotifyCustomOpened()
        {
            if (!Application.isPlaying) return;
            if (instance != null) instance.RemovePrompts(true);
            customStarted = true;
        }

        public static void ResetModuleOnboarding()
        {
            shownModules.Clear();
            if (instance != null) instance.RemovePrompts(false);
        }

        public static void ResetCustomOnboarding()
        {
            customStarted = false;
            if (instance != null) instance.RemovePrompts(true);
        }

        private static OnboardingManager EnsureInstance()
        {
            if (instance == null) instance = new GameObject(nameof(OnboardingManager)).AddComponent<OnboardingManager>();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            var player = GameManager.Instance != null && GameManager.Instance.UI != null
                ? GameManager.Instance.UI.Player : null;
            var nextSession = player != null ? player.GetComponent<WorkshopSession>() : null;
            if (session != nextSession)
            {
                if (session != null) session.WorkbenchEntered -= OnWorkbenchEntered;
                session = nextSession;
                if (session != null) session.WorkbenchEntered += OnWorkbenchEntered;
            }

            if (current != null || waiting.Count == 0 || TutorialManager.Instance == null ||
                TutorialManager.Instance.IsTutorialActive || GameManager.GameplayBlocked) return;
            if (waiting.Peek().IsCustom)
            {
                if (!HasCurrentCustomOrder())
                {
                    RemovePrompts(true);
                    customStarted = false;
                    return;
                }
                if (session == null || !session.IsEditing) return;
            }
            StartNext();
        }

        private void OnWorkbenchEntered()
        {
            if (customStarted || !HasCurrentCustomOrder()) return;
            var steps = new List<OnboardingData.CustomStep>();
            foreach (var step in TableManager.Instance.Onboarding.customSteps)
                if (step != null) steps.Add(step);
            if (steps.Count == 0) return;
            steps.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) :
                string.CompareOrdinal(a.id, b.id));
            customStarted = true;
            foreach (var step in steps)
                waiting.Enqueue(new Prompt
                {
                    Id = step.id,
                    Description = step.title + "\n" + step.description,
                    IsCustom = true
                });
        }

        private void StartNext()
        {
            current = waiting.Dequeue();
            runningSequence = ScriptableObject.CreateInstance<TutorialSequence>();
            runningSequence.name = "Onboarding_" + current.Id;
            runningSequence.steps = new[] { new TutorialStep
            {
                id = current.Id,
                title = "튜토리얼",
                description = current.Description,
                completion = TutorialCompletion.Confirmation,
                confirmOnBackgroundClick = true,
                dialogueInputLockSeconds = DialogueInputLockSeconds,
                buttonGuide = CustomButtonGuide(current.Id)
            } };
            if (current.IsCustom)
            {
                var palette = FindPalette();
                if (palette != null) palette.ShowCustomOnboardingStep(current.Id);
            }
            TutorialManager.Run(runningSequence, this, completed: CompleteCurrent);
        }

        private static ModulePaletteUI FindPalette()
        {
            return GameManager.Instance != null && GameManager.Instance.UI != null
                ? GameManager.Instance.UI.GetComponentInChildren<ModulePaletteUI>(true) : null;
        }

        private static TutorialButtonGuide CustomButtonGuide(string stepId)
        {
            switch (stepId)
            {
                case "button": return TutorialButtonGuide.CustomButton;
                case "paint": return TutorialButtonGuide.CustomPaint;
                case "sticker": return TutorialButtonGuide.CustomSticker;
                case "keyring": return TutorialButtonGuide.CustomKeyring;
                case "removal": return TutorialButtonGuide.CustomRemove;
                default: return TutorialButtonGuide.None;
            }
        }

        private void CompleteCurrent(bool succeeded)
        {
            var completed = current;
            current = null;
            if (runningSequence != null) Destroy(runningSequence);
            runningSequence = null;
            if (completed == null) return;
            if (!succeeded)
            {
                if (completed.IsCustom)
                {
                    customStarted = false;
                    RemovePrompts(true);
                }
                else
                {
                    shownModules.Remove(completed.ModuleId);
                    if (completed.IsStroke && completed.Builder != null) completed.Builder.CancelOnboardingStroke();
                }
                return;
            }
            if (completed.IsCustom)
            {
                var palette = FindPalette();
                if (palette == null || !palette.TryPerformCustomOnboardingAction(completed.Id))
                    Debug.LogWarning("커스텀 온보딩 동작을 완료하지 못했습니다: " + completed.Id, this);
            }
            else if (completed.Builder != null &&
                     !(completed.IsStroke ? completed.Builder.CompleteOnboardingStroke(completed.ModuleId) :
                         completed.Builder.CompleteOnboardingPlacement(completed.ModuleId)))
                Debug.LogWarning("모듈 온보딩 방향을 자동 확정하지 못했습니다: " + completed.ModuleId, this);
        }

        private void RemovePrompts(bool custom)
        {
            if (current != null && current.IsCustom == custom)
            {
                if (current.IsStroke && current.Builder != null) current.Builder.CancelOnboardingStroke();
                TutorialManager.Instance?.Cancel(this);
                if (runningSequence != null) Destroy(runningSequence);
                runningSequence = null;
                current = null;
            }
            int count = waiting.Count;
            for (int i = 0; i < count; i++)
            {
                var prompt = waiting.Dequeue();
                if (prompt.IsCustom != custom) waiting.Enqueue(prompt);
                else if (prompt.IsStroke && prompt.Builder != null) prompt.Builder.CancelOnboardingStroke();
            }
        }

        private void OnDestroy()
        {
            if (session != null) session.WorkbenchEntered -= OnWorkbenchEntered;
            if (current != null && current.IsStroke && current.Builder != null) current.Builder.CancelOnboardingStroke();
            foreach (var prompt in waiting)
                if (prompt.IsStroke && prompt.Builder != null) prompt.Builder.CancelOnboardingStroke();
            TutorialManager.Instance?.Cancel(this);
            if (runningSequence != null) Destroy(runningSequence);
            if (instance == this) instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            instance = null;
            shownModules.Clear();
            customStarted = false;
        }
    }
}
