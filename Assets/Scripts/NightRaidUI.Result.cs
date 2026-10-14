using System;
using System.Collections;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed partial class NightRaidUI
    {
        [SerializeField] private CanvasGroup resultBackdrop;
        [SerializeField] private CanvasGroup resultContent;
        [SerializeField] private TMPro.TextMeshProUGUI resultTitle;
        [SerializeField] private TMPro.TextMeshProUGUI resultBody;
        [SerializeField] private UnityEngine.UI.Button resultConfirm;
        private Coroutine resultRoutine;
        private Action resultDone;
        private void InitializeResult()
        {
            if (resultBackdrop != null) { resultBackdrop.alpha = 0f; resultBackdrop.gameObject.SetActive(false); }
            if (resultContent != null) resultContent.alpha = 0f;
            if (resultConfirm != null) resultConfirm.onClick.AddListener(ConfirmResult);
        }
        public void ShowRaidResult(bool died, string summary, Action confirmed)
        {
            ShowResult(died, died ? "당신은 죽었습니다" : "귀환 성공",
                died ? new Color(1f,.22f,.18f) : new Color(.2f,1f,.45f),
                died ? "이번 습격에서 획득한 모든 재화는 가져갈 수 없습니다." : summary, confirmed);
        }
        public void ShowBankruptcyResult(Action confirmed)
        {
            ShowResult(true, "가게를 내놓았습니다", new Color(1f,.22f,.18f),
                "자릿세를 내지 못해 가게를 내놓게 되었습니다.", confirmed);
        }
        private void ShowResult(bool failure, string title, Color titleColor, string body, Action confirmed)
        {
            Initialize();
            if (failure) SetExtraction(false, 0f, 0f);
            if (resultBackdrop == null) { confirmed?.Invoke(); return; }
            if (resultRoutine != null)
            {
                if (!failure) return;
                StopCoroutine(resultRoutine);
                resultRoutine = null;
            }
            resultDone = confirmed;
            resultContent.alpha = 0f;
            GameManager.Instance?.SetDialogue(true);
            resultTitle.text = title;
            resultTitle.color = titleColor;
            resultBody.text = body;
            resultBody.color = Color.white;
            resultConfirm.interactable = false;
            resultBackdrop.gameObject.SetActive(true);
            transform.SetAsLastSibling();
            resultBackdrop.transform.SetAsLastSibling();
            resultRoutine = StartCoroutine(RevealResult());
        }
        private IEnumerator RevealResult()
        {
            yield return FadeResult(resultBackdrop, 0f, 1f);
            SetExtraction(false, 0f, 0f);
            yield return FadeResult(resultContent, 0f, 1f);
            resultConfirm.interactable = true; resultRoutine = null;
        }
        private void ConfirmResult()
        {
            if (resultRoutine != null || resultDone == null) return;
            resultConfirm.interactable = false;
            resultRoutine = StartCoroutine(LeaveResult());
        }
        private IEnumerator LeaveResult()
        {
            yield return FadeResult(resultContent, 1f, 0f);
            var done = resultDone; resultDone = null; resultRoutine = null;
            done?.Invoke();
        }
        private static IEnumerator FadeResult(CanvasGroup group, float from, float to)
        {
            for (float time = 0; time < .45f; time += Time.unscaledDeltaTime)
            { group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, time / .45f)); yield return null; }
            group.alpha = to;
        }
    }
}
