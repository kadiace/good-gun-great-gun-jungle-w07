using UnityEngine;
using UnityEngine.EventSystems;

namespace WeaponWorkshop
{
    public sealed class RaidRegionHoverUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private NightRaidUI owner;
        private int region;
        public void Bind(NightRaidUI ui, int index) { owner = ui; region = index; }
        public void OnPointerEnter(PointerEventData data) => owner?.ShowRegionInfo(region);
        public void OnPointerExit(PointerEventData data) => owner?.ClearRegionInfo(region);
        public void OnSelect(BaseEventData data) => owner?.ShowRegionInfo(region);
        public void OnDeselect(BaseEventData data) => owner?.ClearRegionInfo(region);
    }
}
