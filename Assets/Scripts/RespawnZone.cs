using UnityEngine;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RespawnZone : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            FirstPersonPlayer player = other.GetComponentInParent<FirstPersonPlayer>();
            if (player == null) return;

            Transform viewPoint = player.ViewPoint;
            Vector3 viewEuler = viewPoint != null ? viewPoint.localEulerAngles : Vector3.zero;
            player.RestorePose(Vector3.zero, player.transform.eulerAngles, viewEuler);
        }
    }
}
