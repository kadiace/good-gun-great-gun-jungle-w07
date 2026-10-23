using UnityEngine;

namespace WeaponWorkshop
{
    // 도색/스티커 패널의 한 프레임 표시 값이다. 계산은 Presenter가 하고 View는 적용만 한다.
    public sealed class PaintPanelState
    {
        public string Title = string.Empty;
        public bool CustomColorMode;
        public string PageLabel = string.Empty;
        public bool CanGoPrevious;
        public bool CanGoNext;
        public PaintSlotState[] Slots = new PaintSlotState[0];
        public Texture PreviewTexture;
        public Color PreviewColor = Color.white;
        public string MaterialName = string.Empty;
        public Color CustomColor = Color.white;
        public string CustomHex = string.Empty;
        public Color CustomColorButtonColor = Color.white;
        public string ApplyLabel = string.Empty;
        public string Status = string.Empty;
        public Color FillColor = Color.white;
        public bool PaintAllInteractable;
    }

    // 재질 슬롯 하나의 표시 값이다. 슬롯 인덱스는 저작 배열 순서를 따른다.
    public sealed class PaintSlotState
    {
        public bool Visible;
        public Texture SwatchTexture;
        public Color SwatchColor = Color.white;
        public string Label = string.Empty;
        public bool Highlighted;
    }

    // 스티커 패널의 한 프레임 표시 값이다.
    public sealed class StickerPanelState
    {
        public bool ShowEmpty;
        public int SelectedIndex = -1;
        public string Status = string.Empty;
        public bool CanUndo;
        public bool CanErase;
        public bool EraseActive;
        public bool CanAdjust;
        public bool MirrorActive;
        public string MirrorLabel = string.Empty;
        public string SizeLabel = string.Empty;
        public bool CanDecreaseSize;
        public bool CanIncreaseSize;
    }

    // 키링 패널의 한 프레임 표시 값이다.
    public sealed class KeyringPanelState
    {
        public bool ShowEmpty;
        public int SelectedIndex = -1;
        public bool Placing;
        public bool CanSelect;
        public string Status = string.Empty;
        public string CountLabel = string.Empty;
        public bool CanRemove;
    }
}
