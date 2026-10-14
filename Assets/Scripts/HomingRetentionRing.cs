using UnityEngine;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HomingRetentionRing : UnityEngine.UI.MaskableGraphic
    {
        private float radius = 100f;
        public void SetRadius(float value)
        {
            if (Mathf.Approximately(radius, value)) return;
            radius = value; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            const int segments = 96;
            const float thickness = 1.5f;
            for (int i=0; i<segments; i++)
            {
                float a=i*Mathf.PI*2f/segments, b=(i+1)*Mathf.PI*2f/segments;
                Vector2 first=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                Vector2 second=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                int start=mesh.currentVertCount;
                mesh.AddVert(first*(radius-thickness),color,Vector2.zero);
                mesh.AddVert(first*radius,color,Vector2.zero);
                mesh.AddVert(second*radius,color,Vector2.zero);
                mesh.AddVert(second*(radius-thickness),color,Vector2.zero);
                mesh.AddTriangle(start,start+1,start+2); mesh.AddTriangle(start+2,start+3,start);
            }
        }
    }
}
