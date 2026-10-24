using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

using TMPro;

namespace WeaponWorkshop
{
    // 스티커 패널의 목록 타일과 조절 버튼 표시를 담당하는 View다.
    // 스티커 목록은 저작된 타일을 재사용하고, 데이터가 더 많으면 마지막 타일을 템플릿으로 복제한다.
    public sealed class StickerPanelView : MonoBehaviour
    {
        private const string VIEW_NAME = "StickerPanelView";
        private static readonly Color ERASE_ACTIVE_COLOR = new Color(0.62f, 0.28f, 0.25f, 1f);
        private static readonly Color ERASE_IDLE_COLOR = new Color(0.3f, 0.25f, 0.25f, 1f);
        private static readonly Color MIRROR_ACTIVE_COLOR = new Color(0.3f, 0.42f, 0.32f, 1f);
        private static readonly Color MIRROR_IDLE_COLOR = new Color(0.18f, 0.22f, 0.25f, 1f);

        [SerializeField] private Button[] m_buttons = Array.Empty<Button>();
        [SerializeField] private Outline[] m_highlights = Array.Empty<Outline>();
        [SerializeField] private TMP_Text m_status;
        [SerializeField] private TMP_Text m_empty;
        [SerializeField] private Button m_undo;
        [SerializeField] private Button m_erase;
        [SerializeField] private Button m_mirror;
        [SerializeField] private TMP_Text m_mirrorLabel;
        [SerializeField] private Button m_sizeDown;
        [SerializeField] private TMP_Text m_sizeLabel;
        [SerializeField] private Button m_sizeUp;

        private readonly List<GameObject> m_clones = new List<GameObject>();
        private Texture2D[] m_textures = Array.Empty<Texture2D>();
        private UnityAction[] m_buttonActions;
        private bool m_isInitialized;
        private bool m_hasLoggedReferenceError;

        public event Action<int> StickerRequested;
        public event Action UndoRequested;
        public event Action EraseRequested;
        public event Action MirrorRequested;
        public event Action SizeDownRequested;
        public event Action SizeUpRequested;

        public bool IsInitialized => m_isInitialized;

        public void Configure(Button[] _buttons, Outline[] _highlights, TMP_Text _status, TMP_Text _empty,
            Button _undo, Button _erase, Button _mirror, TMP_Text _mirrorLabel,
            Button _sizeDown, TMP_Text _sizeLabel, Button _sizeUp, Texture2D[] _textures)
        {
            m_buttons = _buttons != null ? _buttons : Array.Empty<Button>();
            m_highlights = _highlights != null ? _highlights : Array.Empty<Outline>();
            m_status = _status;
            m_empty = _empty;
            m_undo = _undo;
            m_erase = _erase;
            m_mirror = _mirror;
            m_mirrorLabel = _mirrorLabel;
            m_sizeDown = _sizeDown;
            m_sizeLabel = _sizeLabel;
            m_sizeUp = _sizeUp;
            m_textures = _textures != null ? _textures : Array.Empty<Texture2D>();
        }

        // 저작 타일을 필요한 수만큼 확보하고 썸네일·이름·입력을 연결한다.
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

            EnsureTileCount(m_textures.Length);
            m_buttonActions = new UnityAction[m_buttons.Length];
            for (int i = 0; i < m_buttons.Length; i++)
            {
                int index = i;
                bool available = i < m_textures.Length;
                SetActive(m_buttons[i].gameObject, available);
                if (!available)
                {
                    continue;
                }

                RawImage preview = m_buttons[i].GetComponentInChildren<RawImage>(true);
                if (preview != null)
                {
                    preview.texture = m_textures[i];
                }

                TMP_Text label = m_buttons[i].GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = m_textures[i].name;
                }

                UnityAction action = () => StickerRequested?.Invoke(index);
                m_buttonActions[i] = action;
                m_buttons[i].onClick.AddListener(action);
            }

