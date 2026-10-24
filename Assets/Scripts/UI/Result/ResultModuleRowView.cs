using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 습격 결과 화면 아래쪽의 획득 모듈 아이콘 줄. 저작된 줄·아이콘 템플릿을 재사용하고 개수에 맞춰 배치한다.
    public sealed class ResultModuleRowView : MonoBehaviour
    {
        private const string VIEW_NAME = "ResultModuleRowView";
        // 1920×1080 기준 값(기존 2560 기준 값의 0.75배).
        private const float MAX_SPACING = 54f;
        private const float ROW_CONTENT_WIDTH = 487.5f;
        private const float MAX_ICON_SIZE = 45f;
        private const float ICON_GAP = 6f;
        private const float BODY_SHIFT = 36f;
        private const float BODY_SHRINK = 72f;

        [SerializeField] private RectTransform m_row;
        [SerializeField] private Image m_iconTemplate;
        [SerializeField] private TMPro.TextMeshProUGUI m_body;

        private readonly List<Image> m_icons = new List<Image>();
        private bool m_hasBodyBase;
        private Vector2 m_bodyPosition;
        private Vector2 m_bodySize;
        private bool m_hasLoggedReferenceError;

        private void Awake()
        {
            if (m_row != null && m_icons.Count == 0)
            {
                m_row.gameObject.SetActive(false);
            }

            if (m_iconTemplate != null)
            {
                m_iconTemplate.gameObject.SetActive(false);
            }
        }

        public void Show(IReadOnlyList<int> _modules, Func<int, Sprite> _iconOf, Func<int, ModuleData> _dataOf, HoveringUI _tooltip)
        {
            if (!ValidateReferences())
            {
                return;
            }

            CaptureBodyBase();
            int count = _modules != null ? _modules.Count : 0;
            RectTransform bodyRect = m_body.rectTransform;
            bodyRect.anchoredPosition = count > 0 ? m_bodyPosition + new Vector2(0f, BODY_SHIFT) : m_bodyPosition;
            bodyRect.sizeDelta = count > 0 ? new Vector2(m_bodySize.x, m_bodySize.y - BODY_SHRINK) : m_bodySize;
            if (count == 0)
            {
                Hide();
                return;
            }

            while (m_icons.Count < count)
            {
                Image icon = Instantiate(m_iconTemplate, m_row, false);
                icon.name = "ModuleIcon";
                m_icons.Add(icon);
            }

            float spacing = Mathf.Min(MAX_SPACING, ROW_CONTENT_WIDTH / count);
            float size = Mathf.Min(MAX_ICON_SIZE, spacing - ICON_GAP);
            for (int i = 0; i < m_icons.Count; i++)
            {
                Image icon = m_icons[i];
                bool used = i < count;
                icon.gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                int moduleId = _modules[i];
                icon.sprite = _iconOf != null ? _iconOf(moduleId) : null;
                icon.color = Color.white;
                ResultModuleIconHover hover = icon.GetComponent<ResultModuleIconHover>();
                if (hover != null)
                {
                    hover.Bind(_tooltip, _dataOf != null ? _dataOf(moduleId) : null);
                }

                RectTransform rect = icon.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * spacing, 0f);
                rect.sizeDelta = new Vector2(size, size);
            }

            m_row.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (m_row != null)
            {
                m_row.gameObject.SetActive(false);
            }

            if (m_hasBodyBase && m_body != null)
            {
                m_body.rectTransform.anchoredPosition = m_bodyPosition;
                m_body.rectTransform.sizeDelta = m_bodySize;
            }
        }

        private void CaptureBodyBase()
        {
            if (m_hasBodyBase)
            {
                return;
            }

            m_bodyPosition = m_body.rectTransform.anchoredPosition;
            m_bodySize = m_body.rectTransform.sizeDelta;
            m_hasBodyBase = true;
        }

        private bool ValidateReferences()
        {
            if (m_row != null && m_iconTemplate != null && m_body != null)
            {
                return true;
            }

            if (!m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError("[" + VIEW_NAME + "] 필수 참조 누락: row=" + (m_row != null) + ", iconTemplate=" + (m_iconTemplate != null) +
                    ", body=" + (m_body != null) + ". Tools/UI/Authoring/Raid/ResultModules를 실행하세요.", this);
            }

            return false;
        }
    }
}
