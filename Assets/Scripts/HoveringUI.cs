using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

namespace WeaponWorkshop
{
    // The controller stays on UIManager; only the authored tooltip panel is toggled.
    [DefaultExecutionOrder(60)]
    public sealed class HoveringUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;
        [SerializeField] private GameObject interact;
        [SerializeField] private ModulePaletteUI palette;
        [SerializeField] private RectTransform textArea;
        [SerializeField] private RectTransform point1;
        [SerializeField] private RectTransform point2;
        [SerializeField] private RectTransform connector;
        [SerializeField, Min(0f)] private float connectorWidth = 4f;
        [SerializeField] private Vector2 textOffset = new Vector2(40f, 40f);
        [SerializeField, Min(0f)] private float screenPadding = 12f;
        [SerializeField] private WorkshopTagsUI tagSource;
        private Interaction interaction;
        [SerializeField] private TMP_Text scanModeText;
        private PlayerScanMode scanner;
        [SerializeField, Min(1f)] private float scanLoadingDiameter = 90f;
        [SerializeField, Min(0.1f)] private float scanLoadingThickness = 3f;
        private ScanLoadingGraphic scanLoading;
        private TMP_Text scanLoadingText;
        [SerializeField, Min(0f)] private float scanTextRevealDuration = 0.25f;
        [SerializeField] private RectTransform tagGrid;
        [SerializeField] private RectTransform tagTemplate;
        private Vector2 pointerPosition;
        private readonly List<WeaponTypeData> weaponTags = new List<WeaponTypeData>();
        private readonly List<RectTransform> tagPills = new List<RectTransform>();
        private CubeBuilder builder;
        private bool muzzleTooltipObserved;
        private Color bodyColor;
        private readonly StringBuilder text = new StringBuilder();
        private readonly Dictionary<ModuleData, int> counts = new Dictionary<ModuleData, int>();
        private ModuleData resultModule;
        private Canvas resultTooltipCanvas;
        private static readonly string[] Labels = { "관통력", "연사력", "폭발력", "반사 횟수", "차지", "중력", "발열", "회전", "물", "유도", "산탄", "무게", "스코프", "가속", "컬러", "탄퍼짐" };

