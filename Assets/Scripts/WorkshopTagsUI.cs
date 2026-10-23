using System.Collections.Generic;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 공방 태그·스탯·습격 임시 보상 브리지. 기존 public API·직렬화 필드·씬 오버라이드 호환을 유지하며,
    /// 공방 태그·스탯 표시는 WorkshopTagsPresenter로, 습격 임시 보상은 RaidTempRewardPresenter로 위임한다.
    /// </summary>
    public sealed class WorkshopTagsUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private WeaponTypeData[] tags = new WeaponTypeData[0];
        [SerializeField] private TMPro.TextMeshProUGUI weightText;
        [SerializeField] private TMPro.TextMeshProUGUI salePriceText;
        [SerializeField] private TMPro.TextMeshProUGUI tagsText;
        [SerializeField] private RectTransform tagContainer;
        [SerializeField] private UnityEngine.UI.Image tagTemplate;
        [SerializeField] private TMPro.TMP_Text noTagsText;
        [SerializeField] private ShopCatalog shopCatalog;
        [SerializeField] private UnityEngine.Sprite raidFallbackIcon;
        [SerializeField] private RaidModuleIcon[] raidModuleIcons = System.Array.Empty<RaidModuleIcon>();
        [SerializeField] private WorkshopTagsView m_view;

        private WorkshopTagsPresenter m_presenter;
        private RaidTempRewardPresenter m_raidRewardPresenter;
        private bool m_isInitialized;

        private void Awake()
        {
            Initialize();
        }

        private void OnDisable()
        {
            m_presenter?.HandleDisabled();
            m_raidRewardPresenter?.Deactivate();
        }

        private void OnDestroy()
        {
            m_presenter?.Dispose();
        }

        private void LateUpdate()
        {
            // 습격 보상 표시는 RaidTempRewardPresenter가 Tick에서 처리
            if (m_raidRewardPresenter != null && m_raidRewardPresenter.Tick())
            {
                return;
            }

            // 공방 태그·스탯 표시는 WorkshopTagsPresenter가 Tick에서 처리
            m_presenter?.Tick();
        }

        /// <summary>
        /// View에 씬 오버라이드 참조를 주입하고 Presenter들을 초기화합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Initialize()
        {
            if (m_isInitialized)
            {
                return;
            }

            if (m_view == null)
            {
                m_view = GetComponent<WorkshopTagsView>();
            }

            if (m_view != null)
            {
                m_view.BindReferences(panel, weightText, salePriceText, tagsText, tagContainer, tagTemplate, noTagsText);
            }

            var uiManager = GetComponentInParent<UIManager>(true);
            m_presenter = new WorkshopTagsPresenter(m_view, uiManager, shopCatalog, tags, raidModuleIcons, raidFallbackIcon);
            m_presenter.Initialize();

            m_raidRewardPresenter = new RaidTempRewardPresenter(this);

            m_isInitialized = true;
        }

        // 습격 보상용 public API 유지 (기존 호출부 호환)
        // 습격 결과 화면의 모듈 아이콘이 같은 아이콘 표를 쓴다.
        public UnityEngine.Sprite GetRaidModuleIcon(int _moduleId)
        {
            foreach (RaidModuleIcon mapping in raidModuleIcons)
            {
                if (mapping.moduleId == _moduleId)
                {
                    return mapping.sprite != null ? mapping.sprite : raidFallbackIcon;
                }
            }

            return raidFallbackIcon;
        }

        public void EnterRaidRewards()
        {
            m_presenter?.EnterRaidRewards();
        }

        public void ShowRaidRewards(int _dollars, int _parts, int _tickets, System.Collections.Generic.IReadOnlyList<int> _modules)
        {
            m_presenter?.ShowRaidRewards(_dollars, _parts, _tickets, _modules);
        }

        public void LeaveRaidRewards()
        {
            m_presenter?.LeaveRaidRewards();
        }

        // 태그 판정 메서드 유지 (외부에서 호출하는 경우 호환)
        public void GetMatchedTags(PowerCore core, System.Collections.Generic.List<WeaponTypeData> result)
        {
            m_presenter?.GetMatchedTagsForCompat(core, result);
        }
    }
}