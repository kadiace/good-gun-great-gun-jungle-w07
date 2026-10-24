using UnityEngine;
using UnityEngine.EventSystems;

namespace WeaponWorkshop
{
    // 지도 지역 버튼의 포인터·선택 진입을 RaidMapView 이벤트로 전달한다.
    public sealed class RaidRegionHoverUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        [SerializeField] private RaidMapView m_view;
        [SerializeField] private int m_regionIndex;

        public void OnPointerEnter(PointerEventData _eventData)
        {
            NotifyHovered();
        }

        public void OnPointerExit(PointerEventData _eventData)
        {
            NotifyCleared();
        }

        public void OnSelect(BaseEventData _eventData)
        {
            NotifyHovered();
        }

        public void OnDeselect(BaseEventData _eventData)
        {
            NotifyCleared();
        }

        private void NotifyHovered()
        {
            if (m_view != null)
            {
                m_view.NotifyRegionHovered(m_regionIndex);
            }
        }

        private void NotifyCleared()
        {
            if (m_view != null)
            {
                m_view.NotifyRegionHoverCleared(m_regionIndex);
            }
        }
    }
}
