using UnityEngine;
using UnityEngine.Rendering;

namespace WeaponWorkshop
{
    public sealed class TutorialPlacementGuide : MonoBehaviour
    {
        [SerializeField] private Material previewMaterial;
        [SerializeField, Min(.1f)] private float pulsePeriod = 2f;
        private readonly GameObject[] hints = new GameObject[6];
        private readonly Vector3Int[] faces = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back };
        private MaterialPropertyBlock properties;
        private void Awake() { properties = new MaterialPropertyBlock(); Hide(); }
        private void LateUpdate()
        {
            var manager = TutorialManager.Instance;
            var step = manager != null ? manager.CurrentStep : null;
            var bench = manager != null ? manager.TutorialWorkbench : null;
            var weapon = bench != null ? bench.CurrentWeapon : null;
            var assembly = weapon != null ? weapon.GetComponentInChildren<CubeAssembly>() : null;
            if (step == null || !step.showPlacementHints || assembly == null || previewMaterial == null)
            { Hide(); return; }
            var blocks = assembly.GetComponentsInChildren<PlacedBlock>();
            float pulse = .5f - .5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / pulsePeriod);
            var color = new Color(1f, .8f, .05f, Mathf.Lerp(.05f, .25f, pulse));
            properties.SetColor("_BaseColor", color); properties.SetColor("_Color", color);
            for (int i = 0; i < faces.Length; i++)
            {
                bool customCells = step.hintCells != null && step.hintCells.Length > 0;
                var cell = customCells && i < step.hintCells.Length ? step.hintCells[i] : faces[i];
                bool filled = false;
                foreach (var block in blocks)
                    if (block.transform.parent == assembly.transform && block.Cell == cell) { filled = true; break; }
                bool visible = (customCells ? i < step.hintCells.Length : faces[i] != step.openCoreFace) && !filled;
                if (visible && hints[i] == null)
                {
                    hints[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    hints[i].name = "TutorialBlockHint_" + i;
                    hints[i].layer = LayerMask.NameToLayer("Ignore Raycast");
                    var collider = hints[i].GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                    var renderer = hints[i].GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = previewMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                }
                if (hints[i] == null) continue;
                hints[i].SetActive(visible);
                if (!visible) continue;
                hints[i].transform.SetParent(assembly.transform, false);
                hints[i].transform.localPosition = (Vector3)cell;
                hints[i].transform.localRotation = Quaternion.identity;
                hints[i].transform.localScale = Vector3.one * .99f;
                hints[i].GetComponent<MeshRenderer>().SetPropertyBlock(properties);
            }
        }
        private void Hide() { foreach (var hint in hints) if (hint != null) hint.SetActive(false); }
        private void OnDisable() => Hide();
        private void OnDestroy() { foreach (var hint in hints) if (hint != null) Destroy(hint); }
    }
}
