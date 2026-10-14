using UnityEngine;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HomingLockGraphic : UnityEngine.UI.MaskableGraphic
    {
        private float progress;
        public void SetProgress(float value) { progress = value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            float eased = 1f - (1f-progress)*(1f-progress);
            float radius = Mathf.Lerp(65f, 16f, eased);
            const float length = 16f, thickness = 2f;
            Color tint = progress >= 1f ? new Color(0.3f,1f,0.5f) : new Color(1f,0.85f,0.25f);
            for (int i=0; i<4; i++)
            {
                float angle = (45f+i*90f)*Mathf.Deg2Rad;
                Quad(mesh, new Vector2(radius-length,radius-thickness), new Vector2(radius,radius), angle, tint);
                Quad(mesh, new Vector2(radius-thickness,radius-length), new Vector2(radius,radius), angle, tint);
            }
        }
        private static void Quad(UnityEngine.UI.VertexHelper mesh, Vector2 min, Vector2 max, float angle, Color tint)
        {
            int start = mesh.currentVertCount;
            Vector2 Rotate(Vector2 p) => new Vector2(p.x*Mathf.Cos(angle)-p.y*Mathf.Sin(angle), p.x*Mathf.Sin(angle)+p.y*Mathf.Cos(angle));
            mesh.AddVert(Rotate(min),tint,Vector2.zero);
            mesh.AddVert(Rotate(new Vector2(min.x,max.y)),tint,Vector2.zero);
            mesh.AddVert(Rotate(max),tint,Vector2.zero);
            mesh.AddVert(Rotate(new Vector2(max.x,min.y)),tint,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2); mesh.AddTriangle(start+2,start+3,start);
        }
    }
}
