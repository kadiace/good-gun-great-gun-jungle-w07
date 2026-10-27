using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // A separate graphic keeps the selection border independent of the button's tint.
    public sealed class ModuleSelectionBorder : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            const float width = 2.25f;
            AddRect(mesh, new Rect(r.xMin - width, r.yMin - width, r.width + width * 2f, width));
            AddRect(mesh, new Rect(r.xMin - width, r.yMax, r.width + width * 2f, width));
            AddRect(mesh, new Rect(r.xMin - width, r.yMin, width, r.height));
            AddRect(mesh, new Rect(r.xMax, r.yMin, width, r.height));
        }

        private void AddRect(VertexHelper mesh, Rect rect)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
