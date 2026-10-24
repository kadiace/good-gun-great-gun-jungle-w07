using UnityEngine;

namespace WeaponWorkshop
{
    // 습격 중 임시 달러·부품·티켓·모듈 보상 표시를 컨트롤러에서 읽어 View에 전달한다.
    public sealed class RaidTempRewardPresenter
    {
        private readonly WorkshopTagsUI m_view;
        private bool m_isActive;

        public RaidTempRewardPresenter(WorkshopTagsUI _view)
        {
            m_view = _view;
        }

        public bool Tick()
        {
            if (m_view == null)
            {
                return false;
            }

            NightRaidController raid = NightRaidController.Instance;
            if (raid == null || !raid.IsInRaid)
            {
                if (m_isActive)
                {
                    m_isActive = false;
                    m_view.LeaveRaidRewards();
                }

                return false;
            }

            if (!m_isActive)
            {
                m_isActive = true;
                m_view.EnterRaidRewards();
            }

            m_view.ShowRaidRewards(raid.RaidEarnedDollars, raid.RaidEarnedParts, raid.RaidEarnedTickets,
                raid.RaidEarnedModules);
            return true;
        }

        public void Deactivate()
        {
            m_isActive = false;
        }
    }
}
