using UnityEngine;

namespace WeaponWorkshop
{
    // Authored independently so each fixture can adjust its guide placement.
    public sealed class InteractionGuidePoints : MonoBehaviour
    {
        [SerializeField] private Transform trackingCenter;
        [SerializeField] private Transform markerTop;
        public Vector3 Center => trackingCenter != null ? trackingCenter.position : transform.position;
        public Vector3 Top => markerTop != null ? markerTop.position : Center + Vector3.up;
    }
}
