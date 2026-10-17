using UnityEngine;

namespace WeaponWorkshop
{
    public class SlotMachineReels : MonoBehaviour
    {
        [SerializeField] private Transform[] reels;
        [SerializeField] private float rotationsPerSecond = 1.5f;

        private void Update()
        {
            for (int i = 0; i < reels.Length; i++)
            {
                if (reels[i] != null)
                    reels[i].Rotate(Vector3.right, 360f * rotationsPerSecond * Time.deltaTime, Space.Self);
            }
        }
    }
}
