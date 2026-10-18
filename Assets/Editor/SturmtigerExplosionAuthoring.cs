using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WeaponWorkshop.Editor
{
    // Explicit one-shot authoring request; never runs during play or rewrites existing assets on reload.
    [InitializeOnLoad]
    internal static class SturmtigerExplosionAuthoring
    {
        private const string Request = "Library/BuildSturmtigerExplosion.request";
        private const string PrefabPath = "Assets/Prefabs/FX/FX_SturmtigerExplosion.prefab";
        private const string ShellPath = "Assets/Prefabs/Enemies/RaidSturmtigerShell.prefab";
        static SturmtigerExplosionAuthoring() => EditorApplication.update += ApplyPending;

        private static void ApplyPending()
        {
            if (!File.Exists(Request)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            EditorApplication.update -= ApplyPending;
            File.Delete(Request);
            GameObject root = null;
            GameObject shell = null;
            try
            {
                var soft = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FX_Particle.mat");
                var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false) { name = "SturmtigerShockRing", wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 128; y++)
                    for (int x = 0; x < 128; x++)
                    {
                        float r = new Vector2((x + .5f) / 64f - 1f, (y + .5f) / 64f - 1f).magnitude;
                        float a = Mathf.Exp(-Mathf.Pow((r - .83f) / .045f, 2f));
                        texture.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                texture.Apply(false, true);
                const string texturePath = "Assets/Materials/Enemies/SturmtigerShockRing.asset";
                const string materialPath = "Assets/Materials/Enemies/SturmtigerShockRing.mat";
                AssetDatabase.CreateAsset(texture, texturePath);
                var ringMaterial = new Material(soft) { name = "SturmtigerShockRing" };
                ringMaterial.SetTexture("_BaseMap", texture);
                ringMaterial.SetTexture("_MainTex", texture);
                AssetDatabase.CreateAsset(ringMaterial, materialPath);

                root = new GameObject("FX_SturmtigerExplosion");
                // Authored at unit diameter: the existing explosion system scales by radius * 2.
                var fire = Layer(root, null, soft, 14, .42f, .03f, .24f, .35f, .03f, 0f,
                    new Color(1f, .85f, .3f), new Color(.8f, .16f, .025f), 1f);
                Grow(fire, .35f, 1.25f);
                var flash = Layer(new GameObject("Flash"), root, soft, 1, .12f, 0f, .35f, 0f, 0f, 0f,
                    new Color(1f, .95f, .7f), new Color(1f, .45f, .1f), .9f);
                Grow(flash, .5f, 1.4f);
                var smoke = Layer(new GameObject("BlackSmoke"), root, soft, 12, 2.3f, .12f, .25f, .10f, .08f, .09f,
                    new Color(.10f, .09f, .08f), new Color(.20f, .18f, .16f), .65f);
                Grow(smoke, .35f, 1.9f);
                var dust = Layer(new GameObject("Dust"), root, soft, 18, 1.05f, .04f, .15f, .30f, .10f, .025f,
                    new Color(.42f, .30f, .18f), new Color(.30f, .25f, .19f), .5f);
                var dustShape = dust.shape; dustShape.shapeType = ParticleSystemShapeType.Circle;
                dust.transform.localRotation = Quaternion.Euler(90, 0, 0);
                Grow(dust, .3f, 1.8f);
                var ring = Layer(new GameObject("Shockwave"), root, ringMaterial, 1, .45f, 0f, 1.2f, 0f, 0f, 0f,
                    new Color(.8f, .68f, .44f), new Color(.4f, .32f, .2f), .6f);
                ring.transform.localPosition = Vector3.up * .015f;
                var ringRenderer = ring.GetComponent<ParticleSystemRenderer>();
                ringRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                Grow(ring, .08f, 1f);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                shell = PrefabUtility.LoadPrefabContents(ShellPath);
                var data = new SerializedObject(shell.GetComponent<RaidMissile>());
                data.FindProperty("explosionEffectOverride").objectReferenceValue = prefab.GetComponent<ParticleSystem>();
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(shell, ShellPath);
                AssetDatabase.SaveAssets();
                foreach (string path in new[] { texturePath, materialPath, PrefabPath, ShellPath })
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                File.WriteAllText("Library/SturmtigerExplosionAuthoring.completed", "Dedicated explosion prefab created and assigned; changed assets imported.");
            }
            catch (System.Exception exception) { Debug.LogException(exception); }
            finally
            {
                if (shell != null) PrefabUtility.UnloadPrefabContents(shell);
                if (root != null) Object.DestroyImmediate(root);
            }
        }

        private static ParticleSystem Layer(GameObject obj, GameObject parent, Material material, short count,
            float life, float delay, float size, float speed, float radius, float rise, Color from, Color to, float alpha)
        {
            if (parent != null) obj.transform.SetParent(parent.transform, false);
            var ps = obj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.duration = life + delay;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * .8f, life);
            main.startDelay = delay; main.startSpeed = speed; main.startSize = size;
            main.maxParticles = count; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.stopAction = ParticleSystemStopAction.None;
            var emission = ps.emission; emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius;
            var velocity = ps.velocityOverLifetime; velocity.enabled = rise > 0f;
            velocity.space = ParticleSystemSimulationSpace.Local; velocity.y = rise;
            var colors = ps.colorOverLifetime; colors.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(from, 0), new GradientColorKey(to, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(alpha, .08f),
                    new GradientAlphaKey(alpha * .65f, .45f), new GradientAlphaKey(0, 1) });
            colors.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.maxParticleSize = 1f;
            return ps;
        }

        private static void Grow(ParticleSystem ps, float start, float end)
        {
            var module = ps.sizeOverLifetime; module.enabled = true;
            module.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, start, 1, end));
        }
    }
}
