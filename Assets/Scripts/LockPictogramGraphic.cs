using UnityEngine;

namespace WeaponWorkshop
{
    // White padlock silhouette with a transparent shackle and keyhole.
    public sealed class LockPictogramGraphic : UnityEngine.UI.MaskableGraphic
    {
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) / 40f;
            Vector2 center = rectTransform.rect.center;
            AddRect(mesh, center, scale, -12f, -14f, 10f, 16f);
            AddRect(mesh, center, scale, 2f, -14f, 10f, 16f);
            AddRect(mesh, center, scale, -2f, -14f, 4f, 5f);
            AddRect(mesh, center, scale, -2f, -1f, 4f, 3f);
            AddRect(mesh, center, scale, -9f, 2f, 3f, 3f);
            AddRect(mesh, center, scale, 6f, 2f, 3f, 3f);
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.PI * i / segments, b = Mathf.PI * (i + 1) / segments;
                int start = mesh.currentVertCount;
                foreach (var point in new[] { new Vector2(Mathf.Cos(a) * 9f, 5f + Mathf.Sin(a) * 9f),
                    new Vector2(Mathf.Cos(b) * 9f, 5f + Mathf.Sin(b) * 9f),
                    new Vector2(Mathf.Cos(b) * 6f, 5f + Mathf.Sin(b) * 6f),
                    new Vector2(Mathf.Cos(a) * 6f, 5f + Mathf.Sin(a) * 6f) })
                    mesh.AddVert(center + point * scale, color, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
            }
        }
        private void AddRect(UnityEngine.UI.VertexHelper mesh, Vector2 center, float scale, float x, float y, float width, float height)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(center + new Vector2(x, y) * scale, color, Vector2.zero);
            mesh.AddVert(center + new Vector2(x, y + height) * scale, color, Vector2.zero);
            mesh.AddVert(center + new Vector2(x + width, y + height) * scale, color, Vector2.zero);
            mesh.AddVert(center + new Vector2(x + width, y) * scale, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
