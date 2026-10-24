using System;
using System.Collections.Generic;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 공방 태그·스탯 패널의 저작된 참조, 시작 숨김, 태그 배지 템플릿 표시를 담당하는 View입니다.
    /// 태그 판정·가격 계산·습격 보상은 Presenter(또는 RaidTempRewardPresenter)가 다룹니다.
    /// </summary>
    public sealed class WorkshopTagsView : MonoBehaviour
    {
        [SerializeField] private GameObject m_panel;
        [SerializeField] private TMP_Text m_weightText;
        [SerializeField] private TMP_Text m_salePriceText;
        [SerializeField] private TMP_Text m_tagsHeaderText;
        [SerializeField] private RectTransform m_tagContainer;
        [SerializeField] private Image m_tagTemplate;
        [SerializeField] private TMP_Text m_noTagsText;

        private readonly List<(Image image, TMP_Text label)> m_tagBadges =
            new List<(Image, TMP_Text)>();

        private bool m_isInitialized;
        private bool m_isListenerBound;
        private bool m_hasLoggedReferenceError;

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
        /// 씬 오버라이드가 적용된 브리지의 유효 참조를 Initialize 전에 주입합니다.
        /// </summary>
        public void BindReferences(GameObject _panel, TMP_Text _weightText, TMP_Text _salePriceText,
            TMP_Text _tagsHeaderText, RectTransform _tagContainer, Image _tagTemplate, TMP_Text _noTagsText)
        {
            Unbind();
            m_panel = _panel;
            m_weightText = _weightText;
            m_salePriceText = _salePriceText;
            m_tagsHeaderText = _tagsHeaderText;
            m_tagContainer = _tagContainer;
            m_tagTemplate = _tagTemplate;
            m_noTagsText = _noTagsText;
        }

        /// <summary>
        /// 시작 숨김 상태를 적용합니다. 중복 호출에 안전합니다.
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

            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 리소스를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            Unbind();
            m_isInitialized = false;
        }

        public void SetPanelVisible(bool _visible)
        {
            if (m_panel != null)
            {
                m_panel.SetActive(_visible);
            }

            // 무게 표시는 공방에서 코어가 있을 때만 보인다(습격 보상 표시 중에는 숨김).
            if (m_weightText != null)
            {
                m_weightText.gameObject.SetActive(_visible);
            }
        }

        public void SetWeightText(string _text)
        {
            if (m_weightText != null)
            {
                m_weightText.text = _text ?? string.Empty;
            }
        }

        public void SetSalePriceText(string _text)
        {
            if (m_salePriceText != null)
            {
                m_salePriceText.text = _text ?? string.Empty;
            }
        }

        public void SetTagsHeaderText(string _text)
        {
            if (m_tagsHeaderText != null)
            {
                m_tagsHeaderText.text = _text ?? string.Empty;
            }
        }

        /// <summary>
        /// 태그 배지를 갱신합니다. 데이터 수가 변하므로 템플릿 복제 풀을 사용합니다.
        /// </summary>
        public void UpdateTagBadges(List<CustomerRequestData.Tag> _tags)
        {
            if (m_tagContainer == null || m_tagTemplate == null)
            {
                if (m_tagContainer != null)
                {
                    m_tagContainer.gameObject.SetActive(false);
                }
                if (m_noTagsText != null)
                {
                    m_noTagsText.gameObject.SetActive(false);
                }
                return;
            }

            int used = 0;
            if (_tags != null)
            {
                foreach (var tag in _tags)
                {
                    if (tag == null)
                    {
                        continue;
                    }

                    if (used == m_tagBadges.Count)
                    {
                        var image = Instantiate(m_tagTemplate, m_tagContainer);
                        var label = image.GetComponentInChildren<TMP_Text>(true);
                        m_tagBadges.Add((image, label));
                    }

                    var badge = m_tagBadges[used++];
                    badge.image.gameObject.SetActive(true);
                    badge.image.color = tag.color;
                    badge.label.text = tag.name;
                    Color linear = tag.color.linear;
                    float luminance = 0.2126f * linear.r + 0.7152f * linear.g + 0.0722f * linear.b;
                    badge.label.color = luminance > 0.179f ? Color.black : Color.white;
                }
            }

            for (int i = used; i < m_tagBadges.Count; i++)
            {
                m_tagBadges[i].image.gameObject.SetActive(false);
            }

            m_tagContainer.gameObject.SetActive(used > 0);
            if (m_noTagsText != null)
            {
                m_noTagsText.gameObject.SetActive(used == 0);
            }
        }

        /// <summary>
        /// 습격 임시 보상용: 달러·부품·티켓 텍스트와 모듈 아이콘을 표시합니다.
        /// </summary>
        public void ShowRaidRewards(int _dollars, int _parts, int _tickets,
            IReadOnlyList<int> _modules, Sprite _raidFallbackIcon, RaidModuleIcon[] _raidModuleIcons)
        {
            if (m_salePriceText != null)
            {
                m_salePriceText.text = $"$ {_dollars:N0}";
            }

            // 공방의 무게 표시와 같은 텍스트를 쓰므로 습격 중에는 숨긴다.
            if (m_weightText != null)
            {
                m_weightText.gameObject.SetActive(false);
            }

            if (m_tagsHeaderText != null)
            {
                m_tagsHeaderText.text = $"티켓  {_tickets:N0}";
            }

            if (m_tagTemplate == null || m_tagContainer == null)
            {
                if (m_tagContainer != null)
                {
                    m_tagContainer.gameObject.SetActive(false);
                }
                return;
            }

            int count = _modules != null ? _modules.Count : 0;
            var raidIcons = new List<Image>();

            while (raidIcons.Count < count)
            {
                Image icon = Instantiate(m_tagTemplate, m_tagContainer);
                TMP_Text label = icon.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = "Module";
                    Color labelColor = label.color;
                    labelColor.a = 0f;
                    label.color = labelColor;
                }

                icon.raycastTarget = false;
                raidIcons.Add(icon);
            }

            for (int i = 0; i < raidIcons.Count; i++)
            {
                Image icon = raidIcons[i];
                bool visible = i < count;
                icon.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                int moduleId = _modules[i];
                Sprite sprite = null;
                Color tint = Color.white;
                foreach (RaidModuleIcon mapping in _raidModuleIcons)
                {
                    if (mapping.moduleId == moduleId)
                    {
                        sprite = mapping.sprite;
                        if (mapping.tint)
                        {
                            tint = mapping.tintColor;
                        }
                        break;
                    }
                }

                icon.sprite = sprite != null ? sprite : _raidFallbackIcon;
                icon.color = sprite != null ? tint : Color.HSVToRGB(Mathf.Repeat(moduleId * 0.618034f, 1f), 0.48f, 0.96f);
                icon.preserveAspect = true;
            }

            m_tagContainer.gameObject.SetActive(count > 0);
            if (m_noTagsText != null)
            {
                m_noTagsText.gameObject.SetActive(false);
            }
        }

        public void LeaveRaidRewards()
        {
            if (m_tagContainer != null)
            {
                m_tagContainer.gameObject.SetActive(false);
            }

            if (m_noTagsText != null)
            {
                m_noTagsText.gameObject.SetActive(false);
            }

            if (m_tagsHeaderText != null)
            {
                m_tagsHeaderText.text = "활성 태그";
            }
        }

        private void ApplyInitialHiddenState()
        {
            if (m_panel != null)
            {
                m_panel.SetActive(false);
            }

            if (m_tagsHeaderText != null)
            {
                m_tagsHeaderText.text = "활성 태그";
            }

            if (m_tagTemplate != null)
            {
                m_tagTemplate.gameObject.SetActive(false);
            }

            if (m_tagContainer != null)
            {
                m_tagContainer.gameObject.SetActive(false);
            }

            if (m_noTagsText != null)
            {
                m_noTagsText.gameObject.SetActive(false);
            }
        }

        private bool ValidateReferences()
        {
            bool isValid = m_panel != null && m_weightText != null && m_salePriceText != null &&
                m_tagsHeaderText != null && m_tagContainer != null && m_tagTemplate != null &&
                m_noTagsText != null;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    "[WorkshopTagsView] Initialize 실패: 필수 참조 누락 " +
                    $"(panel={m_panel != null}, weightText={m_weightText != null}, " +
                    $"salePriceText={m_salePriceText != null}, tagsHeaderText={m_tagsHeaderText != null}, " +
                    $"tagContainer={m_tagContainer != null}, tagTemplate={m_tagTemplate != null}, " +
                    $"noTagsText={m_noTagsText != null}). " +
                    "Tools/UI/Authoring/PresetShare/Author 실행 결과를 확인하세요.",
                    this);
            }

            return isValid;
        }

        private void Unbind()
        {
            if (!m_isListenerBound)
            {
                return;
            }
            m_isListenerBound = false;
        }
    }

    /// <summary>
    /// 습격 보상용 모듈 아이콘 매핑 데이터.
    /// </summary>
    [Serializable]
    public struct RaidModuleIcon
    {
        public int moduleId;
        public Sprite sprite;
        public bool tint;
        public Color tintColor;
    }
}