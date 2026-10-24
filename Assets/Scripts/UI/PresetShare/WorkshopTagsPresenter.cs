using System;
using System.Collections.Generic;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 공방 태그·스탯(무게, 판매가, 태그) 표시 로직을 담당하는 Presenter입니다.
    /// 습격 임시 보상은 RaidTempRewardPresenter가 별도 처리하며, 이 Presenter는 공방 편집 모드일 때만 갱신합니다.
    /// </summary>
    public sealed class WorkshopTagsPresenter : IDisposable
    {
        private readonly WorkshopTagsView m_view;
        private readonly UIManager m_uiManager;
        private readonly ShopCatalog m_shopCatalog;
        private readonly WeaponTypeData[] m_tagData;
        private readonly RaidModuleIcon[] m_raidModuleIcons;
        private readonly Sprite m_raidFallbackIcon;

        private PowerCore m_lastCore;
        private float m_nextRefresh;
        private float m_lastWeight = float.NaN;
        private int m_lastPrice = int.MinValue;
        private readonly List<CustomerRequestData.Tag> m_appliedTags = new List<CustomerRequestData.Tag>();
        private readonly List<CustomerRequestData.Tag> m_badgeTags = new List<CustomerRequestData.Tag>();
        private readonly List<WeaponTypeData> m_matchedTags = new List<WeaponTypeData>();
        private bool m_badgesDrawn;
        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public WorkshopTagsPresenter(WorkshopTagsView _view, UIManager _uiManager, ShopCatalog _shopCatalog,
            WeaponTypeData[] _tagData, RaidModuleIcon[] _raidModuleIcons, Sprite _raidFallbackIcon)
        {
            m_view = _view;
            m_uiManager = _uiManager;
            m_shopCatalog = _shopCatalog;
            m_tagData = _tagData;
            m_raidModuleIcons = _raidModuleIcons;
            m_raidFallbackIcon = _raidFallbackIcon;
        }

        /// <summary>
        /// View 초기화를 연결합니다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null || m_uiManager == null)
            {
                if (!m_hasLoggedDependencyError)
                {
                    m_hasLoggedDependencyError = true;
                    Debug.LogError(
                        "[WorkshopTagsPresenter] Initialize 실패: 필수 의존성 누락 " +
                        $"(_view={m_view != null}, _uiManager={m_uiManager != null})");
                }
                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// View와 내부 상태를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.Dispose();
            }

            m_lastCore = null;
            m_nextRefresh = 0f;
            m_lastWeight = float.NaN;
            m_lastPrice = int.MinValue;
            m_appliedTags.Clear();
            m_badgeTags.Clear();
            m_matchedTags.Clear();
            m_badgesDrawn = false;
            m_isInitialized = false;
        }

        /// <summary>
        /// 매 프레임 공방 편집 모드에서 태그·스탯을 갱신합니다.
        /// 습격 중이면 RaidTempRewardPresenter가 별도 표시하므로 건너뜁니다.
        /// </summary>
        public void Tick()
        {
            if (!m_isInitialized)
            {
                return;
            }

            if (m_uiManager == null)
            {
                m_view.SetPanelVisible(false);
                return;
            }

            var raid = NightRaidController.Instance;
            if (raid != null && raid.IsInRaid)
            {
                m_view.SetPanelVisible(false);
                return;
            }

            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            var session = player != null ? player.GetComponent<WorkshopSession>() : null;
            var bench = session != null && session.IsEditing ? session.ActiveWorkbench : null;
            var holder = player != null ? player.GetComponent<WeaponHolder>() : null;
            var weapon = bench != null ? bench.CurrentWeapon : holder != null ? holder.EquippedWeapon : null;
            var core = weapon != null ? weapon.GetComponentInChildren<PowerCore>(true) : null;
            bool visible = core != null;

            if (m_view != null && m_view.gameObject.activeInHierarchy != visible)
            {
                m_view.SetPanelVisible(visible);
            }

            if (!visible)
            {
                m_lastCore = null;
                m_nextRefresh = 0f;
                m_lastWeight = float.NaN;
                m_lastPrice = int.MinValue;
                m_appliedTags.Clear();
                m_badgesDrawn = false;
                return;
            }

            if (m_lastCore == core && Time.unscaledTime < m_nextRefresh)
            {
                return;
            }

            m_lastCore = core;
            m_nextRefresh = Time.unscaledTime + 0.1f;

            var stats = core.GetCurrentStats();
            if (m_view != null && m_lastWeight != stats.weight)
            {
                m_lastWeight = stats.weight;
                m_view.SetWeightText($"무게  {stats.weight:0.##}");
            }

            GetMatchedTags(core, m_matchedTags);

            if (m_view != null && m_tagData != null)
            {
                var table = TableManager.Instance?.CustomerRequest;
                if (table != null)
                {
                    m_badgeTags.Clear();
                    foreach (var tag in table.tags)
                    {
                        if (tag != null)
                        {
                            var snapshot = new CustomerRequestInfo.WeaponSnapshot(core);
                            if (table.MeetsTag(tag.id, snapshot))
                            {
                                m_badgeTags.Add(tag);
                            }
                        }
                    }

                    if (!m_badgesDrawn || !SameTags(m_badgeTags, m_appliedTags))
                    {
                        m_badgesDrawn = true;
                        m_appliedTags.Clear();
                        foreach (var tag in m_badgeTags)
                        {
                            m_appliedTags.Add(tag);
                        }
                        m_view.UpdateTagBadges(m_appliedTags);
                    }
                }
            }

            if (m_view != null)
            {
                var collector = core.GetComponent<StatCollector>();
                int price = m_shopCatalog != null && collector != null
                    ? m_shopCatalog.GetSellPrice(collector.Modules, m_matchedTags) : 0;
                if (price != m_lastPrice)
                {
                    m_lastPrice = price;
                    m_view.SetSalePriceText($"총기 가치  <nobr>${price:N0}</nobr>");
                }
            }
        }

        /// <summary>
        /// 비활성화 시 패널을 숨기고 캐시를 초기화합니다.
        /// </summary>
        public void HandleDisabled()
        {
            if (m_view != null)
            {
                m_view.SetPanelVisible(false);
            }

            m_lastCore = null;
            m_nextRefresh = 0f;
            m_lastWeight = float.NaN;
            m_lastPrice = int.MinValue;
            m_appliedTags.Clear();
            m_badgesDrawn = false;
        }

        private void GetMatchedTags(PowerCore _core, List<WeaponTypeData> _result)
        {
            _result.Clear();
            if (_core == null || m_tagData == null || m_tagData.Length == 0)
            {
                return;
            }

            var snapshot = new CustomerRequestInfo.WeaponSnapshot(_core);
            var table = TableManager.Instance?.CustomerRequest;
            if (table == null)
            {
                return;
            }

            foreach (var tag in m_tagData)
            {
                if (tag != null && table.MeetsTag(tag.TableId, snapshot))
                {
                    _result.Add(tag);
                }
            }
        }

        private static bool SameTags(List<CustomerRequestData.Tag> _current, List<CustomerRequestData.Tag> _applied)
        {
            if (_current.Count != _applied.Count)
            {
                return false;
            }
            for (int i = 0; i < _current.Count; i++)
            {
                if (!ReferenceEquals(_current[i], _applied[i]))
                {
                    return false;
                }
            }
            return true;
        }

        // 습격 보상 표시는 RaidTempRewardPresenter가 담당하므로 여기서는 위임만 제공합니다.
        public void EnterRaidRewards()
        {
            if (m_view != null)
            {
                m_view.SetPanelVisible(true);
            }
        }

        public void ShowRaidRewards(int _dollars, int _parts, int _tickets, IReadOnlyList<int> _modules)
        {
            if (m_view != null)
            {
                m_view.ShowRaidRewards(_dollars, _parts, _tickets, _modules, m_raidFallbackIcon, m_raidModuleIcons);
            }
        }

        public void LeaveRaidRewards()
        {
            if (m_view != null)
            {
                m_view.LeaveRaidRewards();
            }
        }

        /// <summary>
        /// 기존 WorkshopTagsUI.GetMatchedTags 호환용 메서드.
        /// </summary>
        public void GetMatchedTagsForCompat(PowerCore _core, List<WeaponTypeData> _result)
        {
            GetMatchedTags(_core, _result);
        }
    }
}