using UnityEngine;
using UnityEngine.EventSystems;

namespace WeaponWorkshop
{
    // 누르거나 끄는 위치를 이 사각형 안의 0~1 좌표(왼쪽 아래 = 0,0)로 알려준다. ColorPickerUI가 사용.
    public sealed class ColorPickArea : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public event System.Action<Vector2> Picked;

        public void OnPointerDown(PointerEventData eventData) => Pick(eventData);
        public void OnDrag(PointerEventData eventData) => Pick(eventData);

        private void Pick(PointerEventData eventData)
        {
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position,
                    eventData.pressEventCamera, out Vector2 local)) return;
            Rect area = rect.rect;
            Picked?.Invoke(new Vector2(Mathf.Clamp01((local.x - area.x) / area.width),
                Mathf.Clamp01((local.y - area.y) / area.height)));
        }
    }
}
