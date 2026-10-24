using System;

using UnityEngine;
using UnityEngine.EventSystems;

namespace WeaponWorkshop
{
    // 대화 확인 버튼의 포인터 누름·클릭과 UI Submit을 각각 분리해 전달한다.
    public sealed class DialogueConfirmButtonEventRelay : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, ISubmitHandler
    {
        private Action m_onPointerDown;
        private Action m_onPointerClick;
        private Action m_onSubmit;

        public void Bind(Action _onPointerDown, Action _onPointerClick, Action _onSubmit)
        {
            m_onPointerDown = _onPointerDown;
            m_onPointerClick = _onPointerClick;
            m_onSubmit = _onSubmit;
        }

        public void OnPointerDown(PointerEventData _eventData)
        {
            if (_eventData == null || _eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            m_onPointerDown?.Invoke();
        }

        public void OnPointerClick(PointerEventData _eventData)
        {
            if (_eventData == null || _eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            m_onPointerClick?.Invoke();
        }

        public void OnSubmit(BaseEventData _eventData)
        {
            m_onSubmit?.Invoke();
        }
    }
}
