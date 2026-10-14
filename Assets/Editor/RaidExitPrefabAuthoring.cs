using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop.EditorTools
{
    public static class RaidExitPrefabAuthoring
    {
        public const string PrefabPath = "Assets/Prefabs/NightRaid/NightRaidExit.prefab";
        private const string MeshPath = "Assets/Meshes/NightRaid/NightRaidExitSymbol.asset";
        private const string WhitePath = "Assets/Materials/NightRaid/NightRaidExitSymbol.mat";
        private const string GreenPath = "Assets/Materials/NightRaid/NightRaidExitSign.mat";
        // 습격 씬은 Assets/Scenes/NightRaid 아래에 있다. 경로를 옮기면 이곳만 바꾸면 된다.
        public const string RaidScenesFolder = "Assets/Scenes/NightRaid";

        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode before editing exits.");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null) { mesh = BuildSymbol(); AssetDatabase.CreateAsset(mesh, MeshPath); }
            var white = MaterialAt(WhitePath, Color.white);
            var green = MaterialAt(GreenPath, new Color(.015f, .32f, .12f));
            var sourcePath = RaidScenesFolder + "/NightRaid_WoodenHouse.unity";
            var source = SceneManager.GetSceneByPath(sourcePath);
            bool closeSource = !source.IsValid() || !source.isLoaded;
            if (closeSource) source = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Additive);
            try
            {
                var original = Exits(source).First();
                var template = Object.Instantiate(original.gameObject);
                try
                {
                    template.name = "NightRaidExit";
                    template.transform.SetParent(null);
                    template.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    template.transform.localScale = Vector3.one;
                    foreach (var text in template.GetComponentsInChildren<TMPro.TMP_Text>(true))
                        Object.DestroyImmediate(text.gameObject);
                    var boundary = new SerializedObject(template.GetComponent<NightRaidBoundary>());
                    boundary.FindProperty("exitName").stringValue = "탈출구";
                    boundary.FindProperty("inwardDirection").vector3Value = Vector3.back;
                    boundary.ApplyModifiedPropertiesWithoutUndo();
                    AddSign(template.transform, "SignInside", new Vector3(0, 3.7f, 1.35f), Quaternion.identity, mesh, white, green);
                    AddSign(template.transform, "SignOutside", new Vector3(0, 3.7f, 1.65f), Quaternion.Euler(0, 180, 0), mesh, white, green);
                    PrefabUtility.SaveAsPrefabAsset(template, PrefabPath);
                }
                finally { Object.DestroyImmediate(template); }
            }
            finally { if (closeSource) EditorSceneManager.CloseScene(source, true); }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var report = new List<string>();
            var paths = AssetDatabase.FindAssets("t:Scene", new[] { RaidScenesFolder })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => System.IO.Path.GetFileName(p).StartsWith("NightRaid_") || p.EndsWith("Tutorial_WoodenHouse.unity")).ToArray();
            foreach (var path in paths)
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool close = !scene.IsValid() || !scene.isLoaded;
                if (close) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var exits = Exits(scene).ToArray();
                    foreach (var exit in exits)
                    {
                        var position = exit.transform.position;
                        var rotation = exit.transform.rotation;
                        string name = exit.name;
                        string exitName = exit.ExitName;
                        PrefabUtility.ConvertToPrefabInstance(exit.gameObject, prefab, new ConvertToPrefabInstanceSettings
                        {
                            objectMatchMode = ObjectMatchMode.ByHierarchy,
                            componentsNotMatchedBecomesOverride = false,
                            gameObjectsNotMatchedBecomesOverride = false,
                            recordPropertyOverridesOfMatches = false,
                            changeRootNameToAssetName = false
                        }, InteractionMode.AutomatedAction);
                        exit.transform.SetPositionAndRotation(position, rotation);
                        exit.transform.localScale = Vector3.one;
                        exit.name = name;
                        var serialized = new SerializedObject(exit);
                        serialized.FindProperty("exitName").stringValue = exitName;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(exit.transform);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(exit);
                    }
                    if (exits.Length > 0)
                    { EditorSceneManager.SaveScene(scene); report.Add(path + ": " + exits.Length); }
                }
                finally { if (close) EditorSceneManager.CloseScene(scene, true); }
            }
            return string.Join("\n", report);
        }

        private static IEnumerable<NightRaidBoundary> Exits(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<NightRaidBoundary>(true));

        private static Material MaterialAt(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = color };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void AddSign(Transform parent, string name, Vector3 position, Quaternion rotation,
            Mesh mesh, Material white, Material green)
        {
            var sign = new GameObject(name);
            sign.transform.SetParent(parent, false);
            sign.transform.localPosition = position;
            sign.transform.localRotation = rotation;
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "GreenBoard";
            board.transform.SetParent(sign.transform, false);
            board.transform.localScale = new Vector3(1.35f, 1.1f, .035f);
            Object.DestroyImmediate(board.GetComponent<Collider>());
            board.GetComponent<MeshRenderer>().sharedMaterial = green;
            var icon = new GameObject("EmergencyExitSymbol", typeof(MeshFilter), typeof(MeshRenderer));
            icon.transform.SetParent(sign.transform, false);
            icon.transform.localPosition = new Vector3(0, 0, -.025f);
            icon.GetComponent<MeshFilter>().sharedMesh = mesh;
            icon.GetComponent<MeshRenderer>().sharedMaterial = white;
            icon.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Mesh BuildSymbol()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Triangle(int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); triangles.Add(c); triangles.Add(b); triangles.Add(a); }
            void Stroke(Vector2 a, Vector2 b, float width)
            {
                Vector2 normal = new Vector2(-(b-a).y, (b-a).x).normalized * width * .5f;
                int i = vertices.Count;
                vertices.Add(a+normal); vertices.Add(b+normal); vertices.Add(b-normal); vertices.Add(a-normal);
                Triangle(i, i+1, i+2); Triangle(i, i+2, i+3);
            }
            // Door frame and a running person: editable vector mesh, independent of fonts.
            Stroke(new Vector2(.23f,-.43f), new Vector2(.23f,.43f), .065f);
            Stroke(new Vector2(.23f,.43f), new Vector2(.55f,.43f), .065f);
            Stroke(new Vector2(.55f,.43f), new Vector2(.55f,-.43f), .065f);
            int center = vertices.Count; vertices.Add(new Vector3(-.055f,.30f,0));
            for (int n=0;n<24;n++) { float angle=n*Mathf.PI*2/24; vertices.Add(new Vector3(-.055f+Mathf.Cos(angle)*.087f,.30f+Mathf.Sin(angle)*.087f,0)); }
            for (int n=0;n<24;n++) Triangle(center, center+1+n, center+1+(n+1)%24);
            Stroke(new Vector2(-.10f,.18f), new Vector2(-.20f,-.07f), .13f);
            Stroke(new Vector2(-.12f,.13f), new Vector2(.03f,.05f), .075f);
            Stroke(new Vector2(.03f,.05f), new Vector2(.15f,.15f), .075f);
            Stroke(new Vector2(-.13f,.15f), new Vector2(-.32f,.08f), .075f);
            Stroke(new Vector2(-.32f,.08f), new Vector2(-.42f,-.07f), .075f);
            Stroke(new Vector2(-.20f,-.07f), new Vector2(-.04f,-.21f), .095f);
            Stroke(new Vector2(-.04f,-.21f), new Vector2(-.01f,-.43f), .085f);
            Stroke(new Vector2(-.20f,-.07f), new Vector2(-.32f,-.24f), .095f);
            Stroke(new Vector2(-.32f,-.24f), new Vector2(-.54f,-.29f), .085f);
            var mesh = new Mesh { name = "EmergencyExitSymbol" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
