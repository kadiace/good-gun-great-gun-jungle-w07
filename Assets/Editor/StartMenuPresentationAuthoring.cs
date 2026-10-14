using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace WeaponWorkshop.EditorTools
{
    public static class StartMenuPresentationAuthoring
    {
        private static readonly Color Cream = new Color(.94f,.92f,.86f);
        private static readonly Color Gold = new Color(.96f,.68f,.28f);
        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode before editing the start scene.");
            StyleMenu();
            string path = "Assets/Scenes/Intro.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool close = !scene.IsValid() || !scene.isLoaded;
            if (close) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                foreach (var old in scene.GetRootGameObjects().Where(r=>r.name=="StartMenuPresentation").ToArray()) Object.DestroyImmediate(old);
                var root = new GameObject("StartMenuPresentation");
                var stage = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stage.name = "CoreStage"; stage.transform.SetParent(root.transform,false);
                stage.transform.localPosition = new Vector3(2.6f,-.08f,0);
                stage.transform.localScale = new Vector3(4.4f,.08f,4.4f);
                Object.DestroyImmediate(stage.GetComponent<Collider>());
                stage.GetComponent<Renderer>().sharedMaterial = MaterialAt("Assets/Prefabs/UI/StartMenuStage.mat",new Color(.06f,.07f,.085f),.48f);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name="BackdropFloor"; floor.transform.SetParent(root.transform,false);
                floor.transform.localPosition=new Vector3(0,-.17f,0); floor.transform.localScale=Vector3.one*10;
                Object.DestroyImmediate(floor.GetComponent<Collider>());
                floor.GetComponent<Renderer>().sharedMaterial=MaterialAt("Assets/Prefabs/UI/StartMenuFloor.mat",new Color(.018f,.022f,.029f),.12f);
                var pivot = new GameObject("DiagonalCorePivot"); pivot.transform.SetParent(root.transform,false);
                pivot.transform.localPosition=new Vector3(2.6f,0,0);
                pivot.transform.localRotation=Quaternion.FromToRotation(Vector3.one.normalized,Vector3.up);
                var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MainCore/Parts_MainCore_Draw.prefab");
                var core=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);
                core.name="MainCoreVisual"; core.transform.SetParent(pivot.transform,false); core.transform.localScale=Vector3.one*1.85f;
                float bottom=core.GetComponentsInChildren<Renderer>().Min(r=>r.bounds.min.y);
                pivot.transform.position += Vector3.up*(-bottom+.025f);
                var display=root.AddComponent<StartMenuCoreDisplay>();
                var so=new SerializedObject(display);so.FindProperty("core").objectReferenceValue=pivot.transform;so.ApplyModifiedPropertiesWithoutUndo();
                var camera=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First(c=>c.name=="Main Camera");
                camera.transform.position=new Vector3(0,3.3f,-10.5f);
                camera.transform.rotation=Quaternion.LookRotation(new Vector3(0,1.65f,0)-camera.transform.position);
                camera.orthographic=false;camera.fieldOfView=39;camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.014f,.018f,.024f);camera.nearClipPlane=.1f;camera.farClipPlane=150;
                foreach(var l in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Light>(true))) if(l.type==LightType.Directional) {l.intensity=.14f;l.color=new Color(.55f,.64f,.78f);}
                LightAt(root.transform,"OverheadSpotlight",LightType.Spot,new Vector3(2.6f,7,-.4f),pivot.transform.position,new Color(1f,.89f,.7f),42,12,42,true);
                LightAt(root.transform,"CoolRim",LightType.Spot,new Vector3(4,4,3),pivot.transform.position,new Color(.4f,.62f,1f),15,10,55,false);
                LightAt(root.transform,"SoftFrontFill",LightType.Point,new Vector3(1.2f,2.7f,-3),pivot.transform.position,new Color(.9f,.92f,1f),2,7,60,false);
                var fixture=GameObject.CreatePrimitive(PrimitiveType.Cylinder);fixture.name="SpotlightFixture";fixture.transform.SetParent(root.transform,false);
                fixture.transform.position=new Vector3(2.6f,7.1f,-.4f);fixture.transform.localScale=new Vector3(.38f,.16f,.38f);
                Object.DestroyImmediate(fixture.GetComponent<Collider>());fixture.GetComponent<Renderer>().sharedMaterial=stage.GetComponent<Renderer>().sharedMaterial;
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.12f,.14f,.18f);RenderSettings.skybox=null;
                EditorSceneManager.SaveScene(scene);
            }
            finally {if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(close)EditorSceneManager.CloseScene(scene,true);}
            return "Start menu styled; diagonal main core display and lighting saved.";
        }
        private static Material MaterialAt(string path,Color color,float smoothness)
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smoothness);EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;
        }
        private static void LightAt(Transform root,string name,LightType type,Vector3 position,Vector3 target,Color color,float intensity,float range,float angle,bool shadow)
        {
            var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.position=position;go.transform.rotation=Quaternion.LookRotation(target-position);
            var l=go.AddComponent<Light>();l.type=type;l.color=color;l.intensity=intensity;l.range=range;l.spotAngle=angle;l.innerSpotAngle=angle*.65f;l.shadows=shadow?LightShadows.Soft:LightShadows.None;
        }
        private static void Place(RectTransform rect,Vector2 position,Vector2 size)
        {rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;rect.localScale=Vector3.one;}
        private static void StyleMenu()
        {
            string path="Assets/Prefabs/UI/StartUI_Manager.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var transforms=root.GetComponentsInChildren<Transform>(true);
                var panel=transforms.First(t=>t.name=="Start_Panel");
                panel.GetComponent<UnityEngine.UI.Image>().color=Color.clear;
                var heavy=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NanumSquareNeo/TMP/NanumSquareNeo-Heavy Dynamic.asset");
                var regular=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NanumSquareNeo/TMP/NanumSquareNeo-Regular Dynamic.asset");
                var title=transforms.First(t=>t.name=="GameTitle").GetComponent<TextMeshProUGUI>();
                title.text="GoodGun\n<color=#F5AD47>GreatGun</color>";title.font=heavy;title.fontSize=94;title.enableAutoSizing=false;title.alignment=TextAlignmentOptions.Left;title.lineSpacing=-15;title.characterSpacing=-3;title.textWrappingMode=TextWrappingModes.NoWrap;title.raycastTarget=false;
                Place(title.rectTransform,new Vector2(-440,185),new Vector2(560,240));
                transforms.First(t=>t.name=="Game_Name_text").gameObject.SetActive(false);
                var accent=transforms.First(t=>t.name=="AccentLine").GetComponent<UnityEngine.UI.Image>();accent.color=Gold;Place(accent.rectTransform,new Vector2(-460,39),new Vector2(510,3));
                var shade=panel.Find("MenuShade");
                if(shade==null){shade=new GameObject("MenuShade",typeof(RectTransform),typeof(UnityEngine.UI.Image)).transform;shade.SetParent(panel,false);}
                var image=shade.GetComponent<UnityEngine.UI.Image>();image.color=new Color(.025f,.032f,.043f,.84f);image.raycastTarget=false;
                Place((RectTransform)shade,new Vector2(-475,0),new Vector2(700,900));shade.SetAsFirstSibling();
                var names=new[]{"KeepStartGameButton","StartGameButton","LoadGameButton","QuitGameButton"};
                for(int i=0;i<names.Length;i++)
                {
                    var button=root.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name==names[i]);
                    Place((RectTransform)button.transform,new Vector2(-460,-22-i*84),new Vector2(510,64));
                    var bg=button.GetComponent<UnityEngine.UI.Image>();bg.color=Color.white;
                    var colors=button.colors;colors.normalColor=new Color(.161f,.176f,.2f);colors.highlightedColor=new Color(.25f,.27f,.29f);colors.pressedColor=new Color(.34f,.30f,.23f);colors.selectedColor=colors.highlightedColor;colors.disabledColor=new Color(.10f,.11f,.13f,.9f);colors.fadeDuration=.12f;button.colors=colors;
                    var label=button.GetComponentInChildren<TextMeshProUGUI>(true);label.font=heavy;label.fontSize=26;label.color=Cream;label.alignment=TextAlignmentOptions.MidlineLeft;
                    var rect=label.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(26,0);rect.offsetMax=new Vector2(-20,0);
                }
                var eyebrow=panel.Find("WorkshopHeading");
                if(eyebrow==null){eyebrow=new GameObject("WorkshopHeading",typeof(RectTransform),typeof(TextMeshProUGUI)).transform;eyebrow.SetParent(panel,false);}
                var text=eyebrow.GetComponent<TextMeshProUGUI>();text.font=regular;text.fontSize=18;text.color=Gold;text.text="W E A P O N   W O R K S H O P";text.alignment=TextAlignmentOptions.MidlineLeft;text.raycastTarget=false;
                Place(text.rectTransform,new Vector2(-440,339),new Vector2(560,35));
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
