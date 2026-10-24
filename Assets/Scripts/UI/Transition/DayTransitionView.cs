using System.Collections;

using TMPro;

using UnityEngine;

namespace WeaponWorkshop
{
    // 날짜 전환의 저작된 참조, 페이드·제목 코루틴과 표시 상태만 담당한다.
    public sealed class DayTransitionView : MonoBehaviour
    {
        private const string VIEW_NAME = "DayTransitionView";

        private GameObject m_panel;
        private CanvasGroup m_fade;
        private TMP_Text m_dayText;
        private CanvasGroup m_titleFade;
        private float m_fadeDuration = .45f;
        private float m_titleDuration = 1f;

        private Coroutine m_transition;
        private bool m_isSequenceActive;
        private int m_runId;
        private bool m_isInitialized;
        private bool m_hasLoggedReferenceError;

        public event System.Action CoverReached;
        public event System.Action DayTextShown;
        public event System.Action Finished;

        public bool CanPlay
        {
            get
            {
                return m_isInitialized && !m_isSequenceActive && isActiveAndEnabled && ValidateReferences();
            }
        }

        /// <summary>
        /// 기존 DayTransitionUI에 직렬화된 참조와 시간 값을 전달받는다. 표시 상태는 바꾸지 않는다.
        /// </summary>
        public void Configure(GameObject _panel, CanvasGroup _fade, TMP_Text _dayText, CanvasGroup _titleFade,
            float _fadeDuration, float _titleDuration)
        {
            m_panel = _panel;
            m_fade = _fade;
            m_dayText = _dayText;
            m_titleFade = _titleFade;
            m_fadeDuration = _fadeDuration;
            m_titleDuration = _titleDuration;
        }

        /// <summary>
        /// 필수 참조를 모두 확인한 뒤에만 숨김 상태를 적용한다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (!ValidateReferences())
            {
                return false;
            }

            ApplyInitialHiddenState();
            m_isInitialized = true;
            return true;
        }

        public bool TryPlayDaySequence(bool _alreadyCovered)
        {
            if (!CanPlay)
            {
                return false;
            }

            PreparePanel();
            if (_alreadyCovered)
            {
                m_fade.alpha = 1f;
            }

            int runId = ++m_runId;
            m_isSequenceActive = true;
            Coroutine transition = StartCoroutine(RunDaySequence(runId, _alreadyCovered));
            if (IsRunActive(runId))
            {
                m_transition = transition;
            }

            return true;
        }

        public bool TryPlayTutorialSequence()
        {
            if (!CanPlay)
            {
                return false;
            }

            PreparePanel();

            int runId = ++m_runId;
            m_isSequenceActive = true;
            Coroutine transition = StartCoroutine(RunTutorialSequence(runId));
            if (IsRunActive(runId))
            {
                m_transition = transition;
            }

            return true;
        }

        public void ShowDayText(string _text)
        {
            if (!m_isSequenceActive || m_dayText == null)
            {
                return;
            }

            m_dayText.text = _text;
            m_dayText.gameObject.SetActive(true);
            DayTextShown?.Invoke();
        }

        /// <summary>
        /// 진행 중인 연출을 완료 이벤트 없이 중단하고 숨김 상태로 되돌린다.
        /// </summary>
        public void Cancel()
        {
            m_runId++;
            m_isSequenceActive = false;
            if (m_transition != null)
            {
                StopCoroutine(m_transition);
                m_transition = null;
            }

            ApplyInitialHiddenState();
        }

        private void PreparePanel()
        {
            m_panel.SetActive(true);
            UIScreenRoot.BringToFront(m_panel.transform);
            m_dayText.gameObject.SetActive(false);
        }

        private IEnumerator RunDaySequence(int _runId, bool _alreadyCovered)
        {
            if (!_alreadyCovered)
            {
                yield return FadeRoutine(0f, 1f);
                if (!IsRunActive(_runId))
                {
                    yield break;
                }
            }

            CoverReached?.Invoke();
            if (!IsRunActive(_runId))
            {
                yield break;
            }

            yield return FadeTitleRoutine(0f, 1f);
            if (!IsRunActive(_runId))
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(m_titleDuration);
            if (!IsRunActive(_runId))
            {
                yield break;
            }

            yield return FadeTitleRoutine(1f, 0f);
            if (!IsRunActive(_runId))
            {
                yield break;
            }

            m_dayText.gameObject.SetActive(false);
            yield return FadeRoutine(1f, 0f);
            if (!IsRunActive(_runId))
            {
                yield break;
            }

            m_panel.SetActive(false);
            CompleteSequence(_runId);
        }

        private IEnumerator RunTutorialSequence(int _runId)
        {
            yield return FadeRoutine(0f, 1f);
            if (!IsRunActive(_runId))
            {
                yield break;
            }

            CoverReached?.Invoke();
            if (!IsRunActive(_runId))
            {
                yield break;
            }

            yield return FadeRoutine(1f, 0f);
            if (!IsRunActive(_runId))
            {
                yield break;
            }

            m_panel.SetActive(false);
            CompleteSequence(_runId);
        }

        private IEnumerator FadeTitleRoutine(float _from, float _to)
        {
            if (m_titleFade == null)
            {
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < m_fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                m_titleFade.alpha = Mathf.Lerp(_from, _to,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / m_fadeDuration)));
                yield return null;
            }

            m_titleFade.alpha = _to;
        }

        private IEnumerator FadeRoutine(float _from, float _to)
        {
            float elapsed = 0f;
            while (elapsed < m_fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                m_fade.alpha = Mathf.Lerp(_from, _to,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / m_fadeDuration)));
                yield return null;
            }

            m_fade.alpha = _to;
        }

        private void CompleteSequence(int _runId)
        {
            if (!IsRunActive(_runId))
            {
                return;
            }

            m_isSequenceActive = false;
            m_transition = null;
            m_runId++;
            Finished?.Invoke();
        }

        private bool IsRunActive(int _runId)
        {
            return m_isSequenceActive && m_runId == _runId;
        }

        private void ApplyInitialHiddenState()
        {
            if (m_fade != null)
            {
                m_fade.alpha = 0f;
            }

            if (m_titleFade != null)
            {
                m_titleFade.alpha = 0f;
            }

            if (m_panel != null)
            {
                m_panel.SetActive(false);
            }

            if (m_dayText != null)
            {
                m_dayText.gameObject.SetActive(false);
            }
        }

        private bool ValidateReferences()
        {
            bool isValid = m_panel != null && m_fade != null && m_dayText != null;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    $"[{VIEW_NAME}] 필수 참조 누락: panel={m_panel != null}, fade={m_fade != null}, " +
                    $"dayText={m_dayText != null}. DayTransitionUI의 직렬화 참조를 확인하세요.");
            }

            return isValid;
        }
    }
}
