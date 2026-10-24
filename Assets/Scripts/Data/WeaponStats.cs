using System;
using UnityEngine;

namespace WeaponWorkshop
{
    // 공용 타입: 수정 전 팀 합의 필요. 값 타입이라 복사해도 원본이 바뀌지 않는다.
    [Serializable]
    public struct WeaponStats
    {
        [Tooltip("관통력")] public float penetration;
        [Tooltip("연사력")] public float fireRate;
        [Tooltip("폭발력")] public float explosion;
        [Tooltip("반사 횟수")] public float ricochet;
        [Tooltip("차지")] public float charge;
        [Tooltip("중력")] public float gravity;
        [Tooltip("발열(화염 속성)")] public float heat;
        [Tooltip("회전")] public float spin;
        [Tooltip("물(데미지 0)")] public float water;
        [Tooltip("유도")] public float homing;
        [Tooltip("산탄")] public float shotgun;
        [Tooltip("무게")] public float weight;
        [Tooltip("스코프")] public float scope;
        [Tooltip("가속(탄 기본 속도 배율)")] public float speed;
        [Tooltip("컬러(개수, 색은 블록의 ColorModule)")] public float color;
        [Tooltip("탄퍼짐(도). 음수면 퍼짐 감소")] public float spread;

        public static readonly StatType[] AllTypes = (StatType[])Enum.GetValues(typeof(StatType));

        public float Get(StatType type)
        {
            switch (type)
            {
                case StatType.Penetration: return penetration;
                case StatType.FireRate: return fireRate;
                case StatType.Explosion: return explosion;
                case StatType.Ricochet: return ricochet;
                case StatType.Charge: return charge;
                case StatType.Gravity: return gravity;
                case StatType.Heat: return heat;
                case StatType.Spin: return spin;
                case StatType.Water: return water;
                case StatType.Homing: return homing;
                case StatType.Shotgun: return shotgun;
                case StatType.Weight: return weight;
                case StatType.Scope: return scope;
                case StatType.Speed: return speed;
                case StatType.Color: return color;
                case StatType.Spread: return spread;
                default: return 0f;
            }
        }

        public void Set(StatType type, float value)
        {
            switch (type)
            {
                case StatType.Penetration: penetration = value; break;
                case StatType.FireRate: fireRate = value; break;
                case StatType.Explosion: explosion = value; break;
                case StatType.Ricochet: ricochet = value; break;
                case StatType.Charge: charge = value; break;
                case StatType.Gravity: gravity = value; break;
                case StatType.Heat: heat = value; break;
                case StatType.Spin: spin = value; break;
                case StatType.Water: water = value; break;
                case StatType.Homing: homing = value; break;
                case StatType.Shotgun: shotgun = value; break;
                case StatType.Weight: weight = value; break;
                case StatType.Scope: scope = value; break;
                case StatType.Speed: speed = value; break;
                case StatType.Color: color = value; break;
                case StatType.Spread: spread = value; break;
            }
        }

        public void Add(StatType type, float amount) => Set(type, Get(type) + amount);

        public void Add(WeaponStats other)
        {
            foreach (StatType type in AllTypes) Add(type, other.Get(type));
        }

        // 머지: Scripts/WeaponStats.cs 쪽 멤버 합침 (PlacedBlock가 사용).
        // 현재 규칙: 관통력과 폭발력이 반동에 같은 비중으로 기여.
        public float Recoil => penetration + explosion;

        public WeaponStats Clamped()
        {
            WeaponStats result = this;
            foreach (StatType type in AllTypes) result.Set(type, Mathf.Max(0f, Get(type)));
            return result;
        }

        public static WeaponStats operator +(WeaponStats a, WeaponStats b)
        {
            a.Add(b);
            return a;
        }

        public float Sum()
        {
            float total = 0f;
            foreach (StatType type in AllTypes) total += Get(type);
            return total;
        }
    }
}
