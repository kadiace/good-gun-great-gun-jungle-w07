using System.Collections.Generic;
using System.Globalization;
using System.Text;

using UnityEngine;

namespace WeaponWorkshop
{
    // 호버·스캔 대상 판정, 표시 문구 가공, 튜토리얼 신호 전달을 담당한다.
    // 표시는 HoverTooltipView의 명시적 함수만 호출한다.
    public sealed class HoverTooltipPresenter
    {
        private const string SCREEN_NAME = "HoverTooltipPresenter";

        private static readonly string[] LABELS = { "관통력", "연사력", "폭발력", "반사 횟수", "차지", "중력", "발열", "회전", "물", "유도", "산탄", "무게", "스코프", "가속", "컬러", "탄퍼짐" };

        private readonly HoverTooltipView m_view;
        private readonly ModulePaletteUI m_palette;
        private readonly WorkshopTagsUI m_tagSource;
        private readonly StringBuilder m_text = new StringBuilder();
        private readonly Dictionary<ModuleData, int> m_counts = new Dictionary<ModuleData, int>();
        private readonly List<WeaponTypeData> m_weaponTags = new List<WeaponTypeData>();

        private Interaction m_interaction;
        private float m_nextInteractionSearch;
        private readonly HashSet<int> m_warnedLabelTargets = new HashSet<int>();
        private CubeBuilder m_builder;
        private PlayerScanMode m_scanner;
        private bool m_muzzleTooltipObserved;
        private ModuleData m_resultModule;
        private string m_completedScanText;
        private Color m_completedScanColor;
        private Vector2 m_completedScanPointer;
        private float m_completedScanVisibleUntil;
        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public HoverTooltipPresenter(HoverTooltipView _view, ModulePaletteUI _palette, WorkshopTagsUI _tagSource)
        {
            m_view = _view;
            m_palette = _palette;
            m_tagSource = _tagSource;
        }

        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null)
            {
                if (!m_hasLoggedDependencyError)
                {
                    m_hasLoggedDependencyError = true;
                    Debug.LogError(
                        $"[{SCREEN_NAME}] Initialize 실패: HoverTooltipView 참조가 없습니다. " +
                        "UIManager.prefab의 HoverOrder 저작을 실행하세요.");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_isInitialized = true;
            Hide();
            return true;
        }

        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.Dispose();
            }

