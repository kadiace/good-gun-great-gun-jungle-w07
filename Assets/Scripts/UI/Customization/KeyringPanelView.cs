using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

using TMPro;

namespace WeaponWorkshop
{
    // 키링 패널의 모델 목록과 상태 표시를 담당하는 View다.
    // 저작된 타일을 재사용하고 모델 수가 더 많으면 마지막 타일을 템플릿으로 복제한다.
    public sealed class KeyringPanelView : MonoBehaviour
    {
        private const string VIEW_NAME = "KeyringPanelView";
        private static readonly Color PLACING_COLOR = new Color(0.34f, 0.45f, 0.5f, 1f);
        private static readonly Color IDLE_COLOR = new Color(0.13f, 0.15f, 0.17f, 1f);

        [SerializeField] private Button[] m_buttons = Array.Empty<Button>();
        [SerializeField] private Outline[] m_highlights = Array.Empty<Outline>();
        [SerializeField] private TMP_Text m_empty;
        [SerializeField] private TMP_Text m_status;
        [SerializeField] private TMP_Text m_count;
        [SerializeField] private Button m_remove;

        private readonly List<GameObject> m_clones = new List<GameObject>();
        private UnityAction[] m_buttonActions;
        private GameObject[] m_models = Array.Empty<GameObject>();
        private int m_boundButtonCount;
        private bool m_isInitialized;
        private bool m_hasLoggedReferenceError;

        public event Action<int> KeyringRequested;
        public event Action RemoveRequested;

        public bool IsInitialized => m_isInitialized;

        public void Configure(Button[] _buttons, Outline[] _highlights, TMP_Text _empty, TMP_Text _status,
            TMP_Text _count, Button _remove)
        {
            m_buttons = _buttons != null ? _buttons : Array.Empty<Button>();
            m_highlights = _highlights != null ? _highlights : Array.Empty<Outline>();
            m_empty = _empty;
            m_status = _status;
            m_count = _count;
            m_remove = _remove;
        }

        // 초기 모델 목록으로 타일·이름·입력을 연결한다.
        public bool Initialize(IReadOnlyList<GameObject> _models, string _emptyMessage)
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (!ValidateReferences())
            {
                return false;
            }

            ShowModels(_models, _emptyMessage);
            m_remove.onClick.AddListener(OnRemove);
            m_isInitialized = true;
            return true;
        }

        public void Dispose()
        {
            if (!m_isInitialized)
            {
                return;
            }

            for (int i = 0; i < m_boundButtonCount; i++)
            {
                if (m_buttons[i] != null && m_buttonActions[i] != null)
                {
                    m_buttons[i].onClick.RemoveListener(m_buttonActions[i]);
                }
            }

            m_remove.onClick.RemoveListener(OnRemove);
            DestroyClones();
            m_buttonActions = null;
            m_boundButtonCount = 0;
            m_isInitialized = false;
        }

        // 목록을 다시 읽은 뒤 타일 이름과 표시 여부를 갱신한다.
        public void ShowModels(IReadOnlyList<GameObject> _models, string _emptyMessage)
        {
            int count = _models != null ? _models.Count : 0;
            m_models = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                m_models[i] = _models[i];
            }

