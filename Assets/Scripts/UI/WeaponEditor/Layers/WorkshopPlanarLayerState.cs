using UnityEngine;

namespace WeaponWorkshop
{
    // 층 이동 UI의 한 프레임 표시 값이다. 계산은 Presenter가 담당하고 View는 저작된 오브젝트에 적용만 한다.
    public sealed class WorkshopPlanarLayerState
    {
        public string Title = string.Empty;
        public string Hint = string.Empty;
        public bool CanEdit;
        public Color SelectedColor = Color.black;
        public Color UnselectedColor = Color.black;
        public WorkshopPlanarLayerSlotState[] Slots = new WorkshopPlanarLayerSlotState[0];
    }

    // 브리지 layers 배열 슬롯 하나의 표시 값이다. 슬롯 인덱스는 저작 배열 순서를 따른다.
    public sealed class WorkshopPlanarLayerSlotState
    {
        public int Depth;
        public bool IsInRange;
        public bool IsSelected;
        public bool Interactable;
        public string Caption = string.Empty;
    }
}
