using System;

using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace WeaponWorkshop
{
    /// <summary>
    /// 레시피 패널의 컨트롤 참조 보관, 시작 숨김 초기화, 레시피 보기 버튼 입력 전달,
    /// 인라인 높이 측정·적용만 담당하는 View입니다. 표 해석과 표시 정책은 RecipePresenter가 소유합니다.
    /// </summary>
    public sealed class RecipeView : MonoBehaviour
    {
        [Serializable]
        public sealed class RecipeEntry
        {
            public string tagId;
            public TMP_Text title;
            public TagPillGraphic badge;
            public TMP_Text conditions;
        }

        [SerializeField] private GameObject panel;
        [SerializeField] private Button openButton;
        [SerializeField] private TMP_Text buttonLabel;
        [SerializeField] private RectTransform weaponInfo;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private LayoutElement inlineLayout;
        [SerializeField] private RecipeEntry[] recipes = new RecipeEntry[0];

        private readonly Vector3[] m_canvasCorners = new Vector3[4];
        private GameObject m_savedFocus;
        private bool m_isInitialized;
        private bool m_isButtonBound;

        public event Action ToggleRequested;

        public bool IsPanelOpen => panel != null && panel.activeSelf;

        public bool IsWeaponInfoVisible => weaponInfo != null && weaponInfo.gameObject.activeInHierarchy;

        public RectTransform TutorialButton => openButton != null ? openButton.transform as RectTransform : null;

        public int RecipeCount => recipes != null ? recipes.Length : 0;

        private void Awake()
        {
            // Bridge가 먼저 Initialize한 뒤 늦게 실행되는 Awake가 설정된 화면을 다시 숨기지 않게 한다.
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
        /// 씬 오버라이드가 적용된 브리지의 유효 참조를 Initialize 전에 주입합니다. Initialize 전 호출을 전제로 하며,
        /// 이미 버튼이 묶여 있으면 참조 교체 후 다시 묶이도록 해제합니다.
        /// </summary>
        public void BindReferences(GameObject _panel, Button _openButton, TMP_Text _buttonLabel,
            RectTransform _weaponInfo, ScrollRect _scroll, LayoutElement _inlineLayout, RecipeEntry[] _recipes)
        {
            UnbindButton();
            panel = _panel;
            openButton = _openButton;
            buttonLabel = _buttonLabel;
            weaponInfo = _weaponInfo;
            scroll = _scroll;
            inlineLayout = _inlineLayout;
            recipes = _recipes ?? new RecipeEntry[0];
        }

        /// <summary>
        /// 필수 참조를 검증하고 시작 숨김을 적용한 뒤 레시피 보기 버튼 클릭을 ToggleRequested로 전달합니다.
        /// 중복 호출은 초기화된 상태를 유지합니다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            ApplyInitialHiddenState();
            if (panel == null || openButton == null || buttonLabel == null || weaponInfo == null ||
                scroll == null || scroll.content == null || inlineLayout == null)
            {
                Debug.LogError(
                    "[RecipeView] Initialize 실패: 필수 참조 누락 " +
                    $"(panel={panel != null}, openButton={openButton != null}, buttonLabel={buttonLabel != null}, " +
                    $"weaponInfo={weaponInfo != null}, scroll={scroll != null}, scroll.content={scroll != null && scroll.content != null}, " +
                    $"inlineLayout={inlineLayout != null})",
                    this);
                return false;
            }

            openButton.onClick.AddListener(OnButtonClicked);
            m_isButtonBound = true;
            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 버튼 클릭 리스너를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            UnbindButton();
            m_isInitialized = false;
        }

        public string GetRecipeTagId(int _index)
        {
            if (recipes == null || _index < 0 || _index >= recipes.Length || recipes[_index] == null)
            {
                return null;
            }

            return recipes[_index].tagId;
        }

        public void SetRecipeEntry(int _index, string _title, Color _color, string _conditions)
        {
            if (recipes == null || _index < 0 || _index >= recipes.Length || recipes[_index] == null)
            {
                return;
            }

            RecipeEntry entry = recipes[_index];
            if (entry.title != null)
            {
                entry.title.text = _title;
            }

            if (entry.badge != null)
            {
                entry.badge.color = _color;
            }

            if (entry.conditions != null)
            {
                entry.conditions.text = _conditions;
            }
        }

        public void SetPanelVisible(bool _visible)
        {
            if (panel == null || panel.activeSelf == _visible)
            {
                return;
            }

            if (_visible)
            {
                UIRoot.SaveFocus(ref m_savedFocus, transform);
            }

            panel.SetActive(_visible);

            if (_visible)
            {
                Selectable defaultSelectable = (scroll != null && scroll.verticalScrollbar != null)
                    ? scroll.verticalScrollbar
                    : (Selectable)openButton;
                UIRoot.SetFocus(defaultSelectable, openButton != null ? new Selectable[] { openButton } : null);
            }
            else
            {
                UIRoot.RestoreFocus(ref m_savedFocus, openButton);
            }
        }

        public void SetButtonActive(bool _active)
        {
            if (openButton != null && openButton.gameObject.activeSelf != _active)
            {
                openButton.gameObject.SetActive(_active);
            }
        }

        public void SetButtonLabel(string _label)
        {
            if (buttonLabel != null)
            {
                buttonLabel.text = _label;
            }
        }

        public void ResetScroll()
        {
            if (scroll == null)
            {
                return;
            }

            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>
        /// 열려 있는 동안 현재 사이드바·내용 높이로 인라인 높이를 맞춥니다.
        /// _forceRebuild가 참일 때만 사이드바 폭 기준 줄바꿈 레이아웃을 강제로 다시 계산합니다.
        /// </summary>
        public void ApplyInlineLayout(bool _forceRebuild)
        {
            if (!m_isInitialized || panel == null || weaponInfo == null || scroll == null ||
                scroll.content == null || inlineLayout == null)
            {
                return;
            }

            if (_forceRebuild)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(weaponInfo);
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            }

            Canvas canvas = weaponInfo.GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas != null ? canvas.rootCanvas.GetComponent<RectTransform>() : null;
            if (canvasRect == null)
            {
                return;
            }

            canvasRect.GetWorldCorners(m_canvasCorners);
            float bottom = weaponInfo.InverseTransformPoint(m_canvasCorners[0]).y + 200f;
            // Exclude the old recipe height when calculating remaining screen space.
            RectTransform panelRect = panel.transform as RectTransform;
            float headerHeight = weaponInfo.rect.height - (panelRect != null ? panelRect.rect.height : 0f);
            float available = Mathf.Max(0f, weaponInfo.rect.yMax - headerHeight - bottom);
            float preferred = LayoutUtility.GetPreferredHeight(scroll.content);
            float height = Mathf.Min(preferred, available);
            if (!Mathf.Approximately(inlineLayout.preferredHeight, height))
            {
                inlineLayout.preferredHeight = height;
            }
        }

        private void ApplyInitialHiddenState()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }

            if (openButton != null)
            {
                openButton.gameObject.SetActive(false);
            }
        }

        private void UnbindButton()
        {
            if (!m_isButtonBound)
            {
                return;
            }

            if (openButton != null)
            {
                openButton.onClick.RemoveListener(OnButtonClicked);
            }

            m_isButtonBound = false;
        }

        private void OnButtonClicked()
        {
            ToggleRequested?.Invoke();
        }
    }
}
