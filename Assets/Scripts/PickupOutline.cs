using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class PickupOutline : MonoBehaviour
    {
        public float WidthMultiplier { get; set; } = 1f;
        [SerializeField] private bool highlightWhenAimed;
        public bool HighlightWhenAimed => highlightWhenAimed;
        private static readonly List<PickupOutline> active = new List<PickupOutline>();
        public static IReadOnlyList<PickupOutline> Active => active;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => active.Clear();
        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);
    }
}