            EnsureTileCount(count);
            EnsureBindings();
            for (int i = 0; i < m_buttons.Length; i++)
            {
                bool available = i < m_models.Length;
                SetActive(m_buttons[i].gameObject, available);
                if (!available)
                {
                    continue;
                }

                TMP_Text label = m_buttons[i].GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = m_models[i] != null ? m_models[i].name : string.Empty;
                }
            }

            SetText(m_empty, _emptyMessage);
            SetActive(m_empty != null ? m_empty.gameObject : null, count == 0);
        }

        // Presenter가 계산한 한 프레임 표시 값을 적용한다.
        public void Render(KeyringPanelState _state)
        {
            if (!m_isInitialized || _state == null)
            {
                return;
            }

            SetActive(m_empty != null ? m_empty.gameObject : null, _state.ShowEmpty);
            for (int i = 0; i < m_buttons.Length; i++)
            {
                bool selected = i < m_models.Length && i == _state.SelectedIndex;
                m_buttons[i].interactable = _state.CanSelect;
                if (m_buttons[i].targetGraphic != null)
                {
                    m_buttons[i].targetGraphic.color = selected && _state.Placing ? PLACING_COLOR : IDLE_COLOR;
                }

                if (i < m_highlights.Length && m_highlights[i] != null)
                {
                    m_highlights[i].enabled = selected;
                }
            }

            SetText(m_status, _state.Status);
            SetText(m_count, _state.CountLabel);
            m_remove.interactable = _state.CanRemove;
        }

        private void Awake()
        {
            if (!m_isInitialized && m_empty != null)
            {
                m_empty.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void OnRemove()
        {
            RemoveRequested?.Invoke();
        }

        private void EnsureTileCount(int _count)
        {
            if (_count <= m_buttons.Length || m_buttons.Length == 0)
            {
                return;
            }

            Button template = m_buttons[m_buttons.Length - 1];
            Transform parent = template.transform.parent;
            int authored = m_buttons.Length;
            int missing = _count - authored;
            Array.Resize(ref m_buttons, _count);
            Array.Resize(ref m_highlights, _count);
            for (int i = 0; i < missing; i++)
            {
                GameObject clone = Instantiate(template.gameObject, parent);
                clone.name = template.name + "_Pool" + i;
                clone.SetActive(true);
                m_clones.Add(clone);
                m_buttons[authored + i] = clone.GetComponent<Button>();
                m_highlights[authored + i] = clone.GetComponent<Outline>();
            }
        }

        // 아직 연결하지 않은 타일에만 입력을 붙인다. ShowModels 재호출에 안전하다.
        private void EnsureBindings()
        {
            if (m_buttonActions == null || m_buttonActions.Length < m_buttons.Length)
            {
                int previous = m_buttonActions != null ? m_buttonActions.Length : 0;
                Array.Resize(ref m_buttonActions, m_buttons.Length);
                for (int i = previous; i < m_buttonActions.Length; i++)
                {
                    m_buttonActions[i] = null;
                }
            }

            for (int i = m_boundButtonCount; i < m_buttons.Length; i++)
            {
                int index = i;
                UnityAction action = () => KeyringRequested?.Invoke(index);
                m_buttonActions[i] = action;
                m_buttons[i].onClick.AddListener(action);
            }

            m_boundButtonCount = m_buttons.Length;
        }

        private void DestroyClones()
        {
            for (int i = 0; i < m_clones.Count; i++)
            {
                if (m_clones[i] == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(m_clones[i]);
                }
                else
                {
                    DestroyImmediate(m_clones[i]);
                }
            }

            m_clones.Clear();
        }

        private bool ValidateReferences()
        {
            bool arraysValid = m_buttons != null && m_highlights != null && m_buttons.Length > 0 &&
                m_buttons.Length == m_highlights.Length &&
                !Array.Exists(m_buttons, _button => _button == null) &&
                !Array.Exists(m_highlights, _highlight => _highlight == null);
            if (arraysValid && m_empty != null && m_status != null && m_count != null && m_remove != null)
            {
                return true;
            }

            if (!m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                List<string> missing = new List<string>();
                if (!arraysValid)
                {
                    missing.Add(nameof(m_buttons) + "/" + nameof(m_highlights));
                }

                if (m_empty == null)
                {
                    missing.Add(nameof(m_empty));
                }

                if (m_status == null)
                {
                    missing.Add(nameof(m_status));
                }

                if (m_count == null)
                {
                    missing.Add(nameof(m_count));
                }

                if (m_remove == null)
                {
                    missing.Add(nameof(m_remove));
                }

                Debug.LogError($"[{VIEW_NAME}] Initialize 실패: 누락/불일치 " + string.Join(", ", missing));
            }

            return false;
        }

        private static void SetText(TMP_Text _text, string _value)
        {
            if (_text != null && _text.text != _value)
            {
                _text.text = _value;
            }
        }

        private static void SetActive(GameObject _target, bool _active)
        {
            if (_target != null && _target.activeSelf != _active)
            {
                _target.SetActive(_active);
            }
        }
    }
}
