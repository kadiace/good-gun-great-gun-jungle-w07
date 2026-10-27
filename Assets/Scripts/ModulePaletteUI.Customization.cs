using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 커스텀(도색·스티커·키링) 브리지. 저작된 View에 실효 참조를 넘기고 Presenter를 연결한다.
    // 화면 표시·선택 상태는 View/Presenter가 담당하며, 이 파일은 기존 직렬화 필드·공개 API 호환만 유지한다.
    public sealed partial class ModulePaletteUI
    {
        [SerializeField] private CustomizationView customView;
        [SerializeField] private PaintPanelView paintPanelView;
        [SerializeField] private StickerPanelView stickerPanelView;
        [SerializeField] private KeyringPanelView keyringPanelView;

        private CustomizationPresenter customPresenter;

        private void BindCustomUI()
        {
            if (!TryBindCustomViews())
            {
                return;
            }

            ConfigureCustomViews();
            customView.Initialize();
            paintPanelView.Initialize();
            stickerPanelView.Initialize();
            keyringPanelView.Initialize(keyringModels, GetKeyringEmptyMessage());

            customPresenter = new CustomizationPresenter(customView, paintPanelView, stickerPanelView,
                keyringPanelView, this, activePaintMaterials, generatedPaintMaterials, stickerTextures,
                keyringModels, ResolveKeyringModels, GetKeyringEmptyMessage,
                keyringPanel != null ? keyringPanel.gameObject : null);
            if (!customPresenter.Initialize())
            {
                customPresenter = null;
                return;
            }

            customPresenter.Bind(builder);
            RefreshCustomNavigation();
            UpdateCustomPanelVisibility(false);
            RefreshPaintPanel();
            RefreshStickerPanel();
            RefreshKeyringPanel();
        }

        // 저작된 View 컴포넌트를 찾는다. 없으면 생성하지 않고 누락 필드를 알린다.
        private bool TryBindCustomViews()
        {
            if (customView == null)
            {
                customView = GetComponent<CustomizationView>();
            }

            if (paintPanelView == null && paintPanel != null)
            {
                paintPanelView = paintPanel.GetComponent<PaintPanelView>();
            }

            if (stickerPanelView == null && stickerPanel != null)
            {
                stickerPanelView = stickerPanel.GetComponent<StickerPanelView>();
            }

            if (keyringPanelView == null && keyringPanel != null)
            {
                keyringPanelView = keyringPanel.GetComponent<KeyringPanelView>();
            }

            if (customView != null && paintPanelView != null && stickerPanelView != null &&
                keyringPanelView != null)
            {
                return true;
            }

            List<string> missing = new List<string>();
            if (customView == null)
            {
                missing.Add(nameof(customView));
            }

            if (paintPanelView == null)
            {
                missing.Add(nameof(paintPanelView));
            }

            if (stickerPanelView == null)
            {
                missing.Add(nameof(stickerPanelView));
            }

            if (keyringPanelView == null)
            {
                missing.Add(nameof(keyringPanelView));
            }

            Debug.LogError("[ModulePaletteUI] 커스텀 화면 초기화 실패: 누락 " + string.Join(", ", missing) +
                " (Tools/UI/Authoring/Customization/Author 실행 필요)");
            return false;
        }

        // 브리지의 기존 직렬화 필드 값을 View에 옮긴다. 씬 오버라이드가 반영된 값이 그대로 전달된다.
        private void ConfigureCustomViews()
        {
            customView.Configure(
                customButtonRoot != null ? customButtonRoot.GetComponent<Button>() : null,
                customNavigation != null ? customNavigation.gameObject : null,
                customContentArea != null ? customContentArea.gameObject : null,
                customContentPrompt != null ? customContentPrompt.gameObject : null,
                customRemoveButton,
                BuildCustomPanelButtons(),
                BuildCustomPanelObjects());

            TMPro.TMP_Text applyLabel = applyCustomColorButton != null
                ? applyCustomColorButton.GetComponentInChildren<TMPro.TMP_Text>(true)
                : null;
            paintPanelView.Configure(paintTitle, previousPaintPageButton, nextPaintPageButton, paintPageLabel,
                paintMaterialButtons.ToArray(), paintMaterialSwatches.ToArray(), BuildPaintSlotLabels(),
                paintMaterialHighlights.ToArray(), customColorModeButton,
                materialSelectionView != null ? materialSelectionView.gameObject : null,
                customColorPicker != null ? customColorPicker.gameObject : null,
                paintPreview, paintName, paintStatus, customColorPreview, customColorHex, rgbInputs,
                colorSpectrum, hueSpectrum, applyCustomColorButton, applyLabel, backToMaterialsButton,
                paintUndoButton, paintFillButton, paintAllButton);

            stickerPanelView.Configure(stickerButtons.ToArray(), stickerHighlights.ToArray(), stickerStatus,
                stickerEmptyLabel, stickerUndoButton, stickerRemoveButton, stickerMirrorButton,
                stickerMirrorButtonLabel, stickerSizeDownButton, stickerSizeLabel, stickerSizeUpButton,
                stickerTextures.ToArray());

            keyringPanelView.Configure(keyringModelButtons.ToArray(), keyringModelHighlights.ToArray(),
                keyringEmptyLabel, keyringStatus, keyringCountLabel, keyringRemoveButton);
        }

        private Button[] BuildCustomPanelButtons()
        {
            int count = customPanels != null ? customPanels.Count : 0;
            Button[] buttons = new Button[count];
            for (int i = 0; i < count; i++)
            {
                CustomPanelEntry entry = customPanels[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.Button == null)
                {
                    CreateCustomPanelEntry(entry);
                }

                buttons[i] = entry.Button;
            }

            return buttons;
        }

        private GameObject[] BuildCustomPanelObjects()
        {
            int count = customPanels != null ? customPanels.Count : 0;
            GameObject[] panels = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                CustomPanelEntry entry = customPanels[i];
                if (entry != null && entry.Panel != null)
                {
                    panels[i] = entry.Panel.gameObject;
                }
            }

            return panels;
        }

        private TMPro.TMP_Text[] BuildPaintSlotLabels()
        {
            TMPro.TMP_Text[] labels = new TMPro.TMP_Text[paintMaterialLabels.Count];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = paintMaterialLabels[i];
            }

            return labels;
        }

        // ModulePaletteUI.cs가 호출하는 표시 갱신 지점이다. 화면 계산은 Presenter가 담당한다.
        private void RefreshCustomNavigation()
        {
            customPresenter?.Bind(builder);
            customPresenter?.RefreshNavigation();
        }

        private void UpdateCustomPanelVisibility(bool _customMode)
        {
            customPresenter?.Bind(builder);
            customPresenter?.RefreshVisibility(_customMode);
        }

        private void RefreshPaintPanel()
        {
            customPresenter?.Bind(builder);
            customPresenter?.RefreshPaint();
        }

        private void RefreshStickerPanel()
        {
            customPresenter?.Bind(builder);
            customPresenter?.RefreshSticker();
        }

        private void RefreshKeyringPanel()
        {
            customPresenter?.Bind(builder);
            customPresenter?.RefreshKeyring();
        }

        // 커스텀 온보딩(OnboardingManager) 호환 API. 동작은 CustomizationPresenter, 강조 대상은 View가 담당한다.
        public bool TryPerformCustomOnboardingAction(string stepId)
        {
            if (stepId == "intro") return true;
            if (!isActiveAndEnabled || builder == null || !builder.isActiveAndEnabled ||
                GameManager.GameplayBlocked || customPresenter == null) return false;

            customPresenter.Bind(builder);
            switch (stepId)
            {
                case "button":
                    return customPresenter.BeginCustomForOnboarding();
                case "paint":
                    return customPresenter.SelectPanelForOnboarding(paintPanel != null ? paintPanel.gameObject : null);
                case "keyring":
                    return customPresenter.SelectPanelForOnboarding(keyringPanel != null ? keyringPanel.gameObject : null);
                case "sticker":
                    return customPresenter.SelectPanelForOnboarding(stickerPanel != null ? stickerPanel.gameObject : null);
                case "removal":
                    return customPresenter.RemoveCustomForOnboarding();
                default:
                    return false;
            }
        }

        public bool ShowCustomOnboardingStep(string stepId)
        {
            if (!isActiveAndEnabled || builder == null || !builder.isActiveAndEnabled) return false;
            switch (stepId)
            {
                case "button":
                    return customButtonRoot != null;
                case "paint":
                    return ShowCustomPanelForOnboarding(paintPanel);
                case "sticker":
                    return ShowCustomPanelForOnboarding(stickerPanel);
                case "keyring":
                    return ShowCustomPanelForOnboarding(keyringPanel);
                case "removal":
                    return builder.IsCustomMode && customRemoveButton != null;
                default:
                    return false;
            }
        }

        public RectTransform GetCustomTutorialTarget(TutorialButtonGuide guide)
        {
            switch (guide)
            {
                case TutorialButtonGuide.CustomButton:
                    return customButtonRoot;
                case TutorialButtonGuide.CustomPaint:
                    return CustomPanelButtonFor(paintPanel);
                case TutorialButtonGuide.CustomSticker:
                    return CustomPanelButtonFor(stickerPanel);
                case TutorialButtonGuide.CustomKeyring:
                    return CustomPanelButtonFor(keyringPanel);
                case TutorialButtonGuide.CustomRemove:
                    return customRemoveButton != null ? customRemoveButton.transform as RectTransform : null;
                default:
                    return null;
            }
        }

        private bool ShowCustomPanelForOnboarding(RectTransform panel)
        {
            if (panel == null || customPresenter == null) return false;
            customPresenter.Bind(builder);
            return customPresenter.SelectPanelForOnboarding(panel.gameObject);
        }

        private RectTransform CustomPanelButtonFor(RectTransform panel)
        {
            return panel != null && customView != null ? customView.PanelButtonFor(panel.gameObject) : null;
        }

        /// <summary>Registers an additional panel in the custom menu.</summary>
        public bool RegisterCustomPanel(string label, RectTransform panel)
        {
            if (string.IsNullOrWhiteSpace(label) || panel == null)
            {
                return false;
            }

            foreach (CustomPanelEntry existing in customPanels)
            {
                if (existing.Panel == panel)
                {
                    return true;
                }
            }

            CustomPanelEntry entry = new CustomPanelEntry
            {
                Label = label.Trim(),
                Panel = panel
            };
            if (!CreateCustomPanelEntry(entry))
            {
                return false;
            }
            customPanels.Add(entry);

            if (customView != null && entry.Button != null)
            {
                customView.AppendPanel(entry.Panel.gameObject, entry.Button);
            }

            RefreshCustomNavigation();
            return true;
        }

        // 저작된 탭을 복제하며 패널의 부모·위치·크기는 호출자가 배치한 그대로 유지한다.
        private bool CreateCustomPanelEntry(CustomPanelEntry entry)
        {
            if (entry == null || entry.Panel == null)
            {
                return false;
            }
            if (entry.Button != null) return true;
            Button template = null;
            foreach (CustomPanelEntry existing in customPanels)
            {
                if (existing != null && existing.Button != null)
                {
                    template = existing.Button;
                    break;
                }
            }
            if (template == null || customNavigationContent == null)
            {
                Debug.LogError("[ModulePaletteUI] 커스텀 탭 등록 실패: 프리팹에 배치된 탭 버튼이 필요합니다.", this);
                return false;
            }
            entry.Button = Instantiate(template, customNavigationContent, false);
            entry.Button.name = "CustomMenuItem_" + entry.Label;
            TMPro.TMP_Text label = entry.Button.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (label != null) label.text = entry.Label;
            entry.Panel.gameObject.SetActive(false);
            return true;
        }

        // 레거시 테이블: Resources 폴더를 스캔해 목록을 만든다. 스티커 목록은 테이블로 승격하기 전까지 이 경로를 유지한다.
        private void ResolveStickerTextures()
        {
            stickerTextures.Clear();
            stickerTextures.AddRange(Resources.LoadAll<Texture2D>("Stickers"));
            stickerTextures.Sort((left, right) => string.Compare(left.name, right.name,
                System.StringComparison.OrdinalIgnoreCase));
        }

        // 레거시 테이블: Resources 폴더를 스캔해 목록을 만든다. 열쇠고리 목록은 테이블로 승격하기 전까지 이 경로를 유지한다.
        private void ResolveKeyringModels()
        {
            keyringModels.Clear();
            GameObject[] loadedModels = Resources.LoadAll<GameObject>("Keyrings");
            foreach (GameObject model in loadedModels)
            {
                if (model != null && model.GetComponentInChildren<Renderer>(true) != null)
                {
                    keyringModels.Add(model);
                }
            }

            keyringAssetsFoundWithoutRenderer = loadedModels.Length > 0 && keyringModels.Count == 0;
            keyringModels.Sort((left, right) => string.Compare(left.name, right.name,
                System.StringComparison.OrdinalIgnoreCase));
        }

        private string GetKeyringEmptyMessage()
        {
            return keyringAssetsFoundWithoutRenderer
                ? "FBX는 찾았지만 메시 렌더러가 없습니다. FBX의 Mesh 임포트 설정을 확인하세요."
                : "Keyrings 경로에서 FBX 모델을 찾지 못했습니다. Assets/Resources/Keyrings를 확인하세요.";
        }

        // 레거시 테이블: 기본 재질 목록을 코드에서 생성한다. 테이블 승격 전까지 저장 키(material.name)를 그대로 유지한다.
        private void ResolvePaintMaterials()
        {
            activePaintMaterials.Clear();
            if (paintMaterials != null)
            {
                foreach (PaintMaterialOption option in paintMaterials)
                {
                    if (option != null && option.material != null)
                    {
                        activePaintMaterials.Add(option);
                    }
                }
            }

            generatedPaintMaterials = activePaintMaterials.Count > 0
                ? PaintMaterialLibrary.CreateCustomColorLibrary()
                : PaintMaterialLibrary.CreateDefaults();
            if (activePaintMaterials.Count > 0)
            {
                return;
            }

            foreach (PaintMaterialOption option in generatedPaintMaterials.Options)
            {
                activePaintMaterials.Add(option);
            }
        }
    }
}
