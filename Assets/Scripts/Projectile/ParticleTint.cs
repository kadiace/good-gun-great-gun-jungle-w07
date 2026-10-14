using UnityEngine;

namespace WeaponWorkshop
{
    // 컬러 모듈 색으로 이펙트(발열 꼬리, 폭발)를 다시 칠한다. 원래 그라데이션의 밝기 변화는 유지:
    // 각 색을 "같은 밝기의 흰색 ~ 같은 밝기의 지정색" 사이로 바꾸며, 원래 채도가 높을수록 지정색에 가깝다.
    // 그래서 흰 불꽃 심지·회색 연기는 거의 그대로, 주황 부분만 지정색이 된다.
    // 수명 그라데이션에 색이 있으면 시작 색은 밝기만 남겨 색이 두 번 곱해지지 않게 한다.
    public static class ParticleTint
    {
        public static void Apply(GameObject root, Color tint)
        {
            if (root == null) return;
            foreach (ParticleSystem particles in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var lifetime = particles.colorOverLifetime;
                bool coloredLifetime = lifetime.enabled && HasColor(lifetime.color);
                if (lifetime.enabled) lifetime.color = Recolor(lifetime.color, tint, false);
                var main = particles.main;
                main.startColor = Recolor(main.startColor, tint, coloredLifetime);
            }
        }

        private static bool HasColor(ParticleSystem.MinMaxGradient gradient)
        {
            switch (gradient.mode)
            {
                case ParticleSystemGradientMode.Color: return Saturation(gradient.color) > 0.05f;
                case ParticleSystemGradientMode.TwoColors:
                    return Saturation(gradient.colorMin) > 0.05f || Saturation(gradient.colorMax) > 0.05f;
                case ParticleSystemGradientMode.TwoGradients:
                    return HasColor(gradient.gradientMin) || HasColor(gradient.gradientMax);
                default: return HasColor(gradient.gradient);
            }
        }

        private static bool HasColor(Gradient gradient)
        {
            if (gradient == null) return false;
            foreach (GradientColorKey key in gradient.colorKeys)
                if (Saturation(key.color) > 0.05f) return true;
            return false;
        }

        private static ParticleSystem.MinMaxGradient Recolor(ParticleSystem.MinMaxGradient gradient, Color tint, bool brightnessOnly)
        {
            switch (gradient.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(Recolor(gradient.color, tint, brightnessOnly));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(Recolor(gradient.colorMin, tint, brightnessOnly),
                        Recolor(gradient.colorMax, tint, brightnessOnly));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Recolor(gradient.gradientMin, tint, brightnessOnly),
                        Recolor(gradient.gradientMax, tint, brightnessOnly));
                default:
                    var result = new ParticleSystem.MinMaxGradient(Recolor(gradient.gradient, tint, brightnessOnly));
                    result.mode = gradient.mode;
                    return result;
            }
        }

        private static Gradient Recolor(Gradient gradient, Color tint, bool brightnessOnly)
        {
            if (gradient == null) return null;
            GradientColorKey[] keys = gradient.colorKeys;
            for (int i = 0; i < keys.Length; i++) keys[i].color = Recolor(keys[i].color, tint, brightnessOnly);
            var result = new Gradient { mode = gradient.mode };
            result.SetKeys(keys, gradient.alphaKeys);
            return result;
        }

        private static Color Recolor(Color color, Color tint, bool brightnessOnly)
        {
            float brightness = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            Color white = new Color(brightness, brightness, brightness, color.a);
            if (brightnessOnly) return white;
            Color tinted = new Color(tint.r * brightness, tint.g * brightness, tint.b * brightness, color.a);
            return Color.Lerp(white, tinted, Saturation(color));
        }

        private static float Saturation(Color color)
        {
            Color.RGBToHSV(color, out _, out float saturation, out _);
            return saturation;
        }
    }
}