            m_muzzleTooltipObserved = false;
            m_isInitialized = false;
        }

        /// <summary>
        /// UIManager가 해석한 편집 도구와 상호작용 참조를 반영합니다. 스캔 대상은 Interaction의 플레이어에서 지연 해석합니다.
        /// </summary>
        public void SetSources(CubeBuilder _builder, Interaction _interaction)
        {
            m_builder = _builder;
            m_interaction = _interaction;
        }

        // UIManager가 플레이어보다 먼저 깨어나 Interaction을 못 찾았거나 플레이어가 교체된 경우 상호작용 라벨이 통째로 사라지므로 늦게라도 다시 찾는다.
        private void ResolveInteraction()
        {
            if (m_interaction != null || Time.unscaledTime < m_nextInteractionSearch)
            {
                return;
            }

            m_nextInteractionSearch = Time.unscaledTime + 1f;
            m_interaction = Object.FindFirstObjectByType<Interaction>();
            m_scanner = null;
        }

        public void Hide()
        {
            if (m_view == null)
            {
                return;
            }

            m_view.Hide();
            m_view.SetScanModeTextVisible(false);
        }

        public void HandleDisabled()
        {
            m_muzzleTooltipObserved = false;
            m_completedScanVisibleUntil = 0f;
            ClearResultModule();
            Hide();
        }

        public void ShowResultModule(ModuleData _module, Vector2 _position)
        {
            if (!m_isInitialized || m_view == null || _module == null)
            {
                return;
            }

            m_resultModule = _module;
            m_view.SetPointerPosition(_position);
            ShowResultModuleNow();
        }

        public void ClearResultModule()
        {
            if (m_resultModule == null)
            {
                return;
            }

            m_resultModule = null;
            Hide();
        }

        private void ShowResultModuleNow()
        {
            m_view.SetTagGridVisible(false);
            m_view.SetDescriptionVisible(true);
            m_view.SetInteractVisible(false);
            ShowModule(m_resultModule);
        }

        public void Tick()
        {
            if (!m_isInitialized || m_view == null)
            {
                return;
            }

            // 결과 화면 모듈 상세는 게임 조작이 막힌 결과 화면에서도 유지한다.
            if (m_resultModule != null)
            {
                ShowResultModuleNow();
                return;
            }

            bool hoveringMuzzle = false;
            try
            {
                ResolveInteraction();
                if (m_scanner == null && m_interaction != null && m_interaction.Player != null)
                {
                    m_scanner = m_interaction.Player.GetComponent<PlayerScanMode>();
                }

                bool scanning = m_scanner != null && m_scanner.isActiveAndEnabled && m_scanner.IsScanning && !GameManager.GameplayBlocked;
                m_view.SetScanModeTextVisible(false);
                m_view.SetScanLoadingVisible(false);
                if (GameManager.GameplayBlocked)
                {
                    m_completedScanVisibleUntil = 0f;
                    Hide();
                    return;
                }

                if (scanning)
                {
                    ShowScan();
                    return;
                }

                if (Time.unscaledTime < m_completedScanVisibleUntil)
                {
                    m_view.SetPointerPosition(m_completedScanPointer);
                    m_view.SetTagGridVisible(false);
                    m_view.SetDescriptionVisible(true);
                    m_view.Show("스캔 완료", m_completedScanText);
                    m_view.SetTitleColor(m_completedScanColor);
                    m_view.SetDescriptionColor(m_view.BodyColor);
                    return;
                }

                EquipableWeapon weapon = m_interaction != null ? m_interaction.HoveredWeapon : null;
                if (weapon == null && m_interaction != null && m_interaction.AimedTarget is GunRack rack)
                {
                    weapon = rack.DisplayedWeapon;
                }

                if (m_interaction != null && m_interaction.isActiveAndEnabled && weapon != null && !weapon.IsEquipped && !weapon.IsMoving &&
                    (weapon.GetComponent<PresetWeaponPreview>() != null || weapon.GetComponentInParent<Workbench>() == null || m_builder == null || !m_builder.isActiveAndEnabled))
                {
                    m_view.SetPointerPosition(m_interaction.HoverPointer);
                    ShowWeaponTags(weapon);
                    return;
                }

                m_view.SetTagGridVisible(false);
                m_view.SetDescriptionVisible(true);
                if (m_builder == null || !m_builder.isActiveAndEnabled)
                {
                    if (!ShowInteractionTitle())
                    {
                        Hide();
                    }

                    return;
                }

                m_view.SetPointerPosition(m_builder.PointerPosition);
                int id = m_palette != null ? m_palette.HoveredModuleId : 0;
                if (id > 0 && m_builder.Catalog != null && m_builder.Catalog.TryGet(id, out var entry))
                {
                    var unlocks = m_interaction != null && m_interaction.Player != null
                        ? m_interaction.Player.GetComponent<PlayerModuleUnlocks>() : null;
                    bool isLocked = m_builder != null
                        ? m_builder.IsModuleLocked(id)
                        : unlocks != null && !unlocks.IsUnlocked(id);
                    if (isLocked)
                    {
                        m_view.Show(entry.displayName, "잠긴 모듈\n" + TableManager.Instance.RaidMap.Locations(entry.data));
                        m_view.SetTitleColor(entry.color);
                        m_view.SetDescriptionColor(m_view.BodyColor);
                    }
                    else
                    {
                        ShowModule(entry.data);
                    }

                    return;
                }

                if (!m_builder.TryGetHoveredModule(out RaycastHit hit))
                {
                    if (!ShowInteractionTitle())
                    {
                        Hide();
                    }

                    return;
                }

                // Workshop raycasts reach farther than interaction; preview hits still describe the whole weapon.
                var preview = hit.collider.GetComponentInParent<PresetWeaponPreview>();
                if (preview != null)
                {
                    var previewWeapon = preview.GetComponent<EquipableWeapon>();
                    if (previewWeapon != null)
                    {
                        ShowWeaponTags(previewWeapon);
                    }
                    else
                    {
                        Hide();
                    }

                    return;
                }

                CubeAssembly assembly = hit.collider.GetComponentInParent<CubeAssembly>();
                if (assembly == null)
                {
                    if (!ShowInteractionTitle())
                    {
                        Hide();
                    }

                    return;
                }

                PlacedBlock block = hit.collider.GetComponentInParent<PlacedBlock>();
                PowerCore mainCore = assembly.GetComponent<PowerCore>();
                if (block == null && mainCore != null)
                {
                    ShowCore(mainCore);
                    return;
                }

                if (assembly.TryGetAttachCell(hit, out var cell, out var face))
                {
                    foreach (var muzzle in assembly.GetProjectilePaths())
                    {
                        PlacedBlock end = muzzle.path.Length > 0 ? muzzle.path[muzzle.path.Length - 1] : null;
                        // An exposed output must be reachable from the core, on this exact block and face.
                        if (end != block || muzzle.direction != face || cell != (end != null ? end.Cell : Vector3Int.zero) + face)
                        {
                            continue;
                        }

                        PowerCore core = assembly.GetComponent<PowerCore>();
                        if (core != null)
                        {
                            ShowMuzzle(core, muzzle.path);
                            var tutorial = TutorialManager.Instance;
                            if (m_view.IsPanelVisibleActive &&
                                tutorial != null && tutorial.CurrentStep?.requiredSignal == "muzzle-stats-viewed" &&
                                tutorial.TutorialWorkbench != null && tutorial.TutorialWorkbench.CurrentWeapon != null &&
                                assembly == tutorial.TutorialWorkbench.CurrentWeapon.GetComponentInChildren<CubeAssembly>())
                            {
                                hoveringMuzzle = true;
                            }

                            return;
                        }
                    }
                }

                ModuleData data = block != null ? block.GetComponent<ModuleStats>()?.Data : null;
                if (data == null && block != null && m_builder.Catalog != null && m_builder.Catalog.TryGet(block.ModuleSlot, out var module))
                {
                    data = module.data;
                }

                if (data != null)
                {
                    ShowModule(data);
                }
                else
                {
                    Hide();
                }
            }
            finally
            {
                var tutorial = TutorialManager.Instance;
                if (!hoveringMuzzle && m_muzzleTooltipObserved && !GameManager.GameplayBlocked &&
                    tutorial != null && tutorial.CurrentStep?.requiredSignal == "muzzle-stats-viewed")
                {
                    tutorial.NotifySignal("muzzle-stats-viewed", tutorial.TutorialWorkbench);
                }

                m_muzzleTooltipObserved = hoveringMuzzle;
            }
        }

        private void ShowScan()
        {
            if (!m_scanner.HasTarget)
            {
                m_completedScanVisibleUntil = 0f;
                m_view.ShowScanLoading(1f, "Scan mode");
                return;
            }

            var target = m_scanner.Target;
            m_view.SetPointerPosition(m_interaction.HoverPointer);
            m_view.SetTagGridVisible(false);
            m_view.SetDescriptionVisible(true);
            m_view.SetInteractVisible(false);
            if (!m_scanner.ScanComplete)
            {
                m_completedScanVisibleUntil = 0f;
                m_view.ShowScanLoading(m_scanner.ScanProgress, "Scanning");
                return;
            }

            m_text.Clear();
            if (m_scanner.ChestTarget != null)
            {
                var gate = m_scanner.ChestTarget.CoreGate;
                if (gate != null)
                {
                    m_text.Append("방어막 통과 조건").AppendLine()
                        .Append("중력 ").Append(gate.RequiredGravity.ToString("0.##")).AppendLine(" 이상")
                        .Append("유도 ").Append(gate.RequiredHoming.ToString("0.##")).Append(" 이상");
                }
                else
                {
                    m_text.Append("엔딩 상자");
                }
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
                    m_text.Append(" 저항  ").Append(highest.ToString("0.##"));
                }
                else
                {
                    m_text.Append("저항 없음");
                }
            }

            m_view.Show("스캔 완료", m_text.ToString());
            m_view.SetTitleColor(m_scanner.TargetColor);
            m_view.SetDescriptionColor(m_view.BodyColor);
            m_completedScanText = m_text.ToString();
            m_completedScanColor = m_scanner.TargetColor;
            m_completedScanPointer = m_interaction.HoverPointer;
            m_completedScanVisibleUntil = Time.unscaledTime + 2f;
        }

        private void AppendScanResistance(string _label, float _value, float _highest)
        {
            if (!Mathf.Approximately(_value, _highest))
            {
                return;
            }

            if (m_text.Length > 0)
            {
                m_text.Append(" · ");
            }

            m_text.Append(_label);
        }

        /// <summary>
        /// 조준 대상의 상호작용 제목과 현재 비용 조건을 호버 UI에 표시합니다.
        /// </summary>
        private bool ShowInteractionTitle()
        {
            if (m_interaction == null || !m_interaction.isActiveAndEnabled || !(m_interaction.AimedTarget is Component target))
            {
                return false;
            }

            var label = target.GetComponent<InteractionLabel>();
            if (label == null || string.IsNullOrWhiteSpace(label.Title))
            {
                // 무기류는 태그 UI가 이름을 대신하므로 라벨이 없어도 정상이다.
                if (!(target is EquipableWeapon) && !(target is PresetWeaponPreview) && m_warnedLabelTargets.Add(target.GetInstanceID()))
                {
                    Debug.LogWarning($"[{SCREEN_NAME}] {target.name}({target.GetType().Name})에 InteractionLabel 또는 Title이 없어 이름을 표시할 수 없습니다.", target);
                }

                return false;
            }

            m_view.SetPointerPosition(m_interaction.HoverPointer);
            m_view.SetTagGridVisible(false);
            bool available = m_interaction.CanInteractTarget;
            bool tutorialBlocked = TutorialManager.Instance != null && !TutorialManager.Instance.AllowsInteraction(m_interaction.AimedTarget);
            bool slotMachine = target is SlotMachineReels;
            bool retire = target is RetireButton;
            bool manual = target is WorkshopManual && label.Title == "매뉴얼";
            if (m_view.HasDescription)
            {
                m_view.SetDescriptionVisible((slotMachine || !tutorialBlocked) && (slotMachine || retire || manual || !available));
                m_view.SetDescriptionColor(slotMachine ? Color.white : m_view.BodyColor);
            }

            string condition = label.UnavailableCondition;
            if (target is SlotMachineReels machine)
            {
                condition = machine.GetHoverDescription(PlayerWallet.ForPlayer(m_interaction.Player));
            }
            else if (retire)
            {
                PlayerWallet wallet = PlayerWallet.ForPlayer(m_interaction.Player);
                int cost = RetireUI.Cost;
                string amount = "$" + cost.ToString("N0", CultureInfo.InvariantCulture);
                string shownAmount = wallet == null || wallet.Balance < cost
                    ? $"<color=#FF0000>{amount}</color>" : amount;
                condition = condition.Replace("{RETIRE_COST}", shownAmount);
            }

            m_view.Show(label.Title, (tutorialBlocked && !slotMachine) || (!slotMachine && !retire && !manual && available) ? string.Empty : condition,
                retire || slotMachine, target is ModuleUnlockCrate crate && !crate.IsClaimed);
            m_view.SetInteractVisible(CanInteract());
            m_view.SetTitleColor(Color.white);
            return true;
        }

        private void ShowWeaponTags(EquipableWeapon _weapon)
        {
            if (m_tagSource == null || !m_view.HasTagLayout)
            {
                Hide();
                return;
            }

            var core = _weapon.GetComponentInChildren<PowerCore>();
            m_tagSource.GetMatchedTags(core, m_weaponTags);
            var preview = _weapon.GetComponent<PresetWeaponPreview>();
            m_view.ShowWeaponTags(m_weaponTags, _weapon.WeaponName);
            // 프리셋 창고는 전시 무기를 바라볼 때 바로 가져갈 수 있다. 작업대 긴 광선이 사거리 밖 전시물을 설명하는 경우는 안내하지 않는다.
            if (preview == null || (m_interaction != null && ReferenceEquals(m_interaction.AimedTarget, preview)))
            {
                m_view.SetInteractVisible(CanInteract());
            }
        }

        private void ShowCore(PowerCore _core)
        {
            CoreBaseStats data = _core.GetComponent<StatCollector>()?.BaseStats;
            WeaponStats stats = data != null ? data.Stats : _core.GetMuzzleStats(System.Array.Empty<PlacedBlock>());
            m_text.Clear();
            m_text.AppendLine(data != null && !string.IsNullOrWhiteSpace(data.Description)
                ? data.Description
                : "무기의 중심이 되는 코어입니다. 여섯 면으로 탄환을 내보냅니다.");
            m_text.AppendLine().AppendLine("코어 기본 스펙");
            foreach (StatType type in WeaponStats.AllTypes)
            {
                float value = stats.Get(type);
                if (Mathf.Approximately(value, 0f))
                {
                    continue;
                }

                m_text.Append("• ").Append(LABELS[(int)type]).Append(' ').Append(value.ToString("0.##")).AppendLine();
            }

            m_view.Show("메인 코어", m_text.ToString().TrimEnd());
            m_view.SetTitleColor(Color.white);
            m_view.SetDescriptionColor(m_view.BodyColor);
        }

        private void ShowModule(ModuleData _data)
        {
            m_view.Show(_data.DisplayName, string.IsNullOrWhiteSpace(_data.Description)
                ? "설명이 아직 등록되지 않은 모듈입니다."
                : _data.Description);
            m_view.SetTitleColor(_data.Color);
            m_view.SetDescriptionColor(m_view.BodyColor);
        }

        private void ShowMuzzle(PowerCore _core, PlacedBlock[] _path)
        {
            m_text.Clear();
            m_counts.Clear();
            foreach (PlacedBlock block in _path)
            {
                if (block == null)
                {
                    continue;
                }

                ModuleData data = block.GetComponent<ModuleStats>()?.Data;
                if (data == null && m_builder.Catalog != null && m_builder.Catalog.TryGet(block.ModuleSlot, out var entry))
                {
                    data = entry.data;
                }

                if (data != null)
                {
                    m_counts[data] = m_counts.TryGetValue(data, out int count) ? count + 1 : 1;
                }
            }

            m_text.AppendLine("적용 모듈");
            foreach (var pair in m_counts)
            {
                m_text.Append("• ").Append(pair.Key.DisplayName).Append(" × ").Append(pair.Value).AppendLine();
            }

            if (m_counts.Count == 0)
            {
                m_text.AppendLine("• 코어 기본값");
            }

            m_text.AppendLine().AppendLine("탄환 스펙");
            WeaponStats stats = _core.GetMuzzleStats(_path);
            foreach (StatType type in WeaponStats.AllTypes)
            {
                if (type == StatType.Weight || type == StatType.Scope || type == StatType.Color)
                {
                    continue;
                }

                float value = stats.Get(type);
                if (Mathf.Approximately(value, 0f))
                {
                    continue;
                }

                m_text.Append("• ").Append(LABELS[(int)type]).Append(' ').Append(value.ToString("0.##")).AppendLine();
            }

            m_view.Show("총구 정보", m_text.ToString().TrimEnd());
            m_view.SetTitleColor(Color.white);
            m_view.SetDescriptionColor(Color.white);
        }

        private bool CanInteract()
        {
            return m_interaction != null && m_interaction.isActiveAndEnabled &&
                !GameManager.GameplayBlocked && m_interaction.CanInteractTarget;
        }
    }
}
