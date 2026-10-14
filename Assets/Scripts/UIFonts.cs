using TMPro;

namespace WeaponWorkshop
{
    public static class UIFonts
    {
        public static void ApplyWeight(TMP_Text text, int weight)
        {
            if (text == null || text.font == null) return;
            int index = UnityEngine.Mathf.Clamp(weight / 100, 1, 9);
            var face = text.font.fontWeightTable[index].regularTypeface;
            if (face != null) text.font = face;
            text.fontSharedMaterial = text.font.material;
            text.fontWeight = FontWeight.Regular;
            text.fontStyle &= ~FontStyles.Bold;
        }
    }
}
