using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop.EditorTools
{
    public static class RaidExitLayoutAuthoring
    {
        public static string Apply(string path, float halfX, float halfZ)
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool close = !scene.IsValid() || !scene.isLoaded;
            if (close) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                foreach (var existing in scene.GetRootGameObjects())
                    if (existing.name == "StandardExitPerimeter") return path + ": exit layout already applied.";
                var fences = new List<Transform>();
                NightRaidArea area = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (area == null) area = root.GetComponentInChildren<NightRaidArea>(true);
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (t.name.StartsWith("PerimeterFence_") || t.name == "MilitaryPerimeter_180x220" ||
                            t.name.StartsWith("PerimeterSide") || t.name.StartsWith("FrontFence") || t.name == "RearFence") fences.Add(t);
                }
                var pieces = new HashSet<Transform>();
                Material material = null;
                foreach (var fence in fences)
                {
                    foreach (var r in fence.GetComponentsInChildren<Renderer>(true))
                    {
                        if (material == null) material = r.sharedMaterial;
                        var target = r.GetComponentInParent<DestructibleTarget>();
                        pieces.Add(target != null ? target.transform : r.transform);
                    }
                }
                foreach (var piece in pieces)
                {
                    if (piece == null) continue;
                    var rs = piece.GetComponentsInChildren<Renderer>(true);
                    if (rs.Length == 0) continue;
                    Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    bool xSide = Mathf.Abs(Mathf.Abs(b.center.x) - halfX) < 2f;
                    bool zSide = Mathf.Abs(Mathf.Abs(b.center.z) - halfZ) < 2f;
                    if (!xSide && !zSide) continue;
                    int axis = xSide ? 2 : 0;
                    float min = b.min[axis], max = b.max[axis];
                    if (max <= -10f || min >= 10f) continue;
                    if (min < -10f) Keep(piece, b, axis, min, -10f);
                    if (max > 10f) Keep(piece, b, axis, 10f, max);
                    Object.DestroyImmediate(piece.gameObject);
                }
                foreach (var fence in fences)
                {
                    var guards = new List<GameObject>();
                    foreach (var box in fence.GetComponentsInChildren<BoxCollider>(true))
                        if (box.GetComponent<Renderer>() == null && box.GetComponentInParent<DestructibleTarget>() == null && !box.isTrigger)
                            guards.Add(box.gameObject);
                    foreach (var guard in guards) if (guard != null) Object.DestroyImmediate(guard);
                }
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (t != null && t.name == "SouthMainGate_Checkpoint") Object.DestroyImmediate(t.gameObject);
                var layout = new GameObject("StandardExitPerimeter"); SceneManager.MoveGameObjectToScene(layout, scene);
                for (int side = 0; side < 4; side++)
                {
                    Vector3 outward = side == 0 ? Vector3.back : side == 1 ? Vector3.forward : side == 2 ? Vector3.left : Vector3.right;
                    Vector3 edge = new Vector3(outward.x * halfX, 0, outward.z * halfZ);
                    Vector3 tangent = new Vector3(outward.z, 0, -outward.x);
                    // Restore identical 4m openings even where the original main gate was wider.
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        panel.name = "GateFenceInfill"; panel.transform.SetParent(layout.transform);
                        panel.transform.position = edge + tangent * (sign * 6f) + Vector3.up;
                        panel.transform.rotation = Quaternion.LookRotation(outward);
                        panel.transform.localScale = new Vector3(8f, 2f, .25f);
                        if (material != null) panel.GetComponent<Renderer>().sharedMaterial = material;
                    }
                    // Continuous outer colliders include the visually open exits; no route outside the map.
                    var wall = new GameObject("OuterCollision_" + side); wall.transform.SetParent(layout.transform);
                    wall.transform.position = edge + outward * .65f + Vector3.up * 4f;
                    wall.transform.rotation = Quaternion.LookRotation(outward);
                    wall.AddComponent<BoxCollider>().size = new Vector3((side < 2 ? halfX : halfZ) * 2f + 3f, 8f, .5f);
                }
                foreach (var exit in area.GetComponentsInChildren<NightRaidBoundary>(true))
                {
                    Vector3 old = exit.transform.position;
                    Vector3 outward = Mathf.Abs(old.x / halfX) > Mathf.Abs(old.z / halfZ) ?
                        (old.x < 0 ? Vector3.left : Vector3.right) : (old.z < 0 ? Vector3.back : Vector3.forward);
                    Vector3 edge = new Vector3(outward.x * halfX, 0, outward.z * halfZ);
                    exit.transform.position = edge - outward * 1.5f;
                    exit.transform.rotation = Quaternion.LookRotation(outward);
                    exit.transform.localScale = Vector3.one;
                    foreach (Transform child in exit.transform)
                    {
                        if (child.name == "ExitPad") { child.localPosition = new Vector3(0,.03f,0); child.localScale = new Vector3(4,.05f,3); }
                        else { var p = child.localPosition; p.z += 1.5f; child.localPosition = p; }
                    }
                    var trigger = exit.GetComponent<BoxCollider>(); trigger.isTrigger = true; trigger.center = new Vector3(0,1.5f,0); trigger.size = new Vector3(4,3,3);
                    var body = exit.GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                }
                var so = new SerializedObject(area); so.FindProperty("mapSize").vector2Value = new Vector2(halfX*2, halfZ*2); so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene);
                return path + ": 4m openings, extraction pads and continuous outer collision saved.";
            }
            finally { if (close) EditorSceneManager.CloseScene(scene, true); }
        }
        private static void Keep(Transform original, Bounds bounds, int worldAxis, float min, float max)
        {
            var copy = Object.Instantiate(original.gameObject, original.parent).transform;
            copy.name = original.name + "_GateTrim";
            Vector3 direction = worldAxis == 0 ? Vector3.right : Vector3.forward;
            int localAxis = Mathf.Abs(Vector3.Dot(copy.right,direction)) > Mathf.Abs(Vector3.Dot(copy.forward,direction)) ? 0 : 2;
            var scale = copy.localScale; scale[localAxis] *= (max-min)/bounds.size[worldAxis]; copy.localScale = scale;
            var offset = Vector3.zero; offset[worldAxis] = (min+max)*.5f-bounds.center[worldAxis]; copy.position += offset;
        }
    }
}
