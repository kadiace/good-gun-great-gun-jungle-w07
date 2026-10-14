using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    public sealed class GoodShotUI : MonoBehaviour
    {
        [SerializeField] private CustomerOutcomeTracker outcomeTracker;
        [SerializeField] private Text messageText;
        [SerializeField, Min(0.1f)] private float displayDuration = 1.5f;
        private Coroutine display;

        private void Awake() => messageText?.gameObject.SetActive(false);

        private void OnEnable()
        {
            if (outcomeTracker != null) outcomeTracker.CustomerKilled += Show;
        }

        private void OnDisable()
        {
            if (outcomeTracker != null) outcomeTracker.CustomerKilled -= Show;
            if (display != null) StopCoroutine(display);
            display = null;
            messageText?.gameObject.SetActive(false);
        }

        private void Show()
        {
            if (messageText == null) return;
            if (display != null) StopCoroutine(display);
            messageText.text = "굿 샷";
            messageText.gameObject.SetActive(true);
            display = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, displayDuration));
            messageText.gameObject.SetActive(false);
            display = null;
        }
    }
}
