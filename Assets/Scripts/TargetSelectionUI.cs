using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class TargetSelectionUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform grid;
        [SerializeField] private UnityEngine.UI.Button buttonTemplate;
        [SerializeField] private UnityEngine.UI.Button closeButton;
        [SerializeField] private GameObject[] targetPrefabs = new GameObject[0];
        [SerializeField] private string[] targetNames = new string[0];
        [SerializeField] private ShootingTestData[] timedTests = new ShootingTestData[0];
        private readonly System.Collections.Generic.List<UnityEngine.UI.Button> testButtons = new System.Collections.Generic.List<UnityEngine.UI.Button>();
        private TMPro.TextMeshProUGUI statusText;
        private TargetBell bell;
        private GameManager game;


        [SerializeField] private GameObject customTargetPrefab;
        public int GetSavePrefabIndex(GameObject prefab) => prefab == customTargetPrefab ? -2 : System.Array.IndexOf(targetPrefabs, prefab);
        public GameObject GetSavePrefab(int slot) => slot == -2 ? customTargetPrefab : slot >= 0 && slot < targetPrefabs.Length ? targetPrefabs[slot] : null;
        private GameObject customPanel;

        private void SetCustomVisible(bool visible)
        {
            if (customPanel == null) return;
            customPanel.SetActive(visible);
        }
        private readonly TMPro.TMP_InputField[] customInputs = new TMPro.TMP_InputField[7];

        private void BuildCustomPanel()
        {
            var custom = Instantiate(buttonTemplate, grid);
            custom.name = "CustomCube";
            custom.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text = "커스텀 큐브";
            custom.onClick.AddListener(() => { SetCustomVisible(!customPanel.activeSelf); if (statusText != null) statusText.text = ""; });
            custom.gameObject.SetActive(true);
            customPanel = new GameObject("CustomCubeSettings", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            customPanel.transform.SetParent(grid.parent, false);
            var root = (RectTransform)customPanel.transform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f,0f);
            root.pivot = new Vector2(0.5f,1f);
            root.anchoredPosition = new Vector2(0f,-12f);
            root.sizeDelta = new Vector2(460f,350f);
            customPanel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.08f,0.1f,0.14f,1f);
            var font = buttonTemplate.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).font;
            string[] labels = { "가로 (m)", "세로 (m)", "높이 (m)", "관통 저항", "폭발 저항", "화속성 저항", "수속성 저항" };
            for (int i=0;i<7;i++)
            {
                var label = new GameObject("Label"+i, typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                label.transform.SetParent(root,false);
                var text = label.GetComponent<TMPro.TextMeshProUGUI>();
                text.font = font; text.fontSize = 22; text.text = labels[i]; text.color = Color.white;
                text.raycastTarget = false;
                var r = text.rectTransform; r.anchorMin=r.anchorMax=new Vector2(0f,1f);
                r.pivot=new Vector2(0f,1f); r.anchoredPosition=new Vector2(24f,-18f-i*38f);
                r.sizeDelta=new Vector2(230f,32f);
                var input = new GameObject("Value"+i,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(TMPro.TMP_InputField));
                input.transform.SetParent(root,false);
                var ir = (RectTransform)input.transform; ir.anchorMin=ir.anchorMax=new Vector2(1f,1f);
                ir.pivot=new Vector2(1f,1f); ir.anchoredPosition=new Vector2(-24f,-18f-i*38f);
                ir.sizeDelta=new Vector2(150f,32f);
                input.GetComponent<UnityEngine.UI.Image>().color=new Color(0.2f,0.24f,0.3f);
                var value = new GameObject("Text",typeof(RectTransform),typeof(TMPro.TextMeshProUGUI));
                value.transform.SetParent(input.transform,false);
                var vt=value.GetComponent<TMPro.TextMeshProUGUI>(); vt.font=font; vt.fontSize=22;
                vt.color=Color.white; vt.alignment=TMPro.TextAlignmentOptions.MidlineLeft; vt.raycastTarget=false;
                vt.rectTransform.anchorMin=Vector2.zero; vt.rectTransform.anchorMax=Vector2.one;
                vt.rectTransform.offsetMin=new Vector2(8f,2f); vt.rectTransform.offsetMax=new Vector2(-8f,-2f);
                var field=input.GetComponent<TMPro.TMP_InputField>();
                field.textComponent=vt; field.textViewport=input.GetComponent<RectTransform>(); field.targetGraphic=input.GetComponent<UnityEngine.UI.Image>();
                field.contentType=TMPro.TMP_InputField.ContentType.DecimalNumber;
                field.text=i<3 ? "3" : "0"; customInputs[i]=field;
            }
            var spawn=Instantiate(buttonTemplate,root);
            spawn.name="SpawnCustomCube"; spawn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text="소환";
            var sr=(RectTransform)spawn.transform; sr.anchorMin=sr.anchorMax=new Vector2(0.5f,0f);
            sr.pivot=new Vector2(0.5f,0f); sr.anchoredPosition=new Vector2(0f,14f); sr.sizeDelta=new Vector2(190f,40f);
            spawn.onClick.AddListener(SpawnCustom);
            SoundManager.RegisterButtonSound(spawn, "SpawnButtonClick");
            spawn.gameObject.SetActive(true);
            customPanel.SetActive(false);
        }

        private void SpawnCustom()
        {
            if (bell == null) return;
            float[] values = new float[7];
            for (int i=0;i<values.Length;i++)
            {
                if (!float.TryParse(customInputs[i].text,System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,out values[i]) ||
                    float.IsNaN(values[i]) || float.IsInfinity(values[i]) || values[i]<0f || (i<3 && values[i]<0.01f))
                { statusText.text="크기는 0.01 이상, 저항은 0 이상의 숫자를 입력하세요."; return; }
            }
            // Protect the same voxel representation used by all range targets from accidental enormous input.
            if (System.Math.Ceiling(values[0]/0.2)*System.Math.Ceiling(values[1]/0.2)*System.Math.Ceiling(values[2]/0.2)>125000)
            { statusText.text="크기가 너무 큽니다. 크기값을 줄여 주세요."; return; }
            if (bell.SpawnCustom(customTargetPrefab,new Vector3(values[0],values[2],values[1]),
                new [] { values[3],values[4],values[5],values[6] })) Close();
            else statusText.text=bell.SpawnFailureReason ?? "소환할 수 없습니다.";
        }


        private void Awake()
        {
            panel?.SetActive(false);
            SetCustomVisible(false);
            bell = null;
            game = GameManager.Instance;
            if (game != null) game.TargetSelectionCloseRequested += Close;
            closeButton?.onClick.AddListener(Close);
            if (grid == null || buttonTemplate == null) return;
            buttonTemplate.gameObject.SetActive(false);
            for (int i = 0; i < targetPrefabs.Length; i++)
            {
                int index = i;
                var button = Instantiate(buttonTemplate, grid);
                button.name = "Target" + i;
                button.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text =
                    i < targetNames.Length ? targetNames[i] : targetPrefabs[i] != null ? targetPrefabs[i].name : "없음";
                button.interactable = targetPrefabs[i] != null;
                button.onClick.AddListener(() => Select(index));
                SoundManager.RegisterButtonSound(button, "SpawnButtonClick");
                button.gameObject.SetActive(true);
            }
            for (int i = 0; i < timedTests.Length; i++)
            {
                int index = i;
                var button = Instantiate(buttonTemplate, grid);
                button.name = "TimedTest" + i;
                var data = timedTests[i];
                button.interactable = data != null && data.IsAvailable;
                button.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text = data != null
                    ? data.DisplayName + " · " + data.TimeLimit.ToString("0") + "초" : "미설정 테스트";
                button.onClick.AddListener(() => SelectTimedTest(index));
                SoundManager.RegisterButtonSound(button, "SpawnButtonClick");
                testButtons.Add(button); button.gameObject.SetActive(true);
            }
            var cleanup = Instantiate(buttonTemplate, grid.parent);
            cleanup.name = "ClearSpawnedTargets";
            var cleanupRect = (RectTransform)cleanup.transform;
            cleanupRect.anchorMin = cleanupRect.anchorMax = cleanupRect.pivot = Vector2.zero;
            cleanupRect.anchoredPosition = new Vector2(24f,20f);
            cleanupRect.sizeDelta = new Vector2(180f,42f);
            cleanup.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text = "내부 청소";
            cleanup.interactable = true;
            var cleanupColors = cleanup.colors;
            cleanupColors.normalColor = new Color(0.8f,0.12f,0.12f,1f);
            cleanupColors.highlightedColor = new Color(1f,0.25f,0.25f,1f);
            cleanupColors.pressedColor = new Color(0.55f,0.06f,0.06f,1f);
            cleanupColors.selectedColor = cleanupColors.highlightedColor;
            cleanup.colors = cleanupColors;
            if (cleanup.targetGraphic != null) cleanup.targetGraphic.color = Color.white;
            var statusRoot = new GameObject("SpawnStatus",typeof(RectTransform),typeof(TMPro.TextMeshProUGUI));
            statusRoot.transform.SetParent(grid.parent,false);
            statusText = statusRoot.GetComponent<TMPro.TextMeshProUGUI>();
            var templateText = buttonTemplate.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            statusText.font = templateText.font;
            statusText.fontSize = 20;
            statusText.color = new Color(1f,0.35f,0.35f,1f);
            statusText.alignment = TMPro.TextAlignmentOptions.Midline;
            statusText.raycastTarget = false;
            statusText.text = "";
            var rect = statusText.rectTransform;
            rect.anchorMin = new Vector2(0f,0f);
            rect.anchorMax = new Vector2(1f,0f);
            rect.pivot = new Vector2(0.5f,0f);
            rect.sizeDelta = new Vector2(-32f,28f);
            rect.anchoredPosition = new Vector2(0f,70f);
            cleanup.onClick.AddListener(() => { if (bell != null) { bell.ClearSpawned(); if (statusText != null) statusText.text = ""; } });
            cleanup.gameObject.SetActive(true);
            BuildCustomPanel();
        }

        public bool Open(TargetBell source)
        {
            if (source == null || panel == null || GameManager.GameplayBlocked) return false;
            game = GameManager.Instance;
            if (game == null) return false;
            bell = source;
            for (int i = 0; i < testButtons.Count; i++)
                testButtons[i].interactable = source.TestController != null && timedTests[i] != null && timedTests[i].IsAvailable;
            grid.gameObject.SetActive(true);
            SetCustomVisible(false);
            if (statusText != null) statusText.text = "";
            game.SetTargetSelection(true);
            panel.SetActive(true);
            return true;
        }

        private void Select(int index)
        {
            if (bell == null || index < 0 || index >= targetPrefabs.Length) return;
            if (bell.Spawn(targetPrefabs[index])) Close();
            else if (statusText != null) statusText.text = bell.SpawnFailureReason ?? "소환할 수 없습니다.";
        }

        private void SelectTimedTest(int index)
        {
            if (bell == null || index < 0 || index >= timedTests.Length || timedTests[index] == null || !timedTests[index].IsAvailable) return;
            var source = bell; // Close clears the UI's bell reference.
            var player = GameManager.Instance != null && GameManager.Instance.UI != null ? GameManager.Instance.UI.Player : null;
            if (!source.PrepareTest(timedTests[index], player))
            { if (statusText != null) statusText.text = source.SpawnFailureReason; return; }
            Close();
            source.TestController.Begin();
        }

        public void Close()
        {
            panel?.SetActive(false);
            SetCustomVisible(false);
            bell = null;
            if (game != null) game.SetTargetSelection(false);
        }

        private void LateUpdate()
        {
            if (panel != null && panel.activeSelf && (bell == null || !bell.isActiveAndEnabled)) Close();
        }

        private void OnDisable() => Close();
        private void OnDestroy()
        {
            closeButton?.onClick.RemoveListener(Close);
            if (game != null) game.TargetSelectionCloseRequested -= Close;
        }
    }
}
