using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace WeaponWorkshop
{
    // 주문 영역 표시는 작업대 세션을 따라가며 입력이나 설치 자체를 제한하지 않는다.
    public sealed class OrderShapeGuide : MonoBehaviour
    {
        [SerializeField] private WorkshopSession session;
        [SerializeField] private Bell customerBell;
        [SerializeField] private Material previewMaterial;
        private readonly List<GameObject> hints = new List<GameObject>();
        private MaterialPropertyBlock properties;
        private int used;
        private void Awake() { properties = new MaterialPropertyBlock(); Hide(); }
        private void LateUpdate()
        {
            used = 0;
            var customer = customerBell != null ? customerBell.Current : null;
            var weapon = session != null && session.IsEditing && session.ActiveWorkbench != null
                ? session.ActiveWorkbench.CurrentWeapon : null;
            var assembly = weapon != null ? weapon.GetComponentInChildren<CubeAssembly>() : null;
            if (customer == null || !customer.HasActiveOrder || customer.Profile == null || assembly == null || previewMaterial == null)
            { Hide(); return; }
            var occupied = GunShapeInfo.OccupiedCells(assembly);
            var table = TableManager.Instance.CustomerRequest;
            var shown = new HashSet<string>();
            foreach (string id in customer.Profile.RequestIds)
                foreach (var condition in table.Conditions(table.FindRequest(id)))
                {
                    if (condition == null || condition.Measure != GunMeasure.Shape || !shown.Add(condition.shapeId)) continue;
                    var shape = TableManager.Instance.GunShape.Find(condition.shapeId);
                    if (shape == null || !shape.valid) continue;
                    int outside = 0, outsideMirror = 0, minX = 0, maxX = 0;
                    foreach (var cell in occupied)
                    {
                        if (!GunShapeInfo.Contains(shape, cell)) outside++;
                        if (!GunShapeInfo.Contains(shape, cell, true)) outsideMirror++;
                        minX = Mathf.Min(minX, cell.x); maxX = Mathf.Max(maxX, cell.x);
                    }
                    bool mirror = shape.allowMirrorZ && outsideMirror < outside;
                    if (shape.mode == "bounds")
                    {
                        Vector3 center = new Vector3((shape.width - 1) * .5f - shape.width / 2,
                            (shape.height - 1) * .5f - shape.height / 2, (shape.depth - 1) * .5f - shape.depth / 2);
                        if (mirror) center.z = -center.z;
                        Show(assembly, center, new Vector3(shape.width, shape.height, shape.depth));
                    }
                    else
                        for (int row = 0; row < shape.rows.Length; row++)
                            for (int col = 0; col < shape.rows[row].Length; col++)
                                if (shape.rows[row][col] != '.')
                                    Show(assembly, new Vector3((minX + maxX) * .5f, shape.anchorRow-row,
                                        (col-shape.anchorColumn) * (mirror ? -1 : 1)), new Vector3(maxX-minX+1, 1, 1));
                }
            for (int i = used; i < hints.Count; i++) if (hints[i] != null) hints[i].SetActive(false);
        }
        private void Show(CubeAssembly assembly, Vector3 cell, Vector3 size)
        {
            if (assembly == null || previewMaterial == null) return;
            if (used == hints.Count) hints.Add(null);
            if (hints[used] == null)
            {
                var hint = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hint.name = "OrderAllowedArea";
                int layer = LayerMask.NameToLayer("Ignore Raycast");
                if (layer >= 0) hint.layer = layer;
                var collider = hint.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                var renderer = hint.GetComponent<MeshRenderer>(); renderer.sharedMaterial = previewMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                hints[used] = hint;
            }
            var item = hints[used++];
            if (item.transform.parent != transform) item.transform.SetParent(transform, false);
            item.transform.SetPositionAndRotation(assembly.transform.TransformPoint(cell), assembly.transform.rotation);
            // 힌트는 가이드 아래에 두므로 가이드 배율을 나눠 월드 크기를 조립체 배율에 맞춘다.
            Vector3 world = Vector3.Scale(assembly.transform.lossyScale, size * .99f), parent = transform.lossyScale;
            item.transform.localScale = new Vector3(Divide(world.x, parent.x), Divide(world.y, parent.y), Divide(world.z, parent.z));
            var color = new Color(1, 1, 1, .09f + .04f * Mathf.Sin(Time.unscaledTime * Mathf.PI));
            properties.SetColor("_BaseColor", color); properties.SetColor("_Color", color);
            item.GetComponent<MeshRenderer>().SetPropertyBlock(properties);
            item.SetActive(true);
        }
        private static float Divide(float value, float scale) => Mathf.Approximately(scale, 0f) ? value : value / scale;
        private void Hide() { foreach (var hint in hints) if (hint != null) hint.SetActive(false); }
        private void OnDisable() => Hide();
        private void OnDestroy() { foreach (var hint in hints) if (hint != null) Destroy(hint); }
    }
}
