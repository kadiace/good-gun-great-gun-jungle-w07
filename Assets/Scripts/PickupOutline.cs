using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class PickupOutline : MonoBehaviour
    {
        private static readonly List<PickupOutline> active = new List<PickupOutline>();
        public static IReadOnlyList<PickupOutline> Active => active;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => active.Clear();
        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);
    }
}
