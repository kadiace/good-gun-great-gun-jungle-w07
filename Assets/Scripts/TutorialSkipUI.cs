using System;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class TutorialSkipUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private UnityEngine.UI.Button skipButton, tutorialButton;
        private Action skip, begin;
        private bool ownsInput;
        private bool transitioning;
        private DayTransitionUI dayTransition;
        public bool IsOpen => panel != null && panel.activeSelf;
        public RectTransform TutorialStartButton => tutorialButton != null && tutorialButton.interactable ? tutorialButton.transform as RectTransform : null;
        public RectTransform TutorialSkipButton => skipButton != null && skipButton.interactable ? skipButton.transform as RectTransform : null;
        private void Awake()
        {
            panel.SetActive(false); ownsInput = false;
            dayTransition = GetComponent<DayTransitionUI>();
            skipButton.onClick.AddListener(() => Complete(true));
            tutorialButton.onClick.AddListener(() => Complete(false));
        }
        public bool Show(Action onSkip, Action onTutorial)
        {
            if (!isActiveAndEnabled || IsOpen) return false;
            skip = onSkip; begin = onTutorial;
            transitioning = false; skipButton.interactable = tutorialButton.interactable = true;
            panel.SetActive(true); panel.transform.SetAsLastSibling();
            ownsInput = GameManager.Instance != null;
            if (ownsInput) GameManager.Instance.SetDialogue(true);
            return true;
        }
        private void Complete(bool skipped)
        {
            if (!IsOpen || transitioning) return;
            transitioning = true; skipButton.interactable = tutorialButton.interactable = false;
            var callback = skipped ? skip : begin;
            if (dayTransition == null) { Close(); callback?.Invoke(); return; }
            if (skipped)
            {
                dayTransition.Covered += HideCoveredSelection;
                callback?.Invoke();
            }
            else if (!dayTransition.BeginTutorial(() => { Close(); callback?.Invoke(); }))
            { transitioning = false; skipButton.interactable = tutorialButton.interactable = true; }
        }
        private void HideCoveredSelection()
        {
            dayTransition.Covered -= HideCoveredSelection;
            ownsInput = false;
            Close();
        }
        private void Close()
        {
            panel.SetActive(false); skip = begin = null;
            if (ownsInput && GameManager.Instance != null) GameManager.Instance.SetDialogue(false);
            ownsInput = false;
        }
        private void OnDisable() => Close();
    }
}
