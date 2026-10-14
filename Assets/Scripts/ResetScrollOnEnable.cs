using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(ScrollRect))]
    public sealed class ResetScrollOnEnable : MonoBehaviour
    {
        private ScrollRect scroll;
        private bool resetPending;
        private void Awake() => scroll = GetComponent<ScrollRect>();
        private void OnEnable() => resetPending = true;
        private void OnDisable() => resetPending = false;
        private void LateUpdate()
        {
            if (!resetPending) return;
            resetPending = false;
            if (scroll == null) scroll = GetComponent<ScrollRect>();
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }
    }
}
