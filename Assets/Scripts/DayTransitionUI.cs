using System.Collections;
using UnityEngine;
using TMPro;

namespace WeaponWorkshop
{
    public sealed class DayTransitionUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup fade;
        [SerializeField] private TMP_Text dayText;
        [SerializeField] private CanvasGroup titleFade;
        public event System.Action Covered;
        public event System.Action Completed;
        [SerializeField, Min(.01f)] private float fadeDuration = .45f;
        [SerializeField, Min(0f)] private float titleDuration = 1f;
        private Coroutine transition;
        public bool IsTransitioning => transition != null;
        private bool ownsInput;
        private void Awake() { fade.alpha = 0f; if (titleFade != null) titleFade.alpha = 0f; panel.SetActive(false); dayText.gameObject.SetActive(false); }
        private void OnDisable()
        {
            if (transition != null) StopCoroutine(transition);
            transition = null;
            if (panel != null) panel.SetActive(false);
            if (ownsInput && GameManager.Instance != null) GameManager.Instance.SetDialogue(false);
            ownsInput = false;
        }
        public bool Begin(_DayManager day)
        {
            if (transition != null || day == null || !day.IsNight || !day.CanStartNextDay) return false;
            ownsInput = true;
            GameManager.Instance.SetDialogue(true);
            panel.SetActive(true); panel.transform.SetAsLastSibling();
            dayText.gameObject.SetActive(false);
            transition = StartCoroutine(Advance(day));
            return true;
        }
        public bool BeginFirstDay(_DayManager day)
        {
            if (transition != null || day == null || day.DayNumber != 0) return false;
            ownsInput = true;
            GameManager.Instance?.SetDialogue(true);
            panel.SetActive(true); panel.transform.SetAsLastSibling();
            dayText.gameObject.SetActive(false);
            transition = StartCoroutine(Advance(day, true));
            return true;
        }
        public bool BeginReturn(_DayManager day)
        {
            if (transition != null || day == null || !day.IsNight || !day.CanStartNextDay) return false;
            ownsInput = true; GameManager.Instance?.SetDialogue(true);
            panel.SetActive(true); panel.transform.SetAsLastSibling(); fade.alpha = 1f;
            dayText.gameObject.SetActive(false);
            transition = StartCoroutine(Advance(day, false, true));
            return true;
        }
        private IEnumerator Advance(_DayManager day, bool firstDay = false, bool alreadyCovered = false)
        {
            if (!alreadyCovered) yield return Fade(0f, 1f);
            Covered?.Invoke();
            if (firstDay) day.BeginFirstBusinessDay(); else day.StartNextDay();
            if (GameManager.Instance != null) GameManager.Instance.SetDialogue(true);
            dayText.text = "Day " + day.DayNumber;
            dayText.gameObject.SetActive(true);
            yield return FadeTitle(0f, 1f);
            yield return new WaitForSecondsRealtime(titleDuration);
            yield return FadeTitle(1f, 0f);
            dayText.gameObject.SetActive(false);
            yield return Fade(1f, 0f);
            panel.SetActive(false);
            transition = null; ownsInput = false;
            if (GameManager.Instance != null) GameManager.Instance.SetDialogue(false);
            Completed?.Invoke();
        }
        public bool BeginTutorial(System.Action whenCovered)
        {
            if (transition != null) return false;
            ownsInput = true;
            GameManager.Instance?.SetDialogue(true);
            panel.SetActive(true); panel.transform.SetAsLastSibling();
            dayText.gameObject.SetActive(false);
            transition = StartCoroutine(RevealTutorial(whenCovered));
            return true;
        }
        private IEnumerator RevealTutorial(System.Action whenCovered)
        {
            yield return Fade(0f, 1f);
            whenCovered?.Invoke();
            GameManager.Instance?.SetDialogue(true);
            yield return Fade(1f, 0f);
            panel.SetActive(false); transition = null; ownsInput = false;
            if (GameManager.Instance != null) GameManager.Instance.SetDialogue(DialogueUI.Instance != null && DialogueUI.Instance.IsOpen);
        }
        private IEnumerator FadeTitle(float from, float to)
        {
            if (titleFade == null) yield break;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                titleFade.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeDuration)));
                yield return null;
            }
            titleFade.alpha = to;
        }
        private IEnumerator Fade(float from, float to)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                fade.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeDuration)));
                yield return null;
            }
            fade.alpha = to;
        }
    }
}
