using System;
using System.Collections;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using TMPro;

namespace WeaponWorkshop
{
    // 저작된 결과 UI 참조, 확인 버튼 입력, 실제 시간 페이드와 포커스 적용을 담당한다.
    public sealed class ResultView : MonoBehaviour
    {
        private const string VIEW_NAME = "ResultView";
        private const float FADE_DURATION = .45f;

        private Transform m_root;
        private CanvasGroup m_backdrop;
        private CanvasGroup m_content;
        private TextMeshProUGUI m_title;
        private TextMeshProUGUI m_body;
        private Button m_confirm;

        private Coroutine m_animation;
        private int m_runId;
        private bool m_isInitialized;
        private bool m_isRevealed;
        private bool m_isHiding;
        private bool m_isButtonBound;
        private bool m_hasLoggedReferenceError;

        public event Action ConfirmRequested;
        public event Action CoverReached;
        public event Action Revealed;
        public event Action Hidden;

        public bool IsInitialized => m_isInitialized;

        private void Awake()
        {
            if (!m_isInitialized)
            {
                ApplyInitialHiddenState();
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }

        /// <summary>
        /// NightRaidUI에 직렬화된 결과 참조와 표시 순서를 옮겨 받는다. 표시 상태는 바꾸지 않는다.
        /// </summary>
        public void Configure(Transform _root, CanvasGroup _backdrop, CanvasGroup _content,
            TextMeshProUGUI _title, TextMeshProUGUI _body, Button _confirm)
        {
            m_root = _root;
            m_backdrop = _backdrop;
            m_content = _content;
            m_title = _title;
            m_body = _body;
            m_confirm = _confirm;
        }

        /// <summary>
        /// 필수 참조를 확인하고 버튼 이벤트를 연결하기 전에 명시적인 숨김 상태를 적용한다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            ApplyInitialHiddenState();
            if (!ValidateReferences())
            {
                return false;
            }

            BindButton();
            m_confirm.navigation = new Navigation { mode = Navigation.Mode.None };
            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 진행 중인 페이드를 멈추고 버튼 리스너를 해제한다. 확인 콜백은 호출하지 않는다.
        /// </summary>
        public void Dispose()
        {
            StopAnimation();
            UnbindButton();
            m_isInitialized = false;
        }

        /// <summary>
        /// 결과 문구를 적용하고 배경 → 내용 순서의 페이드를 시작한다.
        /// </summary>
        public bool TryShow(string _title, Color _titleColor, string _body)
        {
            if (!isActiveAndEnabled)
            {
                return false;
            }

            if (!m_isInitialized && !Initialize())
            {
                return false;
            }

            if (!ValidateReferences())
            {
                return false;
            }

            StopAnimation();
            m_isRevealed = false;
            m_isHiding = false;
            m_content.alpha = 0f;
            m_title.text = _title ?? string.Empty;
            m_title.color = _titleColor;
            m_body.text = _body ?? string.Empty;
            m_body.color = Color.white;
            m_confirm.interactable = false;
            m_backdrop.alpha = 0f;
            m_backdrop.gameObject.SetActive(true);
            if (m_root != null)
            {
                m_root.SetAsLastSibling();
            }

            m_backdrop.transform.SetAsLastSibling();

            int runId = ++m_runId;
            Coroutine animation = StartCoroutine(RevealRoutine(runId));
            if (IsRunActive(runId))
            {
                m_animation = animation;
            }

            return true;
        }

        /// <summary>
        /// 표시된 내용만 실제 시간으로 페이드아웃한다. 검은 배경은 씬 복귀를 위해 남긴다.
        /// </summary>
        public bool BeginHide()
        {
            if (!m_isInitialized || !m_isRevealed || m_isHiding || !isActiveAndEnabled)
            {
                return false;
            }

            StopAnimation();
            m_isRevealed = false;
            m_isHiding = true;
            m_confirm.interactable = false;
            int runId = ++m_runId;
            Coroutine animation = StartCoroutine(HideRoutine(runId));
            if (IsRunActive(runId))
            {
                m_animation = animation;
            }

            return true;
        }

        /// <summary>
        /// 진행 중인 연출을 이벤트 없이 취소하고 초기 숨김 상태로 되돌린다.
        /// </summary>
        public void Cancel()
        {
            StopAnimation();
            ApplyInitialHiddenState();
        }

        /// <summary>
        /// 현재 EventSystem 선택 객체가 유효한 Selectable이면 반환한다.
        /// </summary>
        public GameObject GetValidSelectedObject()
        {
            if (EventSystem.current == null)
            {
                return null;
            }

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            return IsValidSelectable(selected) ? selected : null;
        }

        /// <summary>
        /// 내용 페이드가 끝난 뒤 확인 버튼에만 선택을 적용한다.
        /// </summary>
        public void ApplyConfirmFocus()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(null);
            if (m_confirm != null && IsValidSelectable(m_confirm.gameObject))
            {
                EventSystem.current.SetSelectedGameObject(m_confirm.gameObject);
            }
        }

