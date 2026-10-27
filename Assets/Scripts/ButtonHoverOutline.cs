using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(Outline))]
    public sealed class ButtonHoverOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Min(0f)] private float normalThickness = 0.75f;
        [SerializeField, Min(0f)] private float hoverThickness = 3f;
        private Outline outline;
        private void Awake() => outline = GetComponent<Outline>();
        private void OnEnable() => SetThickness(normalThickness);
        private void OnDisable() => SetThickness(normalThickness);
        public void OnPointerEnter(PointerEventData eventData) => SetThickness(hoverThickness);
        public void OnPointerExit(PointerEventData eventData) => SetThickness(normalThickness);
        private void SetThickness(float thickness)
        {
            if (outline != null) outline.effectDistance = new Vector2(thickness, -thickness);
        }
    }
}
