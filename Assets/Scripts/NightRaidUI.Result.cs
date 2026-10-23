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
        private readonly System.Collections.Generic.List<UnityEngine.UI.Image> resultModuleIcons = new System.Collections.Generic.List<UnityEngine.UI.Image>();
        private RectTransform resultIconRow;
        private HoveringUI resultModuleTooltip;
        private Vector2 resultBodyPosition;
        private Vector2 resultBodySize;
        private void InitializeResult()
        {
            if (resultBackdrop != null) { resultBackdrop.alpha = 0f; resultBackdrop.gameObject.SetActive(false); }
            if (resultContent != null) resultContent.alpha = 0f;
            if (resultIconRow != null) resultIconRow.gameObject.SetActive(false);
            resultModuleTooltip?.ClearResultModule();
            if (resultBody != null)
            {
                var rect = resultBody.rectTransform;
                resultBodyPosition = rect.anchoredPosition;
                resultBodySize = rect.sizeDelta;
            }
            if (resultConfirm != null) resultConfirm.onClick.AddListener(ConfirmResult);
        }
        public void ShowRaidResult(bool died, string summary, System.Collections.Generic.IReadOnlyList<int> modules, Action confirmed)
        {
            ShowResult(died, died ? "당신은 죽었습니다" : "귀환 성공",
                died ? new Color(1f,.22f,.18f) : new Color(.2f,1f,.45f),
                died ? "이번 습격에서 획득한 모든 재화는 가져갈 수 없습니다." : summary, confirmed);
            ShowResultModules(died ? null : modules);
        }
        private void ShowResultModules(System.Collections.Generic.IReadOnlyList<int> modules)
        {
            int count = modules != null ? modules.Count : 0;
            var bodyRect = resultBody.rectTransform;
            bodyRect.anchoredPosition = count > 0 ? resultBodyPosition + new Vector2(0f, 48f) : resultBodyPosition;
            bodyRect.sizeDelta = count > 0 ? new Vector2(resultBodySize.x, resultBodySize.y - 96f) : resultBodySize;
            if (count == 0)
            {
                if (resultIconRow != null) resultIconRow.gameObject.SetActive(false);
                resultModuleTooltip?.ClearResultModule();
                return;
            }
            if (resultIconRow == null)
            {
                var row = new GameObject("ResultModuleIcons", typeof(RectTransform));
                resultIconRow = (RectTransform)row.transform;
                resultIconRow.SetParent(resultContent.transform, false);
                resultIconRow.anchorMin = resultIconRow.anchorMax = new Vector2(.5f, .5f);
                resultIconRow.anchoredPosition = new Vector2(0f, -110f);
                resultIconRow.sizeDelta = new Vector2(660f, 64f);
            }
            var icons = GetComponentInParent<WorkshopTagsUI>(true);
            if (icons == null) icons = transform.root.GetComponentInChildren<WorkshopTagsUI>(true);
            if (resultModuleTooltip == null) resultModuleTooltip = GetComponentInParent<HoveringUI>(true);
            if (resultModuleTooltip == null) resultModuleTooltip = transform.root.GetComponentInChildren<HoveringUI>(true);
            while (resultModuleIcons.Count < count)
            {
                var image = new GameObject("ModuleIcon", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
                image.transform.SetParent(resultIconRow, false);
                image.raycastTarget = true;
                image.preserveAspect = true;
                image.gameObject.AddComponent<ResultModuleIconHover>();
                resultModuleIcons.Add(image);
            }
            float spacing = Mathf.Min(72f, 650f / count);
            float size = Mathf.Min(60f, spacing - 8f);
            for (int i = 0; i < resultModuleIcons.Count; i++)
            {
                var image = resultModuleIcons[i];
                image.gameObject.SetActive(i < count);
                if (i >= count) continue;
                image.sprite = icons != null ? icons.GetRaidModuleIcon(modules[i]) : null;
                image.color = Color.white;
                ModuleData data = moduleCatalog != null && moduleCatalog.TryGet(modules[i], out var entry) ? entry.data : null;
                image.GetComponent<ResultModuleIconHover>().Bind(resultModuleTooltip, data);
                var rect = (RectTransform)image.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2((i - (count - 1) * .5f) * spacing, 0f);
                rect.sizeDelta = new Vector2(size, size);
            }
            resultIconRow.gameObject.SetActive(true);
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
            resultBody.rectTransform.anchoredPosition = resultBodyPosition;
            resultBody.rectTransform.sizeDelta = resultBodySize;
            if (resultIconRow != null) resultIconRow.gameObject.SetActive(false);
            resultModuleTooltip?.ClearResultModule();
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
            resultModuleTooltip?.ClearResultModule();
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

    public sealed class ResultModuleIconHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerMoveHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        private HoveringUI tooltip;
        private ModuleData module;
        public void Bind(HoveringUI target, ModuleData data) { tooltip = target; module = data; }
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData) => tooltip?.ShowResultModule(module, eventData.position);
        public void OnPointerMove(UnityEngine.EventSystems.PointerEventData eventData) => tooltip?.ShowResultModule(module, eventData.position);
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) => tooltip?.ClearResultModule();
        private void OnDisable() => tooltip?.ClearResultModule();
    }
}
