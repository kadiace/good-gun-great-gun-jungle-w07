using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    [Serializable]
    public sealed class PaintMaterialOption
    {
        public string category;
        public string displayName;
        public string hexColor;
        public Material material;
        public Texture2D preview;
    }

    public sealed class PaintMaterialLibrary : IDisposable
    {
        private readonly List<PaintMaterialOption> options = new List<PaintMaterialOption>();
        private readonly List<Material> generatedMaterials = new List<Material>();
        private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
        private readonly Dictionary<Color32, Material> customColorMaterials = new Dictionary<Color32, Material>();

        public IReadOnlyList<PaintMaterialOption> Options => options;

        public static PaintMaterialLibrary CreateDefaults()
        {
            var library = new PaintMaterialLibrary();
            library.AddSolid("메탈릭", new Color(0.72f, 0.76f, 0.8f), 0.92f, 0.82f);
            library.AddSolid("금도색", new Color(0.83f, 0.54f, 0.12f), 0.88f, 0.9f);
            library.AddCamo("밀리터리", 17,
                new Color(0.16f, 0.2f, 0.12f), new Color(0.34f, 0.37f, 0.2f),
                new Color(0.52f, 0.48f, 0.28f), new Color(0.12f, 0.14f, 0.1f));
            library.AddCamo("정글", 29,
                new Color(0.04f, 0.18f, 0.1f), new Color(0.1f, 0.31f, 0.16f),
                new Color(0.23f, 0.4f, 0.17f), new Color(0.07f, 0.12f, 0.09f));
            library.AddCamo("사막", 43,
                new Color(0.56f, 0.41f, 0.22f), new Color(0.76f, 0.63f, 0.4f),
                new Color(0.88f, 0.77f, 0.55f), new Color(0.4f, 0.3f, 0.18f));

            library.AddColorPreset("기본", "무광 블랙", "#25272A", 0.04f, 0.24f);
            library.AddColorPreset("기본", "차콜", "#383D42", 0.12f, 0.32f);
            library.AddColorPreset("기본", "건메탈", "#596168", 0.78f, 0.72f);
            library.AddColorPreset("기본", "실버", "#A7ADB0", 0.84f, 0.68f);
            library.AddColorPreset("기본", "화이트", "#E9E8E1", 0.03f, 0.3f);

            library.AddColorPreset("밀리터리", "샌드", "#C7B68E", 0.04f, 0.28f);
            library.AddColorPreset("밀리터리", "FDE", "#A99370", 0.04f, 0.28f);
            library.AddColorPreset("밀리터리", "카키", "#82775B", 0.04f, 0.28f);
            library.AddColorPreset("밀리터리", "코요테 브라운", "#765C43", 0.04f, 0.28f);
            library.AddColorPreset("밀리터리", "OD 그린", "#626B47", 0.04f, 0.28f);
            library.AddColorPreset("밀리터리", "포레스트 그린", "#344D3D", 0.04f, 0.28f);

            library.AddColorPreset("선명한 색", "크림슨", "#A8323D", 0.04f, 0.34f);
            library.AddColorPreset("선명한 색", "오렌지", "#DC7339", 0.04f, 0.34f);
            library.AddColorPreset("선명한 색", "옐로", "#E5B83C", 0.04f, 0.34f);
            library.AddColorPreset("선명한 색", "라임", "#91BF45", 0.04f, 0.34f);
            library.AddColorPreset("선명한 색", "에메랄드", "#2E9972", 0.04f, 0.34f);
            library.AddColorPreset("선명한 색", "시안", "#43AFC0", 0.04f, 0.34f);
            library.AddColorPreset("선명한 색", "코발트 블루", "#386CAC", 0.04f, 0.34f);
            library.AddColorPreset("선명한 색", "바이올렛", "#7552A3", 0.04f, 0.34f);
            library.AddColorPreset("선명한 색", "핑크", "#D77BA6", 0.04f, 0.34f);

            library.AddColorPreset("고급 도색", "골드", "#C7A45B", 0.82f, 0.76f);
            library.AddColorPreset("고급 도색", "브론즈", "#946F4E", 0.78f, 0.72f);
            library.AddColorPreset("고급 도색", "코퍼", "#B46C4E", 0.8f, 0.72f);
            library.AddColorPreset("고급 도색", "로즈 골드", "#BB8984", 0.76f, 0.74f);
            return library;
        }

        public static PaintMaterialLibrary CreateCustomColorLibrary() => new PaintMaterialLibrary();

        public Material GetCustomColorMaterial(Color color)
        {
            Color32 colorKey = color;
            if (customColorMaterials.TryGetValue(colorKey, out Material cached)) return cached;

            Color32 srgbColor = colorKey;
            string hex = $"{srgbColor.r:X2}{srgbColor.g:X2}{srgbColor.b:X2}";
            Material material = CreateMaterial("Custom RGB #" + hex, null, srgbColor, 0f, 0.38f);
            if (material == null) return null;

            generatedMaterials.Add(material);
            customColorMaterials.Add(colorKey, material);
            return material;
        }

        private void AddSolid(string name, Color color, float metallic, float smoothness)
        {
            Texture2D texture = CreateSolidTexture(name + " Preview", color);
            Add(name, CreateMaterial(name, texture, color, metallic, smoothness), texture, "재질");
        }

        private void AddColorPreset(string category, string name, string hexColor, float metallic, float smoothness)
        {
            if (!ColorUtility.TryParseHtmlString(hexColor, out Color color)) return;
            string normalizedHex = hexColor.ToUpperInvariant();
            Texture2D texture = CreateSolidTexture(name + " Preview", color);
            Add(name, CreateMaterial(name + " " + normalizedHex, texture, color, metallic, smoothness),
                texture, category, normalizedHex);
        }

        private void AddCamo(string name, int seed, params Color[] colors)
        {
            Texture2D texture = CreateCamoTexture(name + " Pattern", seed, colors);
            Add(name, CreateMaterial(name, texture, Color.white, 0.12f, 0.32f), texture, "위장");
        }

        private void Add(string name, Material material, Texture2D texture, string category, string hexColor = "")
        {
            if (material == null) return;
            generatedMaterials.Add(material);
            generatedTextures.Add(texture);
            options.Add(new PaintMaterialOption
            {
                category = category,
                displayName = name,
                hexColor = hexColor,
                material = material,
                preview = texture
            });
        }

        private static Material CreateMaterial(string name, Texture2D texture, Color color, float metallic, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;

            var material = new Material(shader) { name = "Paint - " + name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (texture != null && material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (texture != null && material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            return material;
        }

        private static Texture2D CreateSolidTexture(string name, Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateCamoTexture(string name, int seed, Color[] colors)
        {
            const int size = 96;
            const int siteCount = 22;
            var random = new System.Random(seed);
            var sites = new Vector2[siteCount];
            var siteColors = new int[siteCount];
            for (int i = 0; i < siteCount; i++)
            {
                sites[i] = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
                siteColors[i] = random.Next(colors.Length);
            }

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    float nearest = float.PositiveInfinity;
                    int nearestSite = 0;
                    for (int i = 0; i < sites.Length; i++)
                    {
                        Vector2 delta = point - sites[i];
                        delta.x -= Mathf.Round(delta.x);
                        delta.y -= Mathf.Round(delta.y);
                        float distance = delta.sqrMagnitude;
                        if (distance >= nearest) continue;
                        nearest = distance;
                        nearestSite = i;
                    }
                    pixels[y * size + x] = colors[siteColors[nearestSite]];
                }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2
            };
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        public void Dispose()
        {
            foreach (Material material in generatedMaterials)
                if (material != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(material);
                    else UnityEngine.Object.DestroyImmediate(material);
                }
            foreach (Texture2D texture in generatedTextures)
                if (texture != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
                    else UnityEngine.Object.DestroyImmediate(texture);
                }
            generatedMaterials.Clear();
            generatedTextures.Clear();
            customColorMaterials.Clear();
            options.Clear();
        }
    }
}
