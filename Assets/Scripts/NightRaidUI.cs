using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    public sealed partial class NightRaidUI : MonoBehaviour
    {
        [SerializeField] private GameObject mapPanel;
        [SerializeField] private GameObject returnPanel;
        [SerializeField] private Button[] regionButtons;
        [SerializeField] private Button backButton;
        [SerializeField] private Button returnButton;
        [SerializeField] private Button stayButton;
        [SerializeField] private TMPro.TextMeshProUGUI statusText;
        private NightRaidController controller;
        [SerializeField] private TMPro.TextMeshProUGUI regionInfo;
        [SerializeField] private ModuleCatalog moduleCatalog;
        [SerializeField] private GameObject extractionPanel;
        [SerializeField] private UnityEngine.UI.Image extractionFill;
        [SerializeField] private TMPro.TextMeshProUGUI extractionText;
        public void SetExtraction(bool show, float progress, float remaining)
        {
            if (show) Initialize();
            if (extractionPanel == null) return;
            extractionPanel.SetActive(show);
            if (extractionFill != null)
            {
                extractionFill.type = Image.Type.Filled;
                extractionFill.fillMethod = Image.FillMethod.Horizontal;
                extractionFill.fillOrigin = 0;
                extractionFill.fillAmount = Mathf.Clamp01(progress);
            }
            if (extractionText != null) extractionText.text = "탈출까지 " + Mathf.Max(0f, remaining).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }
        private int hoveredRegion = -1;
        public void ShowRegionInfo(int index)
        {
            if (regionInfo == null || controller == null) return;
            hoveredRegion = index;
            var player = GameManager.Instance != null && GameManager.Instance.UI != null ? GameManager.Instance.UI.Player : null;
            var region = RaidMapData.Current.Find(controller.RegionScene(index));
            regionInfo.text = region != null ? region.Describe(moduleCatalog, player != null ? player.GetComponent<PlayerModuleUnlocks>() : null) : "이 구역의 정보가 아직 등록되지 않았습니다.";
        }
        public void ClearRegionInfo(int index)
        {
            if (hoveredRegion != index) return;
            hoveredRegion = -1;
            if (regionInfo != null) regionInfo.text = "영역 위에 커서를 올려\n모듈과 상자 정보를 확인하세요.";
        }
        public RectTransform TutorialHouseButton => mapPanel != null && mapPanel.activeInHierarchy &&
            regionButtons != null && regionButtons.Length > 0 && regionButtons[0] != null
            ? regionButtons[0].transform as RectTransform : null;
        private bool initialized;
        private void Awake() => Initialize();
        public void Initialize()
        {
            if (initialized)
            {
                if (!gameObject.activeSelf) gameObject.SetActive(true);
                return;
            }
            initialized = true;
            transform.localScale = Vector3.one;
            SetExtraction(false, 0f, 5f);
            InitializeResult();
            mapPanel.SetActive(false);
            if (statusText != null) statusText.gameObject.SetActive(false);
            ClearRegionInfo(-1);
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }
        public void ShowError(string message)
        {
            Debug.LogError(message);
            FeedbackUI.For(this)?.ShowNotification(message);
        }
        public void Bind(NightRaidController owner)
        {
            controller=owner;
            for(int i=0;i<regionButtons.Length;i++)
            {
                int region=i;regionButtons[i].onClick.AddListener(()=>controller.EnterRegion(region));
                var hover = regionButtons[i].GetComponent<RaidRegionHoverUI>();
                if (hover == null) hover = regionButtons[i].gameObject.AddComponent<RaidRegionHoverUI>();
                hover.Bind(this, i);
            }
            backButton.onClick.AddListener(()=>controller.CloseMap());
        }
        public void ShowMap(bool show)
        {
            if (show) Initialize();
            mapPanel.SetActive(show); FeedbackUI.For(this)?.CancelChoice(this);
            ClearRegionInfo(hoveredRegion);
            if (show) mapPanel.transform.SetAsLastSibling();
            if(statusText!=null)statusText.gameObject.SetActive(false);
            for (int i = 0; i < regionButtons.Length; i++)
                regionButtons[i].interactable = show && (TutorialManager.Instance == null || TutorialManager.Instance.AllowsRaidRegion(i));
        }
        private void OnDisable() => FeedbackUI.For(this)?.CancelChoice(this);
        public void ShowReturn(bool show)
        {
            if (show) FeedbackUI.For(this)?.ShowChoice(this, "공방으로 귀환하시겠습니까?", () => controller.ReturnHome(), () => controller.CancelReturn());
            else FeedbackUI.For(this)?.CancelChoice(this);
        }
        public void SetLoading() { foreach(var button in regionButtons)button.interactable=false;backButton.interactable=false; }
        public void ResetBackButton() => backButton.interactable=TutorialManager.ActionAllowed("close-map");
    }
}
