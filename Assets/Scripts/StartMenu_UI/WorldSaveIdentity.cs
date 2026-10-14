using UnityEngine;
namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class WorldSaveIdentity : MonoBehaviour
    {
        [SerializeField, HideInInspector] private string saveId;
        public string Id => saveId;
        internal void SetFallbackId(string value) { if (string.IsNullOrEmpty(saveId)) saveId = value; }
    }
}
