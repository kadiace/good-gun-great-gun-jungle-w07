using UnityEngine;
using UnityEngine.EventSystems;

namespace WeaponWorkshop
{
    // 습격 결과 화면의 모듈 아이콘 하나. 포인터가 올라가면 공용 호버 툴팁에 모듈 상세를 띄운다.
    public sealed class ResultModuleIconHover : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
    {
        private HoveringUI m_tooltip;
        private ModuleData m_module;

        public void Bind(HoveringUI _tooltip, ModuleData _module)
        {
            m_tooltip = _tooltip;
            m_module = _module;
        }

        public void OnPointerEnter(PointerEventData _eventData)
        {
            ShowTooltip(_eventData);
        }

        public void OnPointerMove(PointerEventData _eventData)
        {
            ShowTooltip(_eventData);
        }

        public void OnPointerExit(PointerEventData _eventData)
        {
            ClearTooltip();
        }

        private void OnDisable()
        {
            ClearTooltip();
        }

        private void ShowTooltip(PointerEventData _eventData)
        {
            if (m_tooltip != null)
            {
                m_tooltip.ShowResultModule(m_module, _eventData.position);
            }
        }

        private void ClearTooltip()
        {
            if (m_tooltip != null)
            {
                m_tooltip.ClearResultModule();
            }
        }
    }
}
