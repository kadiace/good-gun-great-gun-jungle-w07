namespace WeaponWorkshop
{
    // 장착 무기의 충전 상태만 조회해 ChargeGaugeView에 표시를 요청한다.
    public sealed class ChargeGaugePresenter
    {
        private readonly ChargeGaugeView m_view;
        private readonly UIManager m_uiManager;

        public ChargeGaugePresenter(ChargeGaugeView _view, UIManager _uiManager)
        {
            m_view = _view;
            m_uiManager = _uiManager;
        }

        public bool Initialize()
        {
            return m_view != null && m_view.Initialize();
        }

        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.Dispose();
            }
        }

        public void Refresh()
        {
            if (m_view == null)
            {
                return;
            }

            WeaponHolder holder = m_uiManager != null ? m_uiManager.PlayerWeaponHolder : null;
            EquipableWeapon weapon = holder != null ? holder.EquippedWeapon : null;
            if (weapon == null || !weapon.IsCharging)
            {
                m_view.Hide();
                return;
            }
            weapon.GetChargeState(out float progress, out int level, out int maximum);
            m_view.SetCharge(maximum > 0, progress, level, maximum);
        }
    }
}
