using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class StartMenuCoreDisplay : MonoBehaviour
    {
        [SerializeField] private Transform core;
        [SerializeField] private float degreesPerSecond = 9f;
        private void Update()
        {
            if (core != null) core.Rotate(Vector3.up, degreesPerSecond * Time.unscaledDeltaTime, Space.World);
        }
    }
}
