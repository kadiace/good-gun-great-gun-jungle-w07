using System;
using TMPro;
using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// CoreDispenser의 무기 이름 라벨 표기 해석 및 오류 알림 표시를 전담하는 Presenter입니다.
    /// </summary>
    public sealed class CoreDispenserPresenter
    {
        public string ResolveDispenserWeaponName(
            EquipableWeapon _corePrefab,
            int _presetSlot,
            WeaponPresets _presets,
            string _displayedCode,
            EquipableWeapon _weaponPreview)
        {
            string name = _corePrefab != null ? _corePrefab.WeaponName : string.Empty;

            if (_presetSlot >= 0 && _presets != null)
            {
                string code = _presets.GetCode(_presetSlot);
                if (string.IsNullOrEmpty(code))
                {
                    name = string.Empty;
                }
                else if (code != _displayedCode)
                {
                    try
                    {
                        name = WeaponShareCode.Decode(code).name;
                    }
                    catch (Exception)
                    {
                        name = string.Empty;
                    }
                }
                else
                {
                    name = _weaponPreview != null ? _weaponPreview.WeaponName : name;
                }
            }

            return name;
        }

        public void UpdateNameLabel(TMP_Text _label, string _name)
        {
            if (_label == null)
            {
                return;
            }

            if (_label.text != _name)
            {
                _label.text = _name;
            }
        }

        public void ShowErrorNotification(Component _owner, string _message)
        {
            if (string.IsNullOrEmpty(_message))
            {
                return;
            }

            FeedbackUI.For(_owner)?.ShowNotification(_message);
        }
    }
}
