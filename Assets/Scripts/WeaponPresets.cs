using System;
using UnityEngine;

namespace WeaponWorkshop
{
    // Player-owned blueprints: save files and workshop travel retain the same six slots.
    public sealed class WeaponPresets : MonoBehaviour
    {
        public const int SlotCount = 6;
        [SerializeField] private EquipableWeapon[] defaults = new EquipableWeapon[SlotCount];
        private string[] codes;
        public event Action<int, string> Stored;
        private void Awake() => Initialize();
        private void Initialize()
        {
            if (codes != null) return;
            codes = new string[SlotCount];
            for (int i = 0; i < codes.Length; i++)
                if (defaults != null && i < defaults.Length && defaults[i] != null)
                    codes[i] = WeaponShareCode.Export(defaults[i]);
        }
        public string GetCode(int slot) { Initialize(); return codes[slot]; }
        public void Store(int slot, EquipableWeapon weapon)
        {
            Initialize();
            codes[slot] = WeaponShareCode.Export(weapon);
            Stored?.Invoke(slot, codes[slot]);
        }
        public string[] Capture() { Initialize(); return (string[])codes.Clone(); }
        public void Restore(string[] saved)
        {
            Initialize();
            if (saved == null) return; // Older saves keep the authored defaults.
            if (saved.Length > SlotCount) throw new ArgumentException("프리셋 저장 데이터를 확인하세요.");
            foreach (var code in saved) if (!string.IsNullOrEmpty(code)) WeaponShareCode.Decode(code);
            // 3슬롯 시절 세이브는 짧으므로 빈 슬롯으로 채운다.
            codes = new string[SlotCount];
            Array.Copy(saved, codes, saved.Length);
        }
    }
}
