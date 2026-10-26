using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // Simplified site plans based on the five raid scenes, rather than a shared invented city.
    public sealed class RaidAreaMapGraphic : Graphic
    {
        [SerializeField, Range(0, 4)] private int region;
        public void SetRegion(int index) { region = index; SetVerticesDirty(); }
        private VertexHelper mesh;
        private Color stroke;
        private Vector2 scale;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); mesh = vh; stroke = color;
            float uniformScale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) / 360f;
            scale = Vector2.one * uniformScale;
            Frame(-167, -72, 167, 76);
            switch (region)
            {
                case 0: House(); break;
                case 1: Bank(); break;
                case 2: Base(); break;
                case 3: Airport(); break;
                case 4: Campus(); break;
            }
            Entry(region == 1 ? -112 : 0, -66);
        }
        private void House()
        {
            Building(-56, -21, 43, 37);
            Line(-56, -21, -7, 10); Line(-7, 10, 43, -21);
            Line(-56, 37, -7, 10); Line(-7, 10, 43, 37);
            Frame(-24, -34, 13, -21); // South porch.
            Building(86, 6, 130, 34); // East storage shed.
            Dash(40, -29, 110, 3);
            Dash(-6, -58, -6, -35);
            Tree(-117, 46, 10); Tree(-137, -10, 10); Tree(-88, 59, 9);
            Tree(30, 60, 10); Tree(140, 50, 9); Tree(141, -32, 10);
            Line(-150, -56, -30, -56); Line(25, -56, 151, -56);
        }
        private void Bank()
        {
            Building(-98, -28, 104, 45); // Broad central bank.
            Frame(-82, -17, 88, 32);
            for (int x = -65; x <= 70; x += 27) Frame(x, -30, x + 9, -20);
            Frame(-32, -39, 34, -28); // South entrance and plaza.
            Dash(-111, -56, -3, -48); Dash(-3, -48, -3, -39);
            for (int x = -60; x <= 90; x += 37) Car(x, 57, false);
            for (int y = -17; y <= 36; y += 23) Car(-124, y, true);
            Line(-142, -40, -142, 65); Line(-140, 68, 137, 68);
        }
        private void Base()
        {
            // North headquarters; garage northwest and hangar northeast.
            Building(-28, 46, 25, 66);
            Building(-133, 24, -68, 43); Building(67, 21, 139, 45);
            Frame(-138, -52, -85, -35); // Southwest armory.
            Frame(76, -54, 142, -37); // Southeast logistics containers.
            Building(-129, -28, -90, -15); Building(88, -28, 129, -15);
            Building(-130, 51, -90, 63);
            Line(-8, -58, -8, 42); Line(8, -58, 8, 42);
            Line(-144, 17, 144, 17); Line(-144, 8, 144, 8);
            Dash(0, -51, 0, 43);
            for (int x = -126; x < -48; x += 32)
            {
                Tank(x, -4); Tank(x, -18);
            }
            for (int x = 69; x < 140; x += 44)
            {
                Helipad(x, -3); Helipad(x, -24);
            }
            Frame(-17, -63, 18, -56); // South checkpoint.
            for (int x = -150; x <= 150; x += 300)
                for (int y = -59; y <= 61; y += 120) Frame(x - 5, y - 4, x + 5, y + 4);
        }
        private void Airport()
        {
            Building(-132, -35, -21, 17);
            Frame(-124, -28, -29, 10);
            for (int x = -113; x <= -38; x += 15) Line(x, -19, x, -12);
            for (int y = -24; y <= 7; y += 15) Frame(-21, y, 8, y + 5);
            Building(-132, 44, -82, 63);
            Frame(-150, -7, -137, 19);
            Frame(57, -55, 112, 68);
            Line(79, -55, 79, 68); Line(89, -55, 89, 68);
            for (int y = -44; y <= 60; y += 19) Line(83, y, 86, y + 5);
            Dash(-82, -57, -82, -35);
        }
        private void Campus()
        {
            Frame(-140, -57, 140, 63);
            Building(-77, -24, 75, 43);
            Frame(-69, -17, 66, 36); Frame(-61, -10, 58, 29);
            for (int x = -48; x <= 45; x += 25) Line(x, 29, x, 36);
            Line(-69, 8, -61, 8); Line(58, 8, 66, 8);
            Frame(-14, -32, 14, -24);
            Dash(0, -56, 0, -34);
            Line(-140, -45, -130, -45); Line(130, -45, 140, -45);
        }
        private void Building(float x1, float y1, float x2, float y2)
        {
            Frame(x1, y1, x2, y2);
            Line(x1 + 5, y2 - 6, x2 - 6, y2 - 6);
            Line(x2 + 3, y1 - 3, x2 + 3, y2 - 2);
            Line(x1 + 4, y1 - 4, x2 + 3, y1 - 3);
        }
        private void Frame(float x1, float y1, float x2, float y2)
        {
            Line(x1, y1, x2, y1 + .8f); Line(x2, y1, x2 - .6f, y2);
            Line(x2, y2, x1 + 1, y2 - .7f); Line(x1, y2, x1 + .8f, y1);
        }
        private void Tree(float x, float y, float radius)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f, b = (i + 1) * Mathf.PI / 4f;
                float r = radius * (i % 2 == 0 ? 1f : .84f);
                Line(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r,
                    x + Mathf.Cos(b) * radius, y + Mathf.Sin(b) * radius);
            }
            Line(x - 3, y, x + 3, y); Line(x, y - 3, x, y + 3);
        }
        private void Car(float x, float y, bool vertical)
        {
            float w = vertical ? 5 : 12, h = vertical ? 12 : 5;
            Frame(x - w, y - h, x + w, y + h);
            if (vertical) Line(x - w, y - 3, x + w, y - 3);
            else Line(x - 3, y - h, x - 3, y + h);
        }
        private void Tank(float x, float y)
        { Frame(x - 9, y - 5, x + 9, y + 5); Frame(x - 4, y - 3, x + 3, y + 3); Line(x, y, x + 15, y); }
        private void Helipad(float x, float y)
        { Frame(x - 13, y - 8, x + 13, y + 8); Line(x - 5, y - 5, x - 5, y + 5); Line(x + 5, y - 5, x + 5, y + 5); Line(x - 5, y, x + 5, y); }
        private void Entry(float x, float y)
        { Color old = stroke; stroke = new Color(.94f, .75f, .38f, color.a); Line(x, y - 7, x, y + 4); Line(x, y + 4, x - 4, y); Line(x, y + 4, x + 4, y); stroke = old; }
        private void Dash(float x1, float y1, float x2, float y2)
        {
            for (int i = 0; i < 10; i += 2)
            {
                var a = Vector2.Lerp(new Vector2(x1, y1), new Vector2(x2, y2), i / 10f);
                var b = Vector2.Lerp(new Vector2(x1, y1), new Vector2(x2, y2), (i + 1) / 10f);
                Line(a.x, a.y, b.x, b.y);
            }
        }
        private void Line(float x1, float y1, float x2, float y2)
        {
            Vector2 start = Vector2.Scale(new Vector2(x1, y1 * 2.1f), scale);
            Vector2 end = Vector2.Scale(new Vector2(x2, y2 * 2.1f), scale);
            Vector2 delta = end - start;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized;
            int parts = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 22f));
            for (int i = 0; i < parts; i++)
            {
                Vector2 a = Vector2.Lerp(start, end, i / (float)parts) + normal * Mathf.Sin(i * 2.3f + x1) * .55f;
                Vector2 b = Vector2.Lerp(start, end, (i + 1f) / parts) + normal * Mathf.Sin((i + 1) * 2.3f + x1) * .55f;
                Vector2 width = normal * .65f;
                int first = mesh.currentVertCount;
                mesh.AddVert(a - width, stroke, Vector2.zero); mesh.AddVert(a + width, stroke, Vector2.zero);
                mesh.AddVert(b + width, stroke, Vector2.zero); mesh.AddVert(b - width, stroke, Vector2.zero);
                mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
            }
        }
    }
}
