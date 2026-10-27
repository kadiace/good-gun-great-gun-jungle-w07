using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 기존 NightRaidUI API와 직렬화 참조를 유지하면서 지도·HUD View/Presenter를 연결한다.
    public sealed partial class NightRaidUI : MonoBehaviour, IUIScreen
    {
        [SerializeField] private GameObject mapPanel;
        [SerializeField] private GameObject returnPanel;
        [SerializeField] private Button[] regionButtons = new Button[0];
        [SerializeField] private Button backButton;
        [SerializeField] private Button returnButton;
        [SerializeField] private Button stayButton;
        [SerializeField] private TMPro.TMP_Text statusText;
        private NightRaidController controller;
        [SerializeField] private TMPro.TMP_Text regionInfo;
        [SerializeField] private ModuleCatalog moduleCatalog;
        [SerializeField] private GameObject extractionPanel;
        [SerializeField] private UnityEngine.UI.Image extractionFill;
        [SerializeField] private TMPro.TMP_Text extractionText;
        [SerializeField] private RaidMapView m_mapView;
        [SerializeField] private RaidHudView m_hudView;
        [SerializeField] private GameObject m_transitionCoverPrefab;

        private RaidMapPresenter m_mapPresenter;
        private bool initialized;
        private bool m_hasLoggedMapError;
        private bool m_hasLoggedHudError;

        public void SetExtraction(bool show, float progress, float remaining)
        {
            if (show)
            {
                Initialize();
            }

            RaidHudView hud = ResolveHudView();
            if (hud == null)
            {
                return;
            }

            hud.SetExtraction(show, progress, remaining);
        }

        public void ShowRegionInfo(int index)
        {
            m_mapPresenter?.ShowRegionInfo(index);
        }

        public void ClearRegionInfo(int index)
        {
            m_mapPresenter?.ClearRegionInfo(index);
        }

        public RectTransform TutorialHouseButton => m_mapPresenter != null ? m_mapPresenter.TutorialHouseButton : null;

        private void Awake()
        {
            Initialize();
        }

        public void InitializeScreen(UIContext _context) => Initialize();

        public void Initialize()
        {
            if (initialized)
            {
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }

                return;
            }

            initialized = true;
            transform.localScale = Vector3.one;
            RaidTransitionView.RegisterPrefab(m_transitionCoverPrefab);
            RaidHudView hud = ResolveHudView();
            if (hud != null)
            {
                hud.Initialize();
                hud.SetExtraction(false, 0f, 5f);
            }

            InitializeResult();
            if (m_mapView != null)
            {
                m_mapView.Initialize();
                m_mapView.SetVisible(false);
                m_mapView.ShowDefaultRegionInfo();
            }
            else if (!m_hasLoggedMapError)
            {
                m_hasLoggedMapError = true;
                Debug.LogError(
                    "[NightRaidUI] 지도 초기화 실패: m_mapView 참조가 없습니다. " +
                    "NightRaidUI 프리팹의 RaidMapView 저작과 연결을 확인하세요.", this);
            }

            if (statusText != null)
            {
                statusText.gameObject.SetActive(false);
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        public void ShowError(string message)
        {
            Debug.LogError(message);
            FeedbackUI.For(this)?.ShowNotification(message);
        }

        public void Bind(NightRaidController owner)
        {
            controller = owner;
            DisposeMapPresenter();
            if (m_mapView == null)
            {
                if (!m_hasLoggedMapError)
                {
                    m_hasLoggedMapError = true;
                    Debug.LogError(
                        "[NightRaidUI] 지도 초기화 실패: m_mapView 참조가 없습니다. " +
                        "NightRaidUI 프리팹의 RaidMapView 저작과 연결을 확인하세요.", this);
                }

                return;
            }

            m_mapPresenter = new RaidMapPresenter(m_mapView, owner, moduleCatalog);
            if (!m_mapPresenter.Initialize())
            {
                m_mapPresenter.Dispose();
                m_mapPresenter = null;
            }
        }

        public void ShowMap(bool show)
        {
            if (show)
            {
                Initialize();
            }

            if (statusText != null)
            {
                statusText.gameObject.SetActive(false);
            }

            FeedbackUI.For(this)?.CancelChoice(this);
            m_mapPresenter?.Show(show);
        }

        private void OnDisable()
        {
            FeedbackUI.For(this)?.CancelChoice(this);
            m_mapPresenter?.OnHidden();
            DisposeResult();
        }

        private void OnDestroy()
        {
            DisposeMapPresenter();
            DisposeResult();
        }

        public void ShowEntryConfirm(string message, System.Action onYes)
        {
            FeedbackUI.For(this)?.ShowChoice(this, message, onYes);
        }

        public void ShowUnarmedNotice()
        {
            FeedbackUI.For(this)?.ShowAcknowledgement(this, "무기를 장착해야 야간 습격에 입장할 수 있습니다.");
        }

        public void ShowReturn(bool show)
        {
            if (show)
            {
                FeedbackUI.For(this)?.ShowChoice(this, "공방으로 귀환하시겠습니까?",
                    () => controller.ReturnHome(), () => controller.CancelReturn());
            }
            else
            {
                FeedbackUI.For(this)?.CancelChoice(this);
            }
        }

        public void SetLoading()
        {
            m_mapPresenter?.SetLoading();
        }

        public void ResetBackButton()
        {
            m_mapPresenter?.ResetBackButton();
        }

        private RaidHudView ResolveHudView()
        {
            if (m_hudView != null)
            {
                return m_hudView;
            }

            if (!m_hasLoggedHudError)
            {
                m_hasLoggedHudError = true;
                Debug.LogError(
                    "[NightRaidUI] HUD 초기화 실패: m_hudView 참조가 없습니다. " +
                    "NightRaidUI 프리팹의 RaidHudView 저작과 연결을 확인하세요.", this);
            }

            return null;
        }

        private void DisposeMapPresenter()
        {
            m_mapPresenter?.Dispose();
            m_mapPresenter = null;
        }
    }
}