            m_undo.onClick.AddListener(OnUndo);
            m_erase.onClick.AddListener(OnErase);
            m_mirror.onClick.AddListener(OnMirror);
            m_sizeDown.onClick.AddListener(OnSizeDown);
            m_sizeUp.onClick.AddListener(OnSizeUp);
            SetActive(m_empty.gameObject, m_textures.Length == 0);
            m_isInitialized = true;
            return true;
        }

        public void Dispose()
        {
            if (!m_isInitialized)
            {
                return;
            }

            for (int i = 0; i < m_buttonActions.Length; i++)
            {
                if (m_buttons[i] != null && m_buttonActions[i] != null)
                {
                    m_buttons[i].onClick.RemoveListener(m_buttonActions[i]);
                }
            }

            m_undo.onClick.RemoveListener(OnUndo);
            m_erase.onClick.RemoveListener(OnErase);
            m_mirror.onClick.RemoveListener(OnMirror);
            m_sizeDown.onClick.RemoveListener(OnSizeDown);
            m_sizeUp.onClick.RemoveListener(OnSizeUp);
            DestroyClones();
            m_buttonActions = null;
            m_isInitialized = false;
        }

        // Presenter가 계산한 한 프레임 표시 값을 적용한다.
        public void Render(StickerPanelState _state)
        {
            if (!m_isInitialized || _state == null)
            {
                return;
            }

            SetActive(m_empty.gameObject, _state.ShowEmpty);
            for (int i = 0; i < m_buttons.Length; i++)
            {
                if (i < m_highlights.Length && m_highlights[i] != null)
                {
                    m_highlights[i].enabled = i < m_textures.Length && i == _state.SelectedIndex;
                }
            }

            SetText(m_status, _state.Status);
            m_undo.interactable = _state.CanUndo;
            m_erase.interactable = _state.CanErase;
            if (m_erase.targetGraphic != null)
            {
                m_erase.targetGraphic.color = _state.EraseActive ? ERASE_ACTIVE_COLOR : ERASE_IDLE_COLOR;
            }

            m_mirror.interactable = _state.CanAdjust;
            SetText(m_mirrorLabel, _state.MirrorLabel);
            if (m_mirror.targetGraphic != null)
            {
                m_mirror.targetGraphic.color = _state.MirrorActive ? MIRROR_ACTIVE_COLOR : MIRROR_IDLE_COLOR;
            }

            SetText(m_sizeLabel, _state.SizeLabel);
            m_sizeDown.interactable = _state.CanDecreaseSize;
            m_sizeUp.interactable = _state.CanIncreaseSize;
        }

        private void Awake()
        {
            // 목록 유무는 Initialize/Render가 정한다. 시작 시에는 빈 안내를 숨긴다.
            if (!m_isInitialized && m_empty != null)
            {
                m_empty.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void OnUndo()
        {
            UndoRequested?.Invoke();
        }

        private void OnErase()
        {
            EraseRequested?.Invoke();
        }

        private void OnMirror()
        {
            MirrorRequested?.Invoke();
        }

        private void OnSizeDown()
        {
            SizeDownRequested?.Invoke();
        }

        private void OnSizeUp()
        {
            SizeUpRequested?.Invoke();
        }

        // 저작된 마지막 타일을 템플릿으로 복제해 필요한 슬롯 수를 맞춘다.
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
                !Array.Exists(m_highlights, _highlight => _highlight == null) &&
                !Array.Exists(m_textures, _texture => _texture == null);
            if (arraysValid && m_status != null && m_empty != null && m_undo != null && m_erase != null &&
                m_mirror != null && m_mirrorLabel != null && m_sizeDown != null && m_sizeLabel != null &&
                m_sizeUp != null)
            {
                return true;
            }

            if (!m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                List<string> missing = new List<string>();
                if (!arraysValid)
                {
                    missing.Add(nameof(m_buttons) + "/" + nameof(m_highlights) + "(또는 " + nameof(m_textures) +
                        " 누락)");
                }

                if (m_status == null)
                {
                    missing.Add(nameof(m_status));
                }

                if (m_empty == null)
                {
                    missing.Add(nameof(m_empty));
                }

                if (m_undo == null)
                {
                    missing.Add(nameof(m_undo));
                }

                if (m_erase == null)
                {
                    missing.Add(nameof(m_erase));
                }

                if (m_mirror == null)
                {
                    missing.Add(nameof(m_mirror));
                }

                if (m_mirrorLabel == null)
                {
                    missing.Add(nameof(m_mirrorLabel));
                }

                if (m_sizeDown == null)
                {
                    missing.Add(nameof(m_sizeDown));
                }

                if (m_sizeLabel == null)
                {
                    missing.Add(nameof(m_sizeLabel));
                }

                if (m_sizeUp == null)
                {
                    missing.Add(nameof(m_sizeUp));
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
