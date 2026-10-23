using System.Collections.Generic;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 주문 요구사항 HUD 행의 저작 참조와 표시 함수만 담당한다.
    // 요청 판정과 문구 가공은 CustomerRequirementsPresenter가 맡는다.
    public sealed class CustomerRequirementsView : MonoBehaviour
    {
        private const string SCREEN_NAME = "CustomerRequirementsView";

        [SerializeField] private RectTransform m_content;
        [SerializeField] private RectTransform m_rowTemplate;
        [SerializeField] private QuestInfoTextSizing m_textSizing;

        private readonly List<RowEntry> m_rows = new List<RowEntry>();
        private int m_used;
        private bool m_isInitialized;
        private bool m_hasLoggedReferenceError;

        public bool HasLayout
        {
            get
            {
                return m_content != null && m_rowTemplate != null;
            }
        }

        public readonly struct RowData
        {
            public RowData(string _text, bool _met, int _depth, bool _heading = false)
            {
                Text = _text;
                Met = _met;
                Depth = _depth;
                Heading = _heading;
            }

            public string Text { get; }

            public bool Met { get; }

            public int Depth { get; }

            public bool Heading { get; }
        }

        private readonly struct RowEntry
        {
            public RowEntry(RectTransform _rect, TMP_Text _status, TMP_Text _description)
            {
                Rect = _rect;
                Status = _status;
                Description = _description;
            }

            public RectTransform Rect { get; }

            public TMP_Text Status { get; }

            public TMP_Text Description { get; }
        }

        private void Awake()
        {
            if (!m_isInitialized)
            {
                ApplyInitialHiddenState();
            }
        }

        /// <summary>
        /// 브리지가 보유한 실효 참조(프리팹 값과 씬 오버라이드 반영)를 View 직렬화 참조로 전달합니다.
        /// </summary>
        public void BindReferences(RectTransform _content, RectTransform _rowTemplate,
            QuestInfoTextSizing _textSizing)
        {
            m_content = _content;
            m_rowTemplate = _rowTemplate;
            m_textSizing = _textSizing;
        }

        /// <summary>
        /// 시작 숨김을 적용하고 필수 참조를 검증합니다. 중복 호출은 초기화된 상태를 유지합니다.
        /// </summary>
        public bool Initialize()
        {
            ApplyInitialHiddenState();
            if (m_isInitialized)
            {
                return true;
            }

            if (!ValidateReferences())
            {
                return false;
            }

            m_isInitialized = true;
            return true;
        }

        public void Dispose()
        {
            HideRows();
            m_isInitialized = false;
        }

        public void ShowRows(IReadOnlyList<RowData> _rows)
        {
            if (!HasLayout)
            {
                return;
            }

            m_used = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                ShowRow(_rows[i]);
            }

            for (int i = m_used; i < m_rows.Count; i++)
            {
                if (m_rows[i].Rect.gameObject.activeSelf)
                {
                    m_rows[i].Rect.gameObject.SetActive(false);
                    MarkDirty();
                }
            }
        }

        public void HideRows()
        {
            foreach (RowEntry row in m_rows)
            {
                if (row.Rect.gameObject.activeSelf)
                {
                    row.Rect.gameObject.SetActive(false);
                    MarkDirty();
                }
            }

            m_used = 0;
        }

        private void ShowRow(RowData _data)
        {
            if (m_used == m_rows.Count)
            {
                RectTransform rect = Instantiate(m_rowTemplate, m_content);
                // 저작 템플릿은 상태 라벨이 먼저, 줄바꿈 설명이 두 번째다.
                TMP_Text[] labels = rect.GetComponentsInChildren<TMP_Text>(true);
                m_rows.Add(new RowEntry(rect, labels[0], labels[1]));
            }

            RowEntry row = m_rows[m_used++];
            HorizontalLayoutGroup layout = row.Rect.GetComponent<HorizontalLayoutGroup>();
            string status = _data.Heading ? string.Empty : _data.Met ? "[O]" : "[ ]";
            bool changed = !row.Rect.gameObject.activeSelf ||
                row.Description.text != _data.Text ||
                row.Status.text != status ||
                row.Status.gameObject.activeSelf == _data.Heading ||
                row.Description.fontStyle != (_data.Heading ? FontStyles.Bold : FontStyles.Normal) ||
                layout.padding.left != Mathf.RoundToInt(_data.Depth * 10.5f);
            row.Rect.gameObject.SetActive(true);
            layout.padding.left = Mathf.RoundToInt(_data.Depth * 10.5f);
            Color color = _data.Heading ? Color.white : _data.Met ? new Color(.5f, .88f, .65f) : new Color(1f, .63f, .56f);
            row.Status.text = status;
            row.Status.gameObject.SetActive(!_data.Heading);
            row.Status.color = row.Description.color = color;
            row.Description.text = _data.Text;
            row.Description.fontStyle = _data.Heading ? FontStyles.Bold : FontStyles.Normal;
            if (changed)
            {
                MarkDirty();
            }
        }

        private void ApplyInitialHiddenState()
        {
            if (m_rowTemplate != null)
            {
                m_rowTemplate.gameObject.SetActive(false);
            }

            HideRows();
        }

        private bool ValidateReferences()
        {
            bool isValid = HasLayout;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    $"[{SCREEN_NAME}] Initialize 실패: 필수 참조 누락 " +
                    $"(m_content={m_content != null}, m_rowTemplate={m_rowTemplate != null})",
                    this);
            }

            return isValid;
        }

        private void MarkDirty()
        {
            if (m_textSizing != null)
            {
                m_textSizing.MarkDirty();
            }
        }
    }
}
