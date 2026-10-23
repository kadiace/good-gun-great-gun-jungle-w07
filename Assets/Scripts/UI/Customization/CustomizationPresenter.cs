using System;
using System.Collections.Generic;
using System.Globalization;

using UnityEngine;

namespace WeaponWorkshop
{
    // 커스텀 화면의 선택 상태를 소유하고 CubeBuilder의 실제 상태로 표시 값을 만든다.
    // 커스텀 진입·제거·도구 상태의 원본은 CubeBuilder이며, Presenter는 요청과 표시만 담당한다.
    public sealed class CustomizationPresenter
    {
        private const string CONFIRM_ENTRY_MESSAGE = "커스텀 제거 전까진 총기 수정이 불가합니다.\n그래도 하시겠습니까?";
        private const string CONFIRM_REMOVE_MESSAGE = "커스텀을 제거하시겠습니까?\n제거하면 총기를 다시 수정할 수 있습니다";

        private readonly CustomizationView m_view;
        private readonly PaintPanelView m_paintView;
        private readonly StickerPanelView m_stickerView;
        private readonly KeyringPanelView m_keyringView;
        private readonly Component m_owner;
        private readonly IReadOnlyList<PaintMaterialOption> m_materials;
        private readonly PaintMaterialLibrary m_customLibrary;
        private readonly IReadOnlyList<Texture2D> m_stickerTextures;
        private readonly IReadOnlyList<GameObject> m_keyringModels;
        private readonly Action m_resolveKeyrings;
        private readonly Func<string> m_keyringEmptyMessage;
        private readonly GameObject m_keyringPanel;

        private CubeBuilder m_builder;
        private bool m_isInitialized;
        private bool m_wasCustomMode;
        private bool m_confirmingCustomRemoval;
        private int m_selectedPaintMaterial = -1;
        private int m_paintMaterialPage;
        private bool m_customColorSelected;
        private bool m_customColorMode;
        private Color32 m_customColor = new Color32(70, 170, 255, 255);
        private float m_customHue;
        private int m_selectedStickerIndex = -1;
        private int m_selectedPanelIndex = -1;

        private PaintPanelState m_paintState;
        private PaintSlotState[] m_paintSlots;
        private StickerPanelState m_stickerState;
        private KeyringPanelState m_keyringState;

        public CustomizationPresenter(CustomizationView _view, PaintPanelView _paintView,
            StickerPanelView _stickerView, KeyringPanelView _keyringView, Component _owner,
            IReadOnlyList<PaintMaterialOption> _materials, PaintMaterialLibrary _customLibrary,
            IReadOnlyList<Texture2D> _stickerTextures, IReadOnlyList<GameObject> _keyringModels,
            Action _resolveKeyrings, Func<string> _keyringEmptyMessage, GameObject _keyringPanel)
        {
            m_view = _view;
            m_paintView = _paintView;
            m_stickerView = _stickerView;
            m_keyringView = _keyringView;
            m_owner = _owner;
            m_materials = _materials;
            m_customLibrary = _customLibrary;
            m_stickerTextures = _stickerTextures;
            m_keyringModels = _keyringModels;
            m_resolveKeyrings = _resolveKeyrings;
            m_keyringEmptyMessage = _keyringEmptyMessage;
            m_keyringPanel = _keyringPanel;
        }

        // View 이벤트를 구독한다. 중복 호출에 안전하다.
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null || m_paintView == null || m_stickerView == null || m_keyringView == null)
            {
                Debug.LogError("[CustomizationPresenter] Initialize 실패: View 참조가 비어 있습니다.");
                return false;
            }

            m_view.CustomRequested += OpenCustomConfirmation;
            m_view.RemoveRequested += OpenCustomRemovalConfirmation;
            m_view.PanelRequested += SelectPanel;

            m_paintView.PreviousPageRequested += OnPreviousPage;
            m_paintView.NextPageRequested += OnNextPage;
            m_paintView.SlotRequested += SelectPaintSlot;
            m_paintView.CustomColorModeRequested += OpenCustomColorPicker;
            m_paintView.ApplyColorRequested += SelectCustomColor;
            m_paintView.BackRequested += ReturnToMaterialSelection;
            m_paintView.UndoRequested += OnUndoPaint;
            m_paintView.FillRequested += OnToggleFill;
            m_paintView.PaintAllRequested += OnPaintAllFaces;
            m_paintView.RgbSubmitted += SetRgbChannel;
            m_paintView.SpectrumPicked += SelectSpectrumColor;
            m_paintView.HuePicked += SelectHue;
            m_paintView.ColorApplyOnReleaseRequested += ApplyCustomColorToBrush;

