using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class WeaponKeyringHistory : MonoBehaviour
    {
        public const int MaximumKeyringCount = 1;

        private readonly List<WeaponKeyring> keyrings = new List<WeaponKeyring>();

        public int Count
        {
            get
            {
                int count = 0;
                foreach (WeaponKeyring keyring in keyrings)
                    if (keyring != null) count++;
                return count;
            }
        }

        public bool Place(EquipableWeapon weapon, GameObject modelPrefab, Transform attachmentFrame,
            Vector3 worldPoint, Vector3 worldNormal)
        {
            if (!isActiveAndEnabled || weapon == null || modelPrefab == null || weapon.gameObject != gameObject ||
                !weapon.ConstructionLocked || attachmentFrame == null ||
                (attachmentFrame.GetComponentInParent<PlacedBlock>() == null &&
                 attachmentFrame.GetComponentInParent<PowerCore>() != null) ||
                (attachmentFrame != weapon.transform && !attachmentFrame.IsChildOf(weapon.transform)) ||
                weapon.GetComponentInChildren<CubeAssembly>(true) == null ||
                Count >= MaximumKeyringCount) return false;

            GameObject instance = new GameObject(modelPrefab.name + " Keyring");
            WeaponKeyring keyring = instance.AddComponent<WeaponKeyring>();
            if (!keyring.Initialize(this, weapon, modelPrefab, attachmentFrame, worldPoint, worldNormal))
            {
                DestroyInstance(instance);
                return false;
            }

            keyrings.Add(keyring);
            return true;
        }

        public bool Undo()
        {
            for (int i = keyrings.Count - 1; i >= 0; i--)
            {
                WeaponKeyring keyring = keyrings[i];
                keyrings.RemoveAt(i);
                if (keyring == null) continue;
                DestroyInstance(keyring.gameObject);
                return true;
            }

            return false;
        }

        public void ClearAll()
        {
            for (int i = keyrings.Count - 1; i >= 0; i--)
            {
                WeaponKeyring keyring = keyrings[i];
                keyrings.RemoveAt(i);
                if (keyring != null) DestroyInstance(keyring.gameObject);
            }
        }

        internal void Forget(WeaponKeyring keyring) => keyrings.Remove(keyring);

        private static void DestroyInstance(GameObject instance)
        {
            if (instance == null) return;
            if (Application.isPlaying) Destroy(instance);
            else DestroyImmediate(instance);
        }

        private void OnDestroy() => ClearAll();
    }
}