        /// <summary>
        /// 강제 정리 전에 보관한 선택을 유효한 Selectable일 때만 복원한다.
        /// </summary>
        public void RestoreFocus(GameObject _previous)
        {
            if (EventSystem.current == null)
            {
                return;
            }

            if (!IsValidSelectable(_previous))
            {
                EventSystem.current.SetSelectedGameObject(null);
                return;
            }

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(_previous);
        }

        private IEnumerator RevealRoutine(int _runId)
        {
            yield return FadeRoutine(m_backdrop, 0f, 1f, _runId);
            if (!IsRunActive(_runId))
            {
                yield break;
            }

            CoverReached?.Invoke();
            if (!IsRunActive(_runId))
            {
                yield break;
            }

            yield return FadeRoutine(m_content, 0f, 1f, _runId);
            if (!IsRunActive(_runId))
            {
                yield break;
            }

            m_animation = null;
            m_isRevealed = true;
            m_confirm.interactable = true;
            Revealed?.Invoke();
        }

        private IEnumerator HideRoutine(int _runId)
        {
            yield return FadeRoutine(m_content, 1f, 0f, _runId);
            if (!IsRunActive(_runId))
            {
                yield break;
            }

            m_animation = null;
            m_isHiding = false;
            Hidden?.Invoke();
        }

        private IEnumerator FadeRoutine(CanvasGroup _group, float _from, float _to, int _runId)
        {
            float elapsed = 0f;
            while (elapsed < FADE_DURATION)
            {
                if (!IsRunActive(_runId))
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(_from, _to,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / FADE_DURATION)));
                yield return null;
            }

            if (!IsRunActive(_runId))
            {
                yield break;
            }

            _group.alpha = _to;
        }

        private void StopAnimation()
        {
            m_runId++;
            if (m_animation != null)
            {
                StopCoroutine(m_animation);
                m_animation = null;
            }

            m_isRevealed = false;
            m_isHiding = false;
        }

        private bool IsRunActive(int _runId)
        {
            return m_runId == _runId;
        }

        private void ApplyInitialHiddenState()
        {
            if (m_backdrop != null)
            {
                m_backdrop.alpha = 0f;
                m_backdrop.gameObject.SetActive(false);
            }

            if (m_content != null)
            {
                m_content.alpha = 0f;
            }

            if (m_confirm != null)
            {
                m_confirm.interactable = false;
            }
        }

        private bool ValidateReferences()
        {
            bool isValid = m_backdrop != null && m_content != null && m_title != null &&
                m_body != null && m_confirm != null;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    $"[{VIEW_NAME}] 필수 참조 누락: backdrop={m_backdrop != null}, content={m_content != null}, " +
                    $"title={m_title != null}, body={m_body != null}, confirm={m_confirm != null}. " +
                    "NightRaidUI의 결과 화면 직렬화 참조를 확인하세요.", this);
            }

            return isValid;
        }

        private void BindButton()
        {
            if (m_isButtonBound || m_confirm == null)
            {
                return;
            }

            m_confirm.onClick.AddListener(OnConfirmClicked);
            m_isButtonBound = true;
        }

        private void UnbindButton()
        {
            if (!m_isButtonBound)
            {
                return;
            }

            if (m_confirm != null)
            {
                m_confirm.onClick.RemoveListener(OnConfirmClicked);
            }

            m_isButtonBound = false;
        }

        private void OnConfirmClicked()
        {
            ConfirmRequested?.Invoke();
        }

        private static bool IsValidSelectable(GameObject _target)
        {
            if (_target == null || !_target.activeInHierarchy)
            {
                return false;
            }

            Selectable selectable = _target.GetComponent<Selectable>();
            return selectable != null && selectable.isActiveAndEnabled && selectable.IsInteractable();
        }
    }
}
