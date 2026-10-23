using UnityEngine;
using UnityEngine.Rendering;

namespace WeaponWorkshop
{
    public sealed class TutorialPlacementGuide : MonoBehaviour
    {
        [SerializeField] private Material previewMaterial;
        [SerializeField, Min(.1f)] private float pulsePeriod = 2f;
        [SerializeField, Min(.05f), Tooltip("총구 호버 단계에서 출력면에 띄우는 시선 유도 구체의 지름")] private float muzzleHintSize = .6f;
        private readonly GameObject[] hints = new GameObject[6];
        private GameObject muzzleHint;
        private readonly Vector3Int[] faces = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back };
        private MaterialPropertyBlock properties, muzzleProperties;
        private void Awake() { properties = new MaterialPropertyBlock(); muzzleProperties = new MaterialPropertyBlock(); Hide(); }
        private void LateUpdate()
        {
            var manager = TutorialManager.Instance;
            var step = manager != null ? manager.CurrentStep : null;
            var bench = manager != null ? manager.TutorialWorkbench : null;
            var weapon = bench != null ? bench.CurrentWeapon : null;
            var assembly = weapon != null ? weapon.GetComponentInChildren<CubeAssembly>() : null;
            float pulse = .5f - .5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / pulsePeriod);
            UpdateMuzzleHint(step, assembly, pulse);
            if (step == null || !step.showPlacementHints || assembly == null || previewMaterial == null)
            { HideBlocks(); return; }
            var blocks = assembly.GetComponentsInChildren<PlacedBlock>();
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
        // 총구 호버 단계에서 첫 발사 경로의 노출된 출력면 중심에 노란 반투명 구체를 띄운다. 콜라이더가 없어 호버 판정을 가리지 않는다.
        private void UpdateMuzzleHint(TutorialStep step, CubeAssembly assembly, float pulse)
        {
            bool visible = step != null && step.requiredSignal == "muzzle-stats-viewed" && assembly != null && previewMaterial != null;
            var paths = visible ? assembly.GetProjectilePaths() : null;
            visible &= paths != null && paths.Count > 0;
            if (!visible) { if (muzzleHint != null) muzzleHint.SetActive(false); return; }
            if (muzzleHint == null)
            {
                muzzleHint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                muzzleHint.name = "TutorialMuzzleHint";
                muzzleHint.layer = LayerMask.NameToLayer("Ignore Raycast");
                var collider = muzzleHint.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                var renderer = muzzleHint.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = previewMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
            muzzleHint.SetActive(true);
            muzzleHint.transform.SetParent(assembly.transform, false);
            muzzleHint.transform.localPosition = paths[0].origin; // origin은 이미 출력면 중심이다.
            muzzleHint.transform.localRotation = Quaternion.identity;
            muzzleHint.transform.localScale = Vector3.one * muzzleHintSize;
            var color = new Color(1f, .85f, .05f, Mathf.Lerp(.3f, .55f, pulse));
            muzzleProperties.SetColor("_BaseColor", color); muzzleProperties.SetColor("_Color", color);
            muzzleHint.GetComponent<MeshRenderer>().SetPropertyBlock(muzzleProperties);
        }
        private void HideBlocks() { foreach (var hint in hints) if (hint != null) hint.SetActive(false); }
        private void Hide() { HideBlocks(); if (muzzleHint != null) muzzleHint.SetActive(false); }
        private void OnDisable() => Hide();
        private void OnDestroy()
        {
            foreach (var hint in hints) if (hint != null) Destroy(hint);
            if (muzzleHint != null) Destroy(muzzleHint);
        }
    }
}
