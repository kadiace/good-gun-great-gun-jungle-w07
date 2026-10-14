using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>Marks the point on an installed module where the player's hand grips the weapon.</summary>
    public sealed class TriggerModule : MonoBehaviour
    {
        [SerializeField] private Transform gripPoint;

        public Transform GripPoint => gripPoint != null ? gripPoint : transform;
    }
}
