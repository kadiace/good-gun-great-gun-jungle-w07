using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 저장 슬롯 카드 한 장(카드 버튼·요약 라벨)의 표시와 카드 클릭 이벤트만 담당하는 View 컴포넌트입니다.
    /// 삭제 버튼은 브리지의 배열 참조(씬 오버라이드 반영)를 SaveSlotsView가 직접 구동하므로 여기서 다루지 않습니다.
    /// 수동 슬롯 1~3의 카드 버튼은 프리팹 persistent 호출(SelectSlot)이 그대로 소유하므로 이 컴포넌트로
    /// 클릭을 연결하는 대상은 자동저장 카드뿐입니다(기존 EnsureAutoSaveSlot과 같은 규칙).
    /// </summary>
    public sealed class SaveSlotCardView : MonoBehaviour
    {
        [SerializeField] private Button m_button;
        [SerializeField] private TMP_Text m_label;

        private UnityAction m_clickHandler;
        private bool m_mainClickBound;
        private bool m_hasLoggedMissingButton;

        public event Action Clicked;

        public Button Button
        {
            get
            {
                return m_button;
            }
        }

        /// <summary>브리지가 보유한 실효 참조(씬 오버라이드 반영)를 카드에 전달합니다. null 인자는 무시합니다.</summary>
        public void BindReferences(Button _button, TMP_Text _label)
        {
            if (_button != null)
            {
                m_button = _button;
            }

            if (_label != null)
            {
                m_label = _label;
            }
        }

        /// <summary>
        /// 카드 버튼 클릭을 Clicked 이벤트로 전달합니다. 기존 EnsureAutoSaveSlot과 같이 원본 복제가 남긴
        /// persistent 호출을 먼저 꺼서 이중 실행을 막습니다. 중복 호출에 안전합니다.
        /// </summary>
        public void BindMainClick()
        {
            if (m_mainClickBound)
            {
                return;
            }

            if (m_button == null)
            {
                if (!m_hasLoggedMissingButton)
                {
                    m_hasLoggedMissingButton = true;
                    Debug.LogError($"[SaveSlotCardView] BindMainClick 실패: {name} 카드에 Button 참조가 없습니다.", this);
                }

                return;
            }

            for (int i = 0; i < m_button.onClick.GetPersistentEventCount(); i++)
            {
                m_button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            }

            m_clickHandler = OnClicked;
            m_button.onClick.RemoveListener(m_clickHandler);
            m_button.onClick.AddListener(m_clickHandler);
            m_mainClickBound = true;
        }

        /// <summary>바인딩한 카드 클릭 리스너를 해제합니다. 중복 호출에 안전합니다.</summary>
        public void UnbindMainClick()
        {
            if (!m_mainClickBound)
            {
                return;
            }

            if (m_button != null && m_clickHandler != null)
            {
                m_button.onClick.RemoveListener(m_clickHandler);
            }

            m_clickHandler = null;
            m_mainClickBound = false;
        }

        /// <summary>슬롯 요약 문장(슬롯 번호·설명 두 줄)을 갱신합니다.</summary>
        public void SetLabel(string _text)
        {
            if (m_label != null)
            {
                m_label.text = _text;
            }
        }

        /// <summary>카드 버튼의 저장·불러오기 가능 상태를 갱신합니다.</summary>
        public void SetInteractable(bool _interactable)
        {
            if (m_button != null)
            {
                m_button.interactable = _interactable;
            }
        }

        private void OnDestroy()
        {
            UnbindMainClick();
        }

        private void OnClicked()
        {
            Clicked?.Invoke();
        }
    }
}
