using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // 빛처럼 색을 섞는다(가산 혼합): R·G·B를 각각 더하고 1을 넘으면 1로 자른다.
    // 빨강+초록=노랑, 빨강+파랑=자홍, 초록+파랑=청록, 셋 다=흰색. 같은 색끼리는 그 색 그대로.
    public static class LightMixer
    {
        public static Color Mix(IReadOnlyList<Color> colors)
        {
            if (colors == null || colors.Count == 0) return Color.white;
            float r = 0f, g = 0f, b = 0f;
            foreach (Color color in colors)
            {
                r += color.r;
                g += color.g;
                b += color.b;
            }
            return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
        }
    }
}
