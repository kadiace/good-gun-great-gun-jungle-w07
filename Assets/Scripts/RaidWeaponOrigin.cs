using UnityEngine;
namespace WeaponWorkshop
{
    // Keep a data ID rather than a reference to an unloaded workshop object.
    public sealed class RaidWeaponOrigin : MonoBehaviour
    {
        public string ReturnBenchId { get; private set; }
        public void Initialize(string benchId) => ReturnBenchId = benchId;
    }
}
