using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 공용 UIManager 프리팹의 4계층(HudLayer, ScreenLayer, PopupLayer, TransitionLayer) 참조와
    /// 화면 전환 시 EventSystem 선택 포커스 보관·적용·복원을 담당합니다.
    /// </summary>
    public sealed class UIRoot : MonoBehaviour
    {
        private const string SCREEN_NAME = "UIRoot";

        [SerializeField] private RectTransform m_hudLayer;
        [SerializeField] private RectTransform m_screenLayer;
        [SerializeField] private RectTransform m_popupLayer;
        [SerializeField] private RectTransform m_transitionLayer;

        private GameObject m_previousSelectedObject;
        private bool m_hasLoggedLayerError;

        public RectTransform HudLayer
        {
            get
            {
                return m_hudLayer;
            }
        }

        public RectTransform ScreenLayer
        {
            get
            {
                return m_screenLayer;
            }
        }

        public RectTransform PopupLayer
        {
            get
            {
                return m_popupLayer;
            }
        }

        public RectTransform TransitionLayer
        {
            get
            {
                return m_transitionLayer;
            }
        }

        public GameObject PreviousSelectedObject
        {
            get
            {
                return m_previousSelectedObject;
            }
        }

        private void Awake()
        {
            ValidateLayers();
        }

        /// <summary>
        /// 4개의 필수 레이어 RectTransform 참조가 모두 직렬화되어 있는지 검사합니다.
        /// 누락 시 화면명·필드명을 포함해 1회 오류를 기록합니다.
        /// </summary>
        public bool ValidateLayers()
        {
            bool isValid = m_hudLayer != null &&
                           m_screenLayer != null &&
                           m_popupLayer != null &&
                           m_transitionLayer != null;

            if (!isValid && !m_hasLoggedLayerError)
            {
                m_hasLoggedLayerError = true;
                Debug.LogError(
                    $"[{SCREEN_NAME}] Initialize 실패: 필수 레이어 참조 누락 " +
                    $"(m_hudLayer={m_hudLayer != null}, m_screenLayer={m_screenLayer != null}, " +
                    $"m_popupLayer={m_popupLayer != null}, m_transitionLayer={m_transitionLayer != null})",
                    this);
            }

            return isValid;
        }

        /// <summary>
        /// 대상 화면 RectTransform이 지정 레이어에 속하도록 보장하고 해당 레이어의 최상위 순서로 배치합니다.
        /// </summary>
        public void BringToLayerFront(RectTransform _target, RectTransform _expectedLayer)
        {
            if (_target == null)
            {
                return;
            }

            if (_expectedLayer != null && _target.parent != _expectedLayer)
            {
                _target.SetParent(_expectedLayer, false);
            }

            _target.SetAsLastSibling();
        }

        /// <summary>
        /// 상위 화면을 열기 직전의 유효 선택 객체를 보관합니다.
        /// 이미 열리려는 화면 내부의 하위 요소가 선택된 상태라면 덮어쓰지 않습니다.
        /// </summary>
        public void SaveCurrentFocus(Transform _ignoredHierarchyRoot)
        {
            UnityEngine.EventSystems.EventSystem eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null)
            {
                m_previousSelectedObject = null;
                return;
            }

            GameObject current = eventSystem.currentSelectedGameObject;
            if (current != null && _ignoredHierarchyRoot != null && current.transform.IsChildOf(_ignoredHierarchyRoot))
            {
                return;
            }

            m_previousSelectedObject = current;
        }

        /// <summary>
        /// 화면 표시 성공 후 기본 선택 대상을 EventSystem에 적용합니다.
        /// 기본 대상이 비활성·사용 불가이면 후보 목록에서 첫 유효 항목을 선택합니다.
        /// </summary>
        public bool ApplyFocus(UnityEngine.UI.Selectable _defaultSelectable, UnityEngine.UI.Selectable[] _fallbackSelectables)
        {
            UnityEngine.EventSystems.EventSystem eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            UnityEngine.UI.Selectable target = null;
            if (IsValidSelectable(_defaultSelectable))
            {
                target = _defaultSelectable;
            }
            else if (_fallbackSelectables != null)
            {
                for (int i = 0; i < _fallbackSelectables.Length; i++)
                {
                    UnityEngine.UI.Selectable candidate = _fallbackSelectables[i];
                    if (IsValidSelectable(candidate))
                    {
                        target = candidate;
                        break;
                    }
                }
            }

            eventSystem.SetSelectedGameObject(null);
            if (target != null)
            {
                eventSystem.SetSelectedGameObject(target.gameObject);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 화면을 닫을 때 보관해 둔 이전 선택 객체가 여전히 활성·상호작용 가능하면 복원하고,
        /// 그렇지 않으면 대체 항목 또는 null로 선택을 정리합니다.
        /// </summary>
        public void RestorePreviousFocus(UnityEngine.UI.Selectable _fallbackSelectable = null)
        {
            GameObject saved = m_previousSelectedObject;
            m_previousSelectedObject = null;

            UnityEngine.EventSystems.EventSystem eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            if (saved != null && saved.activeInHierarchy)
            {
                UnityEngine.UI.Selectable savedSelectable = saved.GetComponent<UnityEngine.UI.Selectable>();
                if (IsValidSelectable(savedSelectable))
                {
                    eventSystem.SetSelectedGameObject(null);
                    eventSystem.SetSelectedGameObject(saved);
                    return;
                }
            }

            if (IsValidSelectable(_fallbackSelectable))
            {
                eventSystem.SetSelectedGameObject(null);
                eventSystem.SetSelectedGameObject(_fallbackSelectable.gameObject);
                return;
            }

            eventSystem.SetSelectedGameObject(null);
        }

        /// <summary>
        /// Selectable이 현재 계층에서 활성화되어 있고 상호작용 가능한 상태인지 판정합니다.
        /// </summary>
        public static bool IsValidSelectable(UnityEngine.UI.Selectable _selectable)
        {
            return _selectable != null &&
                   _selectable.gameObject.activeInHierarchy &&
                   _selectable.isActiveAndEnabled &&
                   _selectable.IsInteractable();
        }
    }
}
