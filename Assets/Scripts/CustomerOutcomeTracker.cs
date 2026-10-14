using System;
using UnityEngine;

namespace WeaponWorkshop
{
    // 사망 알림을 UI에 전달합니다. 하루 집계는 DayManager가 담당합니다.
    public sealed class CustomerOutcomeTracker : MonoBehaviour
    {
        [SerializeField] private Bell customerBell;
        public event Action CustomerKilled;

        private void OnEnable()
        {
            if (customerBell != null) customerBell.CustomerKilled += OnCustomerKilled;
        }

        private void OnDisable()
        {
            if (customerBell != null) customerBell.CustomerKilled -= OnCustomerKilled;
        }

        private void OnCustomerKilled() => CustomerKilled?.Invoke();
    }
}