        public void Initialize(CubeBuilder source, Interaction interactionSource) { interaction = interactionSource; builder = source; if (tagTemplate != null) tagTemplate.gameObject.SetActive(false); if (tagGrid != null) tagGrid.gameObject.SetActive(false); bodyColor = description != null ? description.color : Color.white; Hide(); }
        private void Awake() { Hide(); if (scanModeText != null) scanModeText.gameObject.SetActive(false); }
        private void OnDisable() { muzzleTooltipObserved = false; ClearResultModule(); Hide(); if (scanModeText != null) scanModeText.gameObject.SetActive(false); }
        private void Hide() { if (interact != null) interact.SetActive(false); if (panel != null) panel.SetActive(false); if (scanLoading != null) scanLoading.gameObject.SetActive(false); }
        public void ShowResultModule(ModuleData data, Vector2 position)
        {
            if (data == null || panel == null) return;
            resultModule = data;
            pointerPosition = position;
            if (resultTooltipCanvas == null)
            {
                resultTooltipCanvas = panel.GetComponent<Canvas>();
                if (resultTooltipCanvas == null) resultTooltipCanvas = panel.AddComponent<Canvas>();
            }
            resultTooltipCanvas.overrideSorting = true;
            resultTooltipCanvas.sortingOrder = 500;
            resultTooltipCanvas.enabled = true;
            ShowModule(data);
        }
        public void ClearResultModule()
        {
            resultModule = null;
            if (resultTooltipCanvas != null) resultTooltipCanvas.enabled = false;
            Hide();
        }
        private void CreateScanLoading()
        {
            if (scanLoading != null || panel == null || title == null) return;
            var root = new GameObject("ScanLoading", typeof(RectTransform), typeof(ScanLoadingGraphic));
            root.transform.SetParent(panel.transform.parent, false);
            scanLoading = root.GetComponent<ScanLoadingGraphic>();
            var rect = scanLoading.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            scanLoading.raycastTarget = false;
            scanLoading.color = new Color(0.5f, 0.9f, 1f);
            var label = new GameObject("ScanningText", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(root.transform, false);
            scanLoadingText = label.GetComponent<TextMeshProUGUI>();
            scanLoadingText.font = title.font;
            scanLoadingText.fontSize = 26f;
            scanLoadingText.alignment = TextAlignmentOptions.Center;
            scanLoadingText.raycastTarget = false;
            scanLoadingText.color = scanLoading.color;
            scanLoadingText.rectTransform.sizeDelta = new Vector2(220f, 40f);
            root.SetActive(false);
        }
        private void AppendScanResistance(string label, float value, float highest)
        {
            if (!Mathf.Approximately(value, highest)) return;
            if (text.Length > 0) text.Append(" · ");
            text.Append(label);
        }

        private void ShowScanLoading(float progress, string label)
        {
            Hide();
            CreateScanLoading();
            if (scanLoading == null) return;
            scanLoading.rectTransform.sizeDelta = Vector2.one * scanLoadingDiameter;
            scanLoading.SetProgress(progress, scanLoadingThickness);
            scanLoadingText.rectTransform.anchoredPosition = new Vector2(0f, -scanLoadingDiameter * 0.5f - 24f);
            scanLoadingText.text = label;
            scanLoading.gameObject.SetActive(true);
        }

        private void ShowInteract()
        {
            if (interact != null) interact.SetActive(interaction != null && interaction.isActiveAndEnabled &&
                !GameManager.GameplayBlocked && interaction.CanInteractTarget);
        }
        private void LateUpdate()
        {
            if (resultModule != null)
            {
                if (tagGrid != null) tagGrid.gameObject.SetActive(false);
                if (description != null) description.gameObject.SetActive(true);
                ShowModule(resultModule);
                return;
            }
            bool hoveringMuzzle = false;
            try
            {
            if (scanner == null && interaction != null && interaction.Player != null)
                scanner = interaction.Player.GetComponent<PlayerScanMode>();
            bool scanning = scanner != null && scanner.isActiveAndEnabled && scanner.IsScanning && !GameManager.GameplayBlocked;
            if (scanModeText != null) scanModeText.gameObject.SetActive(false);
            if (scanLoading != null) scanLoading.gameObject.SetActive(false);
            if (GameManager.GameplayBlocked) { Hide(); return; }
            if (scanning)
            {
                if (!scanner.HasTarget) { ShowScanLoading(1f, "Scan mode"); return; }
                var target = scanner.Target;
                pointerPosition = interaction.HoverPointer;
                if (tagGrid != null) tagGrid.gameObject.SetActive(false);
                if (description != null) description.gameObject.SetActive(true);
                if (interact != null) interact.SetActive(false);
                if (!scanner.ScanComplete)
                {
                    ShowScanLoading(scanner.ScanProgress, "Scanning");
                    return;
                }
                text.Clear();
                if (scanner.ChestTarget != null)
                {
                    var gate = scanner.ChestTarget.CoreGate;
                    if (gate != null)
                    {
                        text.Append("방어막 통과 조건").AppendLine()
                            .Append("중력 ").Append(gate.RequiredGravity.ToString("0.##")).AppendLine(" 이상")
                            .Append("유도 ").Append(gate.RequiredHoming.ToString("0.##")).Append(" 이상");
                    }
                    else text.Append("엔딩 상자");
                }
                else
                {
                    float highest = Mathf.Max(Mathf.Max(target.PenetrationResistance, target.ExplosionResistance),
                        Mathf.Max(target.HeatResistance, target.WaterResistance));
                    if (highest > 0f)
                    {
                        AppendScanResistance("관통", target.PenetrationResistance, highest);
                        AppendScanResistance("폭발", target.ExplosionResistance, highest);
                        AppendScanResistance("발열", target.HeatResistance, highest);
                        AppendScanResistance("물", target.WaterResistance, highest);
                        text.Append(" 저항  ").Append(highest.ToString("0.##"));
                    }
                    else text.Append("저항 없음");
                }
                Show("스캔 완료", text.ToString());
                if (title != null) title.color = scanner.TargetColor;
                if (description != null) description.color = bodyColor;
                float reveal = scanTextRevealDuration <= 0f ? 1f : Mathf.Clamp01(scanner.CompletedElapsed / scanTextRevealDuration);
                if (title != null && description != null)
                {
                    title.ForceMeshUpdate();
                    description.ForceMeshUpdate();
                    int titleCount = title.textInfo.characterCount;
                    int count = Mathf.FloorToInt((titleCount + description.textInfo.characterCount) * reveal);
                    title.maxVisibleCharacters = Mathf.Min(titleCount, count);
                    description.maxVisibleCharacters = Mathf.Max(0, count - titleCount);
                }
                return;
            }
            var weapon = interaction != null ? interaction.HoveredWeapon : null;
            if (weapon == null && interaction != null && interaction.AimedTarget is GunRack rack) weapon = rack.DisplayedWeapon;
            if (interaction != null && interaction.isActiveAndEnabled && weapon != null && !weapon.IsEquipped && !weapon.IsMoving &&
                (weapon.GetComponent<PresetWeaponPreview>() != null || weapon.GetComponentInParent<Workbench>() == null || builder == null || !builder.isActiveAndEnabled))
            {
                pointerPosition = interaction.HoverPointer;
                ShowWeaponTags(weapon);
                return;
            }
            if (tagGrid != null) tagGrid.gameObject.SetActive(false);
            if (description != null) description.gameObject.SetActive(true);
            if (builder == null || !builder.isActiveAndEnabled) { if (!ShowInteractionTitle()) Hide(); return; }
            pointerPosition = builder.PointerPosition;
            int id = palette != null ? palette.HoveredModuleId : 0;
            if (id > 0 && builder.Catalog != null && builder.Catalog.TryGet(id, out var entry))
            {
                var unlocks = interaction != null && interaction.Player != null ? interaction.Player.GetComponent<PlayerModuleUnlocks>() : null;
                if (unlocks != null && !unlocks.IsUnlocked(id))
                {
                    Show(entry.displayName, "잠긴 모듈\n" + TableManager.Instance.RaidMap.Locations(entry.data));
                    if (title != null) title.color = entry.color;
                    if (description != null) description.color = bodyColor;
                }
                else ShowModule(entry.data);
                return;
            }
            if (!builder.TryGetHoveredModule(out RaycastHit hit)) { if (!ShowInteractionTitle()) Hide(); return; }
            // Workshop raycasts reach farther than interaction; preview hits still describe the whole weapon.
            var preview = hit.collider.GetComponentInParent<PresetWeaponPreview>();
            if (preview != null)
            {
                var previewWeapon = preview.GetComponent<EquipableWeapon>();
                if (previewWeapon != null) ShowWeaponTags(previewWeapon); else Hide();
                return;
            }
            CubeAssembly assembly = hit.collider.GetComponentInParent<CubeAssembly>();
            if (assembly == null) { if (!ShowInteractionTitle()) Hide(); return; }
            PlacedBlock block = hit.collider.GetComponentInParent<PlacedBlock>();
            PowerCore mainCore = assembly.GetComponent<PowerCore>();
            if (block == null && mainCore != null) { ShowCore(mainCore); return; }
            if (assembly.TryGetAttachCell(hit, out var cell, out var face))
            {
                foreach (var muzzle in assembly.GetProjectilePaths())
                {
                    PlacedBlock end = muzzle.path.Length > 0 ? muzzle.path[muzzle.path.Length - 1] : null;
                    // An exposed output must be reachable from the core, on this exact block and face.
                    if (end != block || muzzle.direction != face || cell != (end != null ? end.Cell : Vector3Int.zero) + face) continue;
                    PowerCore core = assembly.GetComponent<PowerCore>();
                    if (core != null)
                    {
                        ShowMuzzle(core, muzzle.path);
                        var tutorial = TutorialManager.Instance;
                        if (panel != null && panel.activeInHierarchy && title != null && description != null &&
                            tutorial != null && tutorial.CurrentStep?.requiredSignal == "muzzle-stats-viewed" &&
                            tutorial.TutorialWorkbench != null && tutorial.TutorialWorkbench.CurrentWeapon != null &&
                            assembly == tutorial.TutorialWorkbench.CurrentWeapon.GetComponentInChildren<CubeAssembly>())
                            hoveringMuzzle = true;
                        return;
                    }
                }
            }
            ModuleData data = block != null ? block.GetComponent<ModuleStats>()?.Data : null;
            if (data == null && block != null && builder.Catalog != null && builder.Catalog.TryGet(block.ModuleSlot, out var module)) data = module.data;
            if (data != null) ShowModule(data); else Hide();
            }
            finally
            {
                var tutorial = TutorialManager.Instance;
                if (!hoveringMuzzle && muzzleTooltipObserved && !GameManager.GameplayBlocked &&
                    tutorial != null && tutorial.CurrentStep?.requiredSignal == "muzzle-stats-viewed")
                    tutorial.NotifySignal("muzzle-stats-viewed", tutorial.TutorialWorkbench);
                muzzleTooltipObserved = hoveringMuzzle;
            }
        }
        /// <summary>
        /// 조준 대상의 상호작용 제목과 현재 비용 조건을 호버 UI에 표시합니다.
        /// </summary>
        private bool ShowInteractionTitle()
        {
            if (interaction == null || !interaction.isActiveAndEnabled || !(interaction.AimedTarget is Component target)) return false;
            var label = target.GetComponent<InteractionLabel>();
            if (label == null || string.IsNullOrWhiteSpace(label.Title)) return false;
            pointerPosition = interaction.HoverPointer;
            if (tagGrid != null) tagGrid.gameObject.SetActive(false);
            bool available = interaction.CanInteractTarget;
            bool tutorialBlocked = TutorialManager.Instance != null && !TutorialManager.Instance.AllowsInteraction(interaction.AimedTarget);
            bool slotMachine = target is SlotMachineReels;
            bool retire = target is RetireButton;
            bool manual = target is WorkshopManual && label.Title == "매뉴얼";
            if (description != null)
            {
                description.gameObject.SetActive((slotMachine || !tutorialBlocked) && (slotMachine || retire || manual || !available));
                description.color = slotMachine ? Color.white : bodyColor;
            }
            string condition = label.UnavailableCondition;
            if (target is SlotMachineReels machine)
                condition = machine.GetHoverDescription(PlayerWallet.ForPlayer(interaction.Player));
            else if (retire)
            {
                PlayerWallet wallet = PlayerWallet.ForPlayer(interaction.Player);
                int cost = RetireUI.Cost;
                string amount = "$" + cost.ToString("N0", CultureInfo.InvariantCulture);
                string shownAmount = wallet == null || wallet.Balance < cost
                    ? $"<color=#FF0000>{amount}</color>" : amount;
                condition = condition.Replace("{RETIRE_COST}", shownAmount);
            }
            Show(label.Title, (tutorialBlocked && !slotMachine) || (!slotMachine && !retire && !manual && available) ? string.Empty : condition,
                retire || slotMachine);
            ShowInteract();
            if (title != null) title.color = Color.white;
            return true;
        }
        private void ShowWeaponTags(EquipableWeapon weapon)
        {
            if (tagSource == null || tagTemplate == null || tagGrid == null || textArea == null) { Hide(); return; }
            var core = weapon.GetComponentInChildren<PowerCore>();
            tagSource.GetMatchedTags(core, weaponTags);
            tagGrid.gameObject.SetActive(weaponTags.Count > 0);
            for (int i = 0; i < weaponTags.Count; i++)
            {
                if (i >= tagPills.Count)
                {
                    var pill = Instantiate(tagTemplate, tagGrid);
                    foreach (var graphic in pill.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
                    tagPills.Add(pill);
                }
                tagPills[i].gameObject.SetActive(true);
                tagPills[i].GetComponentInChildren<TMP_Text>(true).text = weaponTags[i].DisplayName;
                tagPills[i].GetComponent<TagPillGraphic>().color = weaponTags[i].TagColor;
            }
            for (int i = weaponTags.Count; i < tagPills.Count; i++) tagPills[i].gameObject.SetActive(false);
            var preview = weapon.GetComponent<PresetWeaponPreview>();
            description.gameObject.SetActive(false);
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(textArea);
            Show(weapon.WeaponName, string.Empty);
            // A long workbench ray may describe another preview beyond interaction range.
            if (preview == null || interaction != null && interaction.AimedTarget == preview) ShowInteract();
            title.color = Color.white;
            description.color = bodyColor;
        }
        private void ShowCore(PowerCore core)
        {
            CoreBaseStats data = core.GetComponent<StatCollector>()?.BaseStats;
            WeaponStats stats = data != null ? data.Stats : core.GetMuzzleStats(System.Array.Empty<PlacedBlock>());
            text.Clear();
            text.AppendLine(data != null && !string.IsNullOrWhiteSpace(data.Description) ? data.Description : "무기의 중심이 되는 코어입니다. 여섯 면으로 탄환을 내보냅니다.");
            text.AppendLine().AppendLine("코어 기본 스펙");
            foreach (StatType type in WeaponStats.AllTypes)
            {
                float value = stats.Get(type);
                if (Mathf.Approximately(value, 0f)) continue;
                text.Append("• ").Append(Labels[(int)type]).Append(' ').Append(value.ToString("0.##")).AppendLine();
            }
            Show("메인 코어", text.ToString().TrimEnd());
            if (title != null) title.color = Color.white;
            if (description != null) description.color = bodyColor;
        }
        private void ShowModule(ModuleData data)
        {
            Show(data.DisplayName, string.IsNullOrWhiteSpace(data.Description) ? "설명이 아직 등록되지 않은 모듈입니다." : data.Description);
            if (title != null) title.color = data.Color;
            if (description != null) description.color = bodyColor;
        }
        private void ShowMuzzle(PowerCore core, PlacedBlock[] path)
        {
            text.Clear(); counts.Clear();
            foreach (PlacedBlock block in path)
            {
                if (block == null) continue;
                ModuleData data = block.GetComponent<ModuleStats>()?.Data;
                if (data == null && builder.Catalog != null && builder.Catalog.TryGet(block.ModuleSlot, out var entry)) data = entry.data;
                if (data != null) counts[data] = counts.TryGetValue(data, out int count) ? count + 1 : 1;
            }
            text.AppendLine("적용 모듈");
            foreach (var pair in counts) text.Append("• ").Append(pair.Key.DisplayName).Append(" × ").Append(pair.Value).AppendLine();
            if (counts.Count == 0) text.AppendLine("• 코어 기본값");
            text.AppendLine().AppendLine("탄환 스펙");
            WeaponStats stats = core.GetMuzzleStats(path);
            foreach (StatType type in WeaponStats.AllTypes)
            {
                if (type == StatType.Weight || type == StatType.Scope || type == StatType.Color) continue;
                float value = stats.Get(type);
                if (Mathf.Approximately(value, 0f)) continue;
                text.Append("• ").Append(Labels[(int)type]).Append(' ').Append(value.ToString("0.##")).AppendLine();
            }
            Show("총구 정보", text.ToString().TrimEnd());
            if (title != null) title.color = Color.white;
            if (description != null) description.color = Color.white;
        }
        private void Show(string heading, string body, bool richText = false)
        {
            if (title != null) title.maxVisibleCharacters = int.MaxValue;
            if (description != null) description.maxVisibleCharacters = int.MaxValue;
            if (interact != null) interact.SetActive(false);
            if (panel == null || title == null || description == null) return;
            description.richText = richText;
            bool changed = title.text != heading || description.text != body;
            if (title.text != heading) title.text = heading;
            if (description.text != body) description.text = body;
            if (!panel.activeSelf) panel.SetActive(true);
            if (changed && textArea != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(textArea);
            RectTransform parent = panel.transform.parent as RectTransform;
            Canvas canvas = panel.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, pointerPosition, camera, out Vector2 point))
            {
                panel.transform.localPosition = new Vector3(point.x, point.y, 0f);
                Vector2 offset = textOffset;
                if (textArea != null)
                {
                    Rect bounds = parent.rect;
                    offset.x = Mathf.Clamp(point.x + textOffset.x, bounds.xMin + screenPadding,
                        Mathf.Max(bounds.xMin + screenPadding, bounds.xMax - textArea.rect.width - screenPadding)) - point.x;
                    offset.y = Mathf.Clamp(point.y + textOffset.y, bounds.yMin + screenPadding,
                        Mathf.Max(bounds.yMin + screenPadding, bounds.yMax - textArea.rect.height - screenPadding)) - point.y;
                    textArea.anchoredPosition = offset;
                }
                // Point1 follows the cursor until Point2 would leave the top or right edge of the text area.
                // Never reverse either axis of the connection at a screen edge.
                Vector2 secondPoint = new Vector2(Mathf.Max(0f, offset.x), Mathf.Max(0f, offset.y));
                if (textArea != null && point2 != null)
                {
                    float radius = point2.rect.height * .5f;
                    float right = offset.x + textArea.rect.xMax - point2.rect.width * .5f;
                    secondPoint.x = Mathf.Min(secondPoint.x, Mathf.Max(offset.x, right));
                    float top = offset.y + textArea.rect.yMax - radius;
                    secondPoint.y = Mathf.Min(secondPoint.y, Mathf.Max(offset.y, top));
                }
                if (point1 != null) point1.anchoredPosition = new Vector2(Mathf.Min(0f, secondPoint.x), Mathf.Min(0f, secondPoint.y));
                if (point2 != null) point2.anchoredPosition = secondPoint;
                if (connector != null && point1 != null && point2 != null)
                {
                    Vector3 first = connector.parent.InverseTransformPoint(point1.position);
                    Vector3 second = connector.parent.InverseTransformPoint(point2.position);
                    Vector2 delta = second - first;
                    connector.localPosition = (first + second) * .5f;
                    connector.sizeDelta = new Vector2(delta.magnitude, connectorWidth);
                    connector.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                }
            }
        }
    }
}