            m_stickerView.StickerRequested += SelectStickerTexture;
            m_stickerView.UndoRequested += OnUndoSticker;
            m_stickerView.EraseRequested += OnToggleStickerErase;
            m_stickerView.MirrorRequested += OnToggleStickerMirror;
            m_stickerView.SizeDownRequested += OnStickerSizeDown;
            m_stickerView.SizeUpRequested += OnStickerSizeUp;

            m_keyringView.KeyringRequested += SelectKeyringModel;
            m_keyringView.RemoveRequested += OnRemoveKeyring;

            m_paintView.SetSpectrumHue(m_customHue);
            m_paintView.SetRgbInputs(m_customColor);
            m_isInitialized = true;
            return true;
        }

        public void Dispose()
        {
            if (!m_isInitialized)
            {
                return;
            }

            m_view.CustomRequested -= OpenCustomConfirmation;
            m_view.RemoveRequested -= OpenCustomRemovalConfirmation;
            m_view.PanelRequested -= SelectPanel;

            m_paintView.PreviousPageRequested -= OnPreviousPage;
            m_paintView.NextPageRequested -= OnNextPage;
            m_paintView.SlotRequested -= SelectPaintSlot;
            m_paintView.CustomColorModeRequested -= OpenCustomColorPicker;
            m_paintView.ApplyColorRequested -= SelectCustomColor;
            m_paintView.BackRequested -= ReturnToMaterialSelection;
            m_paintView.UndoRequested -= OnUndoPaint;
            m_paintView.FillRequested -= OnToggleFill;
            m_paintView.PaintAllRequested -= OnPaintAllFaces;
            m_paintView.RgbSubmitted -= SetRgbChannel;
            m_paintView.SpectrumPicked -= SelectSpectrumColor;
            m_paintView.HuePicked -= SelectHue;
            m_paintView.ColorApplyOnReleaseRequested -= ApplyCustomColorToBrush;

            m_stickerView.StickerRequested -= SelectStickerTexture;
            m_stickerView.UndoRequested -= OnUndoSticker;
            m_stickerView.EraseRequested -= OnToggleStickerErase;
            m_stickerView.MirrorRequested -= OnToggleStickerMirror;
            m_stickerView.SizeDownRequested -= OnStickerSizeDown;
            m_stickerView.SizeUpRequested -= OnStickerSizeUp;

            m_keyringView.KeyringRequested -= SelectKeyringModel;
            m_keyringView.RemoveRequested -= OnRemoveKeyring;

            m_isInitialized = false;
        }

        // 브리지가 늦게 CubeBuilder를 찾은 경우에도 최신 참조를 쓴다.
        public void Bind(CubeBuilder _builder)
        {
            if (m_builder != _builder)
            {
                m_builder = _builder;
            }
        }

        // 탭 색만 갱신한다. 표시 여부는 RefreshVisibility가 담당한다.
        public void RefreshNavigation()
        {
            m_view.RenderTabs(m_selectedPanelIndex);
        }

        // 커스텀 모드 진입/종료를 표시하고, 진입 시 화면 선택 상태를 초기화한다.
        public void RefreshVisibility(bool _customMode)
        {
            if (_customMode && !m_wasCustomMode)
            {
                ResetForEntry();
            }

            if (!_customMode)
            {
                m_confirmingCustomRemoval = false;
                m_customColorMode = false;
                m_selectedPanelIndex = -1;
            }

            m_wasCustomMode = _customMode;
            m_view.Render(_customMode, m_selectedPanelIndex);
        }

        public void RefreshPaint()
        {
            PaintPanelState state = EnsurePaintState();
            int itemCount = m_materials.Count;
            int slotCount = m_paintView.SlotCount;
            int pageSize = Mathf.Max(1, slotCount);
            int pageCount = Mathf.Max(1, (itemCount + pageSize - 1) / pageSize);
            m_paintMaterialPage = Mathf.Clamp(m_paintMaterialPage, 0, pageCount - 1);
            int firstItem = m_paintMaterialPage * pageSize;

            state.Title = m_customColorMode ? "도색 - 나만의 색상" : "도색 - 준비된 재질";
            state.CustomColorMode = m_customColorMode;
            state.PageLabel = (m_paintMaterialPage + 1) + " / " + pageCount;
            state.CanGoPrevious = m_paintMaterialPage > 0;
            state.CanGoNext = m_paintMaterialPage < pageCount - 1;

            for (int slot = 0; slot < slotCount; slot++)
            {
                int index = firstItem + slot;
                PaintSlotState slotState = m_paintSlots[slot];
                slotState.Visible = index < itemCount;
                if (!slotState.Visible)
                {
                    continue;
                }

                PaintMaterialOption option = m_materials[index];
                Texture texture = GetPreviewTexture(option);
                slotState.SwatchTexture = texture;
                slotState.SwatchColor = texture != null
                    ? Color.white
                    : option != null && option.material != null ? option.material.color : Color.white;
                slotState.Label = option == null
                    ? string.Empty
                    : (string.IsNullOrEmpty(option.category)
                        ? option.displayName
                        : option.category + "\n" + option.displayName) +
                      (string.IsNullOrEmpty(option.hexColor) ? string.Empty : "\n#" + option.hexColor);
                slotState.Highlighted = index == m_selectedPaintMaterial;
            }

            PaintMaterialOption selected = m_selectedPaintMaterial >= 0 &&
                m_selectedPaintMaterial < m_materials.Count ? m_materials[m_selectedPaintMaterial] : null;
            state.PreviewTexture = selected != null ? GetPreviewTexture(selected) : null;
            state.PreviewColor = m_customColorSelected ? m_customColor : Color.white;
            state.MaterialName = m_customColorSelected
                ? $"나만의 색상  #{m_customColor.r:X2}{m_customColor.g:X2}{m_customColor.b:X2}"
                : selected != null ? selected.displayName : "재질을 선택하세요";
            state.CustomColor = m_customColor;
            state.CustomHex = "#" + m_customColor.r.ToString("X2") +
                              m_customColor.g.ToString("X2") +
                              m_customColor.b.ToString("X2");
            state.CustomColorButtonColor = m_customColorSelected
                ? new Color(0.34f, 0.43f, 0.36f, 1f)
                : new Color(0.23f, 0.29f, 0.36f, 1f);
            state.ApplyLabel = m_customColorSelected ? "선택한 색 다시 적용" : "이 색으로 선택";
            state.Status = BuildPaintStatus();
            state.FillColor = m_builder != null && m_builder.IsPaintFillMode
                ? new Color(0.32f, 0.45f, 0.3f, 1f)
                : new Color(0.22f, 0.28f, 0.33f, 1f);
            state.PaintAllInteractable = m_builder != null && m_builder.IsPaintingMode &&
                !GameManager.ModalBlocked;
            m_paintView.Render(state);
        }

        public void RefreshSticker()
        {
            if (m_stickerState == null)
            {
                m_stickerState = new StickerPanelState();
            }

            if (m_selectedStickerIndex >= m_stickerTextures.Count)
            {
                m_selectedStickerIndex = -1;
            }

            m_stickerState.ShowEmpty = m_stickerTextures.Count == 0;
            m_stickerState.SelectedIndex = m_selectedStickerIndex;
            if (m_builder != null && m_builder.IsStickerEraseMode)
            {
                m_stickerState.Status = "제거할 스티커를 클릭하세요 · Ctrl+Z로 복구";
            }
            else if (m_selectedStickerIndex < 0)
            {
                m_stickerState.Status = m_stickerTextures.Count == 0
                    ? "PNG 스티커를 선택할 수 없습니다"
                    : "PNG 스티커를 선택하세요";
            }
            else
            {
                m_stickerState.Status = m_builder != null && m_builder.IsStickerToolActive
                    ? m_stickerTextures[m_selectedStickerIndex].name + " 선택됨 · 면을 클릭해 붙이기"
                    : m_stickerTextures[m_selectedStickerIndex].name + " 선택됨 · 스티커를 선택해 붙이기";
            }

            m_stickerState.CanUndo = m_builder != null && m_builder.IsCustomMode;
            m_stickerState.CanErase = m_builder != null && m_builder.IsCustomMode;
            m_stickerState.EraseActive = m_builder != null && m_builder.IsStickerEraseMode;
            m_stickerState.CanAdjust = m_builder != null && m_builder.IsCustomMode && m_builder.HasLastPlacedSticker;
            m_stickerState.MirrorActive = m_builder != null && m_builder.HasLastPlacedSticker &&
                m_builder.IsStickerFlippedHorizontally;
            m_stickerState.MirrorLabel = m_builder == null || !m_builder.HasLastPlacedSticker
                ? "스티커 없음"
                : m_stickerState.MirrorActive ? "좌우 반전: 켬" : "좌우 반전: 끔";
            float sizeMultiplier = m_builder != null
                ? m_builder.StickerSizeMultiplier
                : StickerOverlay.DefaultSizeMultiplier;
            m_stickerState.SizeLabel = "×" + sizeMultiplier.ToString("0.00", CultureInfo.InvariantCulture);
            m_stickerState.CanDecreaseSize = m_stickerState.CanAdjust && m_builder.CanDecreaseStickerSize;
            m_stickerState.CanIncreaseSize = m_stickerState.CanAdjust && m_builder.CanIncreaseStickerSize;
            m_stickerView.Render(m_stickerState);
        }

        public void RefreshKeyring()
        {
            if (m_keyringState == null)
            {
                m_keyringState = new KeyringPanelState();
            }

            bool customMode = m_builder != null && m_builder.IsCustomMode;
            bool placing = customMode && m_builder.IsKeyringToolActive;
            GameObject selectedModel = m_builder != null ? m_builder.SelectedKeyringModel : null;
            int selectedIndex = -1;
            for (int i = 0; i < m_keyringModels.Count; i++)
            {
                if (m_keyringModels[i] == selectedModel)
                {
                    selectedIndex = i;
                    break;
                }
            }

            m_keyringState.ShowEmpty = m_keyringModels.Count == 0;
            m_keyringState.SelectedIndex = selectedIndex;
            m_keyringState.Placing = placing;
            m_keyringState.CanSelect = customMode;
            m_keyringState.Status = !customMode
                ? "커스텀 모드에서 사용할 수 있습니다."
                : m_keyringModels.Count == 0
                    ? KeyringEmptyMessage()
                    : placing && selectedModel != null
                        ? selectedModel.name + " 선택됨 · 총의 겉면을 클릭해 줄을 연결하세요."
                        : "키링 모델을 선택한 뒤 총의 겉면을 클릭하세요.";
            m_keyringState.CountLabel = "장착된 키링: " + (m_builder != null ? m_builder.KeyringCount : 0);
            m_keyringState.CanRemove = customMode && m_builder.KeyringCount > 0;
            m_keyringView.Render(m_keyringState);
        }

        private PaintPanelState EnsurePaintState()
        {
            int slotCount = m_paintView.SlotCount;
            if (m_paintState == null || m_paintSlots == null || m_paintSlots.Length != slotCount)
            {
                m_paintSlots = new PaintSlotState[slotCount];
                for (int i = 0; i < slotCount; i++)
                {
                    m_paintSlots[i] = new PaintSlotState();
                }

                m_paintState = new PaintPanelState { Slots = m_paintSlots };
            }

            return m_paintState;
        }

        private void ResetForEntry()
        {
            m_selectedPaintMaterial = -1;
            m_customColorSelected = false;
            m_customColorMode = false;
            m_paintMaterialPage = 0;
            m_selectedStickerIndex = -1;
            m_selectedPanelIndex = -1;
        }

        private string BuildPaintStatus()
        {
            if (m_builder == null)
            {
                return "총기에서 도색할 면을 클릭하세요";
            }

            string selection = m_builder.IsKeyringToolActive
                ? (m_builder.SelectedKeyringModel != null ? m_builder.SelectedKeyringModel.name : "키링 모델")
                : m_builder.IsStickerToolActive ? m_builder.StickerBrushLabel : m_builder.PaintBrushLabel;
            string tool = m_builder.IsKeyringToolActive
                ? "키링"
                : m_builder.IsStickerToolActive ? "스티커" : m_builder.IsPaintFillMode ? "물감통" : "붓";
            return m_builder.PaintSelectionLabel + "\n현재 선택: " + selection + "  |  도구: " + tool;
        }

        private string KeyringEmptyMessage()
        {
            return m_keyringEmptyMessage != null ? m_keyringEmptyMessage() : string.Empty;
        }

        private void OpenCustomConfirmation()
        {
            if (m_builder == null || !m_builder.CanStartCustom)
            {
                return;
            }

            m_confirmingCustomRemoval = false;
            if (FeedbackUI.For(m_owner)?.ShowChoice(m_owner, CONFIRM_ENTRY_MESSAGE,
                    ConfirmCustom, CancelCustomConfirmation) != true)
            {
                CancelCustomConfirmation();
            }
        }

        private void OpenCustomRemovalConfirmation()
        {
            if (m_builder == null || !m_builder.IsCustomMode)
            {
                return;
            }

            m_confirmingCustomRemoval = true;
            if (FeedbackUI.For(m_owner)?.ShowChoice(m_owner, CONFIRM_REMOVE_MESSAGE,
                    ConfirmCustom, CancelCustomConfirmation) != true)
            {
                CancelCustomConfirmation();
            }
        }

        private void ConfirmCustom()
        {
            bool removeCustom = m_confirmingCustomRemoval;
            m_confirmingCustomRemoval = false;
            if (removeCustom)
            {
                ConfirmCustomRemoval();
                return;
            }

            if (m_builder == null || !m_builder.BeginCustomMode())
            {
                return;
            }

            Camera listenerCamera = Camera.main;
            WorkshopSoundEffects.RaiseAssemblyCompleted(listenerCamera != null
                ? listenerCamera.transform.position
                : m_owner.transform.position);
            m_selectedPaintMaterial = -1;
            m_customColorSelected = false;
            m_customColorMode = false;
            m_paintMaterialPage = 0;
            m_selectedStickerIndex = -1;
            m_selectedPanelIndex = -1;
            RefreshNavigation();
            RefreshPaint();
            RefreshSticker();
            RefreshVisibility(true);
        }

        private void ConfirmCustomRemoval()
        {
            if (m_builder == null || !m_builder.RemoveCustomAndResumeEditing())
            {
                return;
            }

            m_customColorSelected = false;
            m_customColorMode = false;
            m_selectedPaintMaterial = -1;
            m_selectedStickerIndex = -1;
            RefreshPaint();
            RefreshSticker();
        }

        private void CancelCustomConfirmation()
        {
            m_confirmingCustomRemoval = false;
        }

        private void SelectPanel(int _index)
        {
            if (_index < 0 || _index >= m_view.PanelCount)
            {
                return;
            }

            if (m_builder == null || !m_builder.IsCustomMode)
            {
                return;
            }

            if (_index != m_selectedPanelIndex)
            {
                m_builder.CancelKeyringPlacement();
            }

            m_selectedPanelIndex = _index;
            bool keyring = m_view.PanelAt(_index) == m_keyringPanel;
            if (keyring)
            {
                m_resolveKeyrings?.Invoke();
                m_keyringView.ShowModels(m_keyringModels, KeyringEmptyMessage());
            }

            m_view.RenderTabs(_index);
            m_view.Render(true, _index);
            if (keyring)
            {
                RefreshKeyring();
            }
        }

        private void OnPreviousPage()
        {
            ChangePaintMaterialPage(-1);
        }

        private void OnNextPage()
        {
            ChangePaintMaterialPage(1);
        }

        private void ChangePaintMaterialPage(int _delta)
        {
            int pageSize = Mathf.Max(1, m_paintView.SlotCount);
            int pageCount = Mathf.Max(1, (m_materials.Count + pageSize - 1) / pageSize);
            m_paintMaterialPage = Mathf.Clamp(m_paintMaterialPage + _delta, 0, pageCount - 1);
            RefreshPaint();
        }

        private void SelectPaintSlot(int _slot)
        {
            int pageSize = Mathf.Max(1, m_paintView.SlotCount);
            SelectPaintMaterial(m_paintMaterialPage * pageSize + _slot);
        }

        private void SelectPaintMaterial(int _index)
        {
            if (m_builder == null || !m_builder.IsPaintingMode ||
                _index < 0 || _index >= m_materials.Count)
            {
                return;
            }

            PaintMaterialOption option = m_materials[_index];
            if (option == null || option.material == null)
            {
                return;
            }

            m_selectedPaintMaterial = _index;
            m_customColorSelected = false;
            m_customColorMode = false;
            m_builder.SelectPaintMaterial(option.material);
            RefreshPaint();
            RefreshSticker();
        }

        private void OpenCustomColorPicker()
        {
            if (m_builder == null || !m_builder.IsPaintingMode)
            {
                return;
            }

            m_customColorMode = true;
            RefreshPaint();
        }

        private void SelectCustomColor()
        {
            if (m_builder == null || !m_builder.IsPaintingMode)
            {
                return;
            }

            m_selectedPaintMaterial = -1;
            m_customColorSelected = true;
            ApplyCustomColorToBrush();
            RefreshPaint();
        }

        private void ReturnToMaterialSelection()
        {
            m_customColorMode = false;
            RefreshPaint();
        }

        private void ApplyCustomColorToBrush()
        {
            if (!m_customColorSelected || m_builder == null || !m_builder.IsPaintingMode ||
                m_customLibrary == null)
            {
                return;
            }

            Material material = m_customLibrary.GetCustomColorMaterial(m_customColor);
            if (material != null)
            {
                m_builder.SelectPaintMaterial(material);
            }
        }

        private void SetRgbChannel(int _channel, string _value)
        {
            if (!int.TryParse(_value, out int channelValue))
            {
                m_paintView.SetRgbInputs(m_customColor);
                return;
            }

            Color32 next = m_customColor;
            byte component = (byte)Mathf.Clamp(channelValue, 0, 255);
            if (_channel == 0)
            {
                next.r = component;
            }
            else if (_channel == 1)
            {
                next.g = component;
            }
            else
            {
                next.b = component;
            }

            SetCustomColor(next, true);
            ApplyCustomColorToBrush();
        }

        private void SelectSpectrumColor(Vector2 _point)
        {
            SetCustomColor(Color.HSVToRGB(m_customHue, Mathf.Clamp01(_point.x),
                Mathf.Clamp01(_point.y)), false);
        }

        private void SelectHue(Vector2 _point)
        {
            m_customHue = 1f - Mathf.Clamp01(_point.y);
            m_paintView.SetSpectrumHue(m_customHue);
            Color.RGBToHSV(m_customColor, out _, out float saturation, out float value);
            SetCustomColor(Color.HSVToRGB(m_customHue, saturation, value), false);
        }

        private void SetCustomColor(Color _color, bool _updateHue)
        {
            Color next = _color;
            next.a = 1f;
            m_customColor = next;
            if (_updateHue)
            {
                Color.RGBToHSV(next, out m_customHue, out _, out _);
                m_paintView.SetSpectrumHue(m_customHue);
            }

            m_paintView.SetRgbInputs(m_customColor);
            RefreshPaint();
        }

        private void SelectStickerTexture(int _index)
        {
            if (m_builder == null || _index < 0 || _index >= m_stickerTextures.Count)
            {
                return;
            }

            Texture2D texture = m_stickerTextures[_index];
            if (texture == null || !m_builder.SelectSticker(texture))
            {
                return;
            }

            m_selectedStickerIndex = _index;
            RefreshSticker();
            if (m_builder.IsPaintingMode)
            {
                RefreshPaint();
            }
        }

        private void SelectKeyringModel(int _index)
        {
            if (m_builder == null || _index < 0 || _index >= m_keyringModels.Count)
            {
                return;
            }

            if (!m_builder.SelectKeyringForPlacement(m_keyringModels[_index]))
            {
                return;
            }

            RefreshKeyring();
        }

        private void OnUndoPaint()
        {
            m_builder?.UndoPaint();
        }

        private void OnToggleFill()
        {
            if (m_builder != null)
            {
                m_builder.SetPaintFillMode(!m_builder.IsPaintFillMode);
            }

            RefreshPaint();
        }

        private void OnPaintAllFaces()
        {
            if (m_builder != null)
            {
                m_builder.PaintAllFaces();
            }

            RefreshPaint();
        }

        private void OnUndoSticker()
        {
            m_builder?.UndoSticker();
        }

        private void OnToggleStickerErase()
        {
            if (m_builder != null)
            {
                m_builder.SetStickerEraseMode(!m_builder.IsStickerEraseMode);
            }

            RefreshSticker();
        }

        private void OnToggleStickerMirror()
        {
            m_builder?.ToggleStickerHorizontalFlip();
            RefreshSticker();
        }

        private void OnStickerSizeDown()
        {
            m_builder?.AdjustStickerSize(-1);
            RefreshSticker();
        }

        private void OnStickerSizeUp()
        {
            m_builder?.AdjustStickerSize(1);
            RefreshSticker();
        }

        private void OnRemoveKeyring()
        {
            m_builder?.RemoveKeyring();
            RefreshKeyring();
        }

        private static Texture GetPreviewTexture(PaintMaterialOption _option)
        {
            if (_option == null)
            {
                return null;
            }

            if (_option.preview != null)
            {
                return _option.preview;
            }

            return _option.material != null ? _option.material.mainTexture : null;
        }
    }
}
