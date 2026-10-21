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
        private TMPro.TMP_Text[] regionLabels;
        private string[] originalRegionLabels;
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
            var region = TableManager.Instance.RaidMap.Find(controller.RegionScene(index));
            var save = FindFirstObjectByType<GameSaveController>();
            regionInfo.text = region.Describe(moduleCatalog, player != null ? player.GetComponent<PlayerModuleUnlocks>() : null,
                save != null && save.HasClearedRaid(region?.scene));
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
            regionLabels = new TMPro.TMP_Text[regionButtons.Length];
            originalRegionLabels = new string[regionButtons.Length];
            for(int i=0;i<regionButtons.Length;i++)
            {
                regionLabels[i] = regionButtons[i].GetComponentInChildren<TMPro.TMP_Text>(true);
                if (regionLabels[i] != null) originalRegionLabels[i] = regionLabels[i].text;
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
            RefreshRegionButtons();
        }
        private void RefreshRegionButtons()
        {
            bool show = mapPanel != null && mapPanel.activeSelf;
            int listedCount = controller != null ? controller.RegionCount : regionButtons.Length;
            var save = show ? FindFirstObjectByType<GameSaveController>() : null;
            for (int i = 0; i < regionButtons.Length; i++)
            {
                if (regionButtons[i] == null) continue;
                // 테이블에 없는 칸은 계속 꺼 둠으로써 표의 행 수가 지도의 칸 수를 결정하게 한다.
                bool listed = i < listedCount;
                if (!listed) regionButtons[i].gameObject.SetActive(false);
                if (regionLabels != null && regionLabels[i] != null)
                {
                    var region = listed && controller != null ? TableManager.Instance.RaidMap.Find(controller.RegionScene(i)) : null;
                    bool firstClearAvailable = region != null && region.FirstClearSaleBonus() > 0f &&
                        (save == null || !save.HasClearedRaid(region.scene));
                    regionLabels[i].text = originalRegionLabels[i] + (firstClearAvailable ? " <size=65%><color=#F04D4D>●</color></size>" : string.Empty);
                }
                regionButtons[i].interactable = show && listed &&
                    (TutorialManager.Instance == null || TutorialManager.Instance.AllowsRaidRegion(i));
            }
        }
        private void OnDisable()
        {
            FeedbackUI.For(this)?.CancelChoice(this);
        }
        // 입장 확인은 지도 소유로 띄워 지도를 다시 열거나 닫으면(CancelChoice(this)) 함께 취소되게 한다.
        public void ShowEntryConfirm(string message, System.Action onYes) => FeedbackUI.For(this)?.ShowChoice(this, message, onYes);
        public void ShowUnarmedNotice() => FeedbackUI.For(this)?.ShowAcknowledgement(this, "무기를 장착해야 야간 습격에 입장할 수 있습니다.");
        public void ShowReturn(bool show)
        {
            if (show) FeedbackUI.For(this)?.ShowChoice(this, "공방으로 귀환하시겠습니까?", () => controller.ReturnHome(), () => controller.CancelReturn());
            else FeedbackUI.For(this)?.CancelChoice(this);
        }
        public void SetLoading() { foreach(var button in regionButtons)button.interactable=false;backButton.interactable=false; }
        public void ResetBackButton() => backButton.interactable=TutorialManager.ActionAllowed("close-map");
    }
}
