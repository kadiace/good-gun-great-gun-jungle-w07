using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using TMPro;

namespace WeaponWorkshop
{
    /// <summary>
    /// I 키로 열고 닫는 생존 모듈 가방 UI 컨트롤러입니다. 무게 표시, 아이템 그리드, 상세 정보 패널, 실물 버리기 기능을 제공합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurvivalInventoryUI : MonoBehaviour
    {
        public static SurvivalInventoryUI Instance { get; private set; }
        public static bool IsOpen { get; private set; }
        private static int _closedFrame = -1;
        public static bool ClosedThisFrame => _closedFrame == Time.frameCount;

        [Header("References")]
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private PlayerInventory _inventory;
        [SerializeField] private WorkshopSession _workshopSession;
        [SerializeField] private GameObject _panelRoot;

        [Header("UI Elements")]
        [SerializeField] private TMP_Text _weightText;
        [SerializeField] private Transform _gridContent;
        [SerializeField] private TMP_Text _detailTitle;
        [SerializeField] private TMP_Text _detailDescription;
        [SerializeField] private TMP_Text _detailWeight;
        [SerializeField] private TMP_Text _dropCountText;
        [SerializeField] private Button _dropMinusButton;
        [SerializeField] private Button _dropPlusButton;
        [SerializeField] private Button _dropConfirmButton;

        private InputActionMap _playerControls;
        private InputActionMap _uiControls;
        private InputAction _cancelAction;
        private InputAction _toggleInventoryAction;
        private int _selectedModuleId = -1;
        private int _selectedDropCount = 1;
        private readonly List<KeyValuePair<int, int>> _cachedItemList = new List<KeyValuePair<int, int>>();
        private readonly List<GameObject> _spawnedGridItems = new List<GameObject>();

        [Header("폰트 설정")]
        [SerializeField] private TMP_FontAsset _koreanFont;

        private TMP_FontAsset _cachedKoreanFont;

        /// <summary>
        /// 프로젝트의 실제 한글 폰트 에셋(WorkshopManual SDF 등)을 탐색하여 반환합니다.
        /// </summary>
        private TMP_FontAsset GetPreferredFont()
        {
            if (_koreanFont != null) return _koreanFont;
            if (_cachedKoreanFont != null) return _cachedKoreanFont;

            var allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (int i = 0; i < allFonts.Length; i++)
            {
                if (allFonts[i] != null && allFonts[i].name.Contains("WorkshopManual"))
                {
                    _cachedKoreanFont = allFonts[i];
                    return _cachedKoreanFont;
                }
            }

            var loadedFont = Resources.Load<TMP_FontAsset>("WorkshopManual SDF");
            if (loadedFont != null)
            {
                _cachedKoreanFont = loadedFont;
                return _cachedKoreanFont;
            }

            var allTexts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allTexts.Length; i++)
            {
                if (allTexts[i] != null && allTexts[i].font != null && allTexts[i].font.name.Contains("WorkshopManual"))
                {
                    _cachedKoreanFont = allTexts[i].font;
                    return _cachedKoreanFont;
                }
            }

            if (allTexts.Length > 0 && allTexts[0].font != null)
            {
                _cachedKoreanFont = allTexts[0].font;
                return _cachedKoreanFont;
            }

            return null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            IsOpen = false;
            _closedFrame = -1;
            Instance = null;
        }

        void Awake()
        {
            Instance = this;
            IsOpen = false;
            if (_inventory == null) _inventory = GetComponentInParent<PlayerInventory>() ?? FindFirstObjectByType<PlayerInventory>();
            if (_workshopSession == null) _workshopSession = GetComponentInParent<WorkshopSession>() ?? FindFirstObjectByType<WorkshopSession>();

            if (_panelRoot == null)
            {
                BuildRuntimeUI();
            }

            if (_panelRoot != null) _panelRoot.SetActive(false);

            if (_dropMinusButton != null) _dropMinusButton.onClick.AddListener(OnDropMinusClicked);
            if (_dropPlusButton != null) _dropPlusButton.onClick.AddListener(OnDropPlusClicked);
            if (_dropConfirmButton != null) _dropConfirmButton.onClick.AddListener(OnDropConfirmClicked);

            SetupInput();

            var font = GetPreferredFont();
            if (font != null)
            {
                if (_weightText != null && (_weightText.font == null || _weightText.font.name == "LiberationSans SDF")) _weightText.font = font;
                if (_detailTitle != null && (_detailTitle.font == null || _detailTitle.font.name == "LiberationSans SDF")) _detailTitle.font = font;
                if (_detailDescription != null && (_detailDescription.font == null || _detailDescription.font.name == "LiberationSans SDF")) _detailDescription.font = font;
                if (_dropCountText != null && (_dropCountText.font == null || _dropCountText.font.name == "LiberationSans SDF")) _dropCountText.font = font;
            }
        }

        void OnEnable()
        {
            _playerControls?.Enable();
            _uiControls?.Enable();
            if (_inventory != null)
            {
                _inventory.InventoryChanged += OnInventoryChanged;
            }
        }

        void OnDisable()
        {
            _playerControls?.Disable();
            _uiControls?.Disable();
            if (_inventory != null)
            {
                _inventory.InventoryChanged -= OnInventoryChanged;
            }
            if (IsOpen) Close();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_toggleInventoryAction != null)
            {
                _toggleInventoryAction.performed -= OnToggleInventoryPerformed;
            }
            if (_cancelAction != null)
            {
                _cancelAction.performed -= OnCancelPerformed;
            }
            _playerControls?.Dispose();
            _uiControls?.Dispose();
            IsOpen = false;
        }

        /// <summary>
        /// 인벤토리 토글(ToggleInventory) 및 닫기(Cancel)용 Input System 액션을 바인딩하고 리스너를 설정합니다.
        /// </summary>
        private void SetupInput()
        {
            if (_inputActions == null)
            {
                var player = GetComponentInParent<FirstPersonPlayer>() ?? FindFirstObjectByType<FirstPersonPlayer>();
                if (player != null && player.InputActions != null)
                {
                    _inputActions = player.InputActions;
                }
            }

            if (_inputActions == null)
            {
                _inputActions = Resources.Load<InputActionAsset>("InputSystem_Actions");
#if UNITY_EDITOR
                if (_inputActions == null)
                {
                    _inputActions = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
                }
#endif
            }

            if (_inputActions != null)
            {
                var playerMap = _inputActions.FindActionMap("Player", false);
                if (playerMap != null)
                {
                    _playerControls = playerMap.Clone();
                    _toggleInventoryAction = _playerControls.FindAction("ToggleInventory", false);
                    if (_toggleInventoryAction != null)
                    {
                        _toggleInventoryAction.performed += OnToggleInventoryPerformed;
                    }
                }

                var uiMap = _inputActions.FindActionMap("UI", false);
                if (uiMap != null)
                {
                    _uiControls = uiMap.Clone();
                    _cancelAction = _uiControls.FindAction("Cancel", false);
                    if (_cancelAction != null)
                    {
                        _cancelAction.performed += OnCancelPerformed;
                    }
                }
            }
        }

        private void OnToggleInventoryPerformed(InputAction.CallbackContext context)
        {
            Toggle();
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            if (IsOpen)
            {
                Close();
            }
        }

        /// <summary>
        /// 가방 UI를 열거나 닫습니다. 작업대 편집 중에는 가방 열기가 차단됩니다.
        /// </summary>
        public void Toggle()
        {
            if (!SurvivalSession.IsActive) return;

            if (IsOpen)
            {
                Close();
            }
            else
            {
                if (_workshopSession != null && _workshopSession.IsInWorkshop) return;
                Open();
            }
        }

        /// <summary>
        /// 가방 UI를 활성화하고 플레이어 조작을 차단하며 커서를 해제합니다. 세계 시간은 멈추지 않습니다.
        /// </summary>
        public void Open()
        {
            if (_workshopSession != null && _workshopSession.IsInWorkshop) return;
            if (_inventory == null) _inventory = FindFirstObjectByType<PlayerInventory>();

            IsOpen = true;
            if (_panelRoot != null) _panelRoot.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            RefreshUI();
        }

        /// <summary>
        /// 가방 UI를 비활성화하고 마우스 커서와 플레이어 조작을 복원합니다.
        /// </summary>
        public void Close()
        {
            _closedFrame = Time.frameCount;
            IsOpen = false;
            if (_panelRoot != null) _panelRoot.SetActive(false);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// 인벤토리 아이템 또는 무게가 변경되었을 때 화면을 갱신합니다.
        /// </summary>
        private void OnInventoryChanged()
        {
            if (IsOpen)
            {
                RefreshUI();
            }
        }

        /// <summary>
        /// 가방의 무게 정보, 아이템 그리드 및 상세 정보 화면을 최신 상태로 갱신합니다.
        /// </summary>
        public void RefreshUI()
        {
            if (_inventory == null) _inventory = FindFirstObjectByType<PlayerInventory>();
            if (_inventory == null) return;

            // 1. 무게 텍스트 갱신
            float currentWeight = _inventory.CurrentCarryWeightKg;
            var table = TableManager.Instance.SurvivalLootWeight;
            float maxCapacity = table != null ? table.baseCarryCapacityKg : 30f;

            if (_weightText != null)
            {
                _weightText.text = $"{currentWeight:0.0} / {maxCapacity:0.0} KG";
                if (currentWeight >= 60f)
                {
                    _weightText.color = new Color(1f, 0.2f, 0.2f);
                }
                else if (currentWeight >= 40f)
                {
                    _weightText.color = new Color(1f, 0.6f, 0.2f);
                }
                else if (currentWeight > 30f)
                {
                    _weightText.color = new Color(1f, 0.9f, 0.3f);
                }
                else
                {
                    _weightText.color = Color.white;
                }
            }

            // 2. 아이템 그리드 갱신
            _inventory.GetAllItems(_cachedItemList);
            UpdateGridItems();

            // 3. 선택 블록 검증 및 상세 정보 갱신
            if (_selectedModuleId > 0 && _inventory.GetCount(_selectedModuleId) <= 0)
            {
                _selectedModuleId = -1;
            }

            if (_selectedModuleId <= 0 && _cachedItemList.Count > 0)
            {
                _selectedModuleId = _cachedItemList[0].Key;
            }

            RefreshDetailPanel();
        }

        /// <summary>
        /// 보유한 블록 아이템들을 그리드 뷰에 동적으로 생성하여 나열합니다.
        /// </summary>
        private void UpdateGridItems()
        {
            for (int i = 0; i < _spawnedGridItems.Count; i++)
            {
                Destroy(_spawnedGridItems[i]);
            }
            _spawnedGridItems.Clear();

            if (_gridContent == null) return;

            var catalog = Resources.Load<ModuleCatalog>("ModuleCatalog");
            for (int i = 0; i < _cachedItemList.Count; i++)
            {
                int moduleId = _cachedItemList[i].Key;
                int count = _cachedItemList[i].Value;
                float singleWeight = SurvivalLootWeightInfo.GetWeightKg(moduleId);
                float totalWeight = singleWeight * count;

                string name = $"모듈 {moduleId}";
                Color color = Color.white;
                if (catalog != null && catalog.TryGet(moduleId, out var entry))
                {
                    name = entry.displayName;
                    color = entry.color;
                }

                var itemObj = new GameObject($"Slot_{moduleId}", typeof(RectTransform), typeof(Image), typeof(Button));
                itemObj.transform.SetParent(_gridContent, false);
                _spawnedGridItems.Add(itemObj);

                var img = itemObj.GetComponent<Image>();
                img.color = moduleId == _selectedModuleId ? new Color(0.2f, 0.5f, 0.8f, 0.9f) : new Color(0.15f, 0.15f, 0.18f, 0.85f);

                var btn = itemObj.GetComponent<Button>();
                int capturedId = moduleId;
                btn.onClick.AddListener(() =>
                {
                    _selectedModuleId = capturedId;
                    _selectedDropCount = 1;
                    RefreshUI();
                });

                var textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObj.transform.SetParent(itemObj.transform, false);
                var textRect = textObj.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(4, 4);
                textRect.offsetMax = new Vector2(-4, -4);

                var tmp = textObj.GetComponent<TextMeshProUGUI>();
                var itemFont = GetPreferredFont();
                if (itemFont != null) tmp.font = itemFont;
                tmp.fontSize = 16f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.text = $"{name}\n<b>×{count}</b>\n<size=13>{totalWeight:0.0}kg</size>";
                tmp.color = color;
            }
        }

        /// <summary>
        /// 현재 선택된 블록의 상세 설명과 버리기 수량 조절기를 갱신합니다.
        /// </summary>
        private void RefreshDetailPanel()
        {
            if (_selectedModuleId <= 0 || _inventory == null || _inventory.GetCount(_selectedModuleId) <= 0)
            {
                if (_detailTitle != null) _detailTitle.text = "선택된 블록 없음";
                if (_detailDescription != null) _detailDescription.text = "가방에서 블록을 선택해 주세요.";
                if (_detailWeight != null) _detailWeight.text = "";
                if (_dropCountText != null) _dropCountText.text = "0";
                if (_dropConfirmButton != null) _dropConfirmButton.interactable = false;
                return;
            }

            int currentCount = _inventory.GetCount(_selectedModuleId);
            _selectedDropCount = Mathf.Clamp(_selectedDropCount, 1, currentCount);

            string name = $"모듈 {_selectedModuleId}";
            string desc = "";
            var catalog = Resources.Load<ModuleCatalog>("ModuleCatalog");
            if (catalog != null && catalog.TryGet(_selectedModuleId, out var entry) && entry.data != null)
            {
                name = entry.displayName;
                desc = entry.data.Description;
            }

            float weight = SurvivalLootWeightInfo.GetWeightKg(_selectedModuleId);

            if (_detailTitle != null) _detailTitle.text = name;
            if (_detailDescription != null) _detailDescription.text = desc;
            if (_detailWeight != null) _detailWeight.text = $"개당 무게: {weight:0.0}kg";
            if (_dropCountText != null) _dropCountText.text = _selectedDropCount.ToString();
            if (_dropConfirmButton != null) _dropConfirmButton.interactable = true;
        }

        /// <summary>
        /// 버리기 수량 감소 버튼 클릭 시 수량을 1 감소시킵니다.
        /// </summary>
        private void OnDropMinusClicked()
        {
            if (_selectedDropCount > 1)
            {
                _selectedDropCount--;
                if (_dropCountText != null) _dropCountText.text = _selectedDropCount.ToString();
            }
        }

        /// <summary>
        /// 버리기 수량 증가 버튼 클릭 시 수량을 1 증가시킵니다.
        /// </summary>
        private void OnDropPlusClicked()
        {
            if (_inventory == null || _selectedModuleId <= 0) return;
            int max = _inventory.GetCount(_selectedModuleId);
            if (_selectedDropCount < max)
            {
                _selectedDropCount++;
                if (_dropCountText != null) _dropCountText.text = _selectedDropCount.ToString();
            }
        }

        /// <summary>
        /// 버리기 확인 버튼 클릭 시 지정한 수량의 실물 블록을 플레이어 발밑 월드에 생성합니다.
        /// </summary>
        private void OnDropConfirmClicked()
        {
            if (_inventory == null || _selectedModuleId <= 0 || _selectedDropCount <= 0) return;

            _inventory.TryDropToWorld(_selectedModuleId, _selectedDropCount);
            _selectedDropCount = 1;
            RefreshUI();
        }

        /// <summary>
        /// 인스펙터 참조가 비어 있을 경우 런타임에 기본 Canvas와 UI 계층을 프로그래매틱하게 생성하여 구성합니다.
        /// </summary>
        private void BuildRuntimeUI()
        {
            var canvasObj = new GameObject("SurvivalInventoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObj.transform.SetParent(transform, false);

            var canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _panelRoot = new GameObject("InventoryPanel", typeof(RectTransform), typeof(Image));
            _panelRoot.transform.SetParent(canvasObj.transform, false);
            var panelRect = _panelRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(900, 600);

            var panelImg = _panelRoot.GetComponent<Image>();
            panelImg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

            // 상단 제목 및 무게
            var headerObj = new GameObject("Header", typeof(RectTransform));
            headerObj.transform.SetParent(_panelRoot.transform, false);
            var headerRect = headerObj.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, 60);

            var titleTextObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleTextObj.transform.SetParent(headerObj.transform, false);
            var titleRect = titleTextObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0);
            titleRect.anchorMax = new Vector2(0.5f, 1);
            titleRect.offsetMin = new Vector2(20, 0);
            var defaultFont = GetPreferredFont();
            var titleTMP = titleTextObj.GetComponent<TextMeshProUGUI>();
            if (defaultFont != null) titleTMP.font = defaultFont;
            titleTMP.fontSize = 24;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.text = "생존 가방 (INVENTORY)";
            titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
            titleTMP.enableWordWrapping = false;

            var weightTextObj = new GameObject("WeightText", typeof(RectTransform), typeof(TextMeshProUGUI));
            weightTextObj.transform.SetParent(headerObj.transform, false);
            var weightRect = weightTextObj.GetComponent<RectTransform>();
            weightRect.anchorMin = new Vector2(0.5f, 0);
            weightRect.anchorMax = new Vector2(1, 1);
            weightRect.offsetMax = new Vector2(-20, 0);
            _weightText = weightTextObj.GetComponent<TextMeshProUGUI>();
            if (defaultFont != null) _weightText.font = defaultFont;
            _weightText.fontSize = 22;
            _weightText.alignment = TextAlignmentOptions.MidlineRight;
            _weightText.text = "0.0 / 30.0 KG";
            _weightText.enableWordWrapping = false;

            // 좌측 아이템 그리드 영역
            var gridScrollObj = new GameObject("GridScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            gridScrollObj.transform.SetParent(_panelRoot.transform, false);
            var gridScrollRect = gridScrollObj.GetComponent<RectTransform>();
            gridScrollRect.anchorMin = new Vector2(0, 0);
            gridScrollRect.anchorMax = new Vector2(0.6f, 1);
            gridScrollRect.offsetMin = new Vector2(20, 20);
            gridScrollRect.offsetMax = new Vector2(-10, -70);
            gridScrollObj.GetComponent<Image>().color = new Color(0.04f, 0.04f, 0.06f, 0.5f);

            var gridContentObj = new GameObject("GridContent", typeof(RectTransform), typeof(GridLayoutGroup));
            gridContentObj.transform.SetParent(gridScrollObj.transform, false);
            var gridContentRect = gridContentObj.GetComponent<RectTransform>();
            gridContentRect.anchorMin = new Vector2(0, 1);
            gridContentRect.anchorMax = new Vector2(1, 1);
            gridContentRect.pivot = new Vector2(0, 1);
            gridContentRect.sizeDelta = new Vector2(0, 500);

            var glg = gridContentObj.GetComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(110, 100);
            glg.spacing = new Vector2(10, 10);
            glg.padding = new RectOffset(10, 10, 10, 10);
            _gridContent = gridContentObj.transform;

            var scroll = gridScrollObj.GetComponent<ScrollRect>();
            scroll.content = gridContentRect;
            scroll.horizontal = false;
            scroll.vertical = true;

            // 우측 상세 정보 패널
            var detailObj = new GameObject("DetailPanel", typeof(RectTransform), typeof(Image));
            detailObj.transform.SetParent(_panelRoot.transform, false);
            var detailRect = detailObj.GetComponent<RectTransform>();
            detailRect.anchorMin = new Vector2(0.6f, 0);
            detailRect.anchorMax = new Vector2(1, 1);
            detailRect.offsetMin = new Vector2(10, 20);
            detailRect.offsetMax = new Vector2(-20, -70);
            detailObj.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.8f);

            var dTitleObj = new GameObject("DetailTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            dTitleObj.transform.SetParent(detailObj.transform, false);
            var dtRect = dTitleObj.GetComponent<RectTransform>();
            dtRect.anchorMin = new Vector2(0, 1);
            dtRect.anchorMax = new Vector2(1, 1);
            dtRect.pivot = new Vector2(0.5f, 1);
            dtRect.sizeDelta = new Vector2(-30, 40);
            dtRect.anchoredPosition = new Vector2(0, -15);
            _detailTitle = dTitleObj.GetComponent<TextMeshProUGUI>();
            if (defaultFont != null) _detailTitle.font = defaultFont;
            _detailTitle.fontSize = 20;
            _detailTitle.fontStyle = FontStyles.Bold;
            _detailTitle.alignment = TextAlignmentOptions.Center;
            _detailTitle.enableWordWrapping = false;

            var dWeightObj = new GameObject("DetailWeight", typeof(RectTransform), typeof(TextMeshProUGUI));
            dWeightObj.transform.SetParent(detailObj.transform, false);
            var dwRect = dWeightObj.GetComponent<RectTransform>();
            dwRect.anchorMin = new Vector2(0, 1);
            dwRect.anchorMax = new Vector2(1, 1);
            dwRect.pivot = new Vector2(0.5f, 1);
            dwRect.sizeDelta = new Vector2(-30, 30);
            dwRect.anchoredPosition = new Vector2(0, -60);
            _detailWeight = dWeightObj.GetComponent<TextMeshProUGUI>();
            if (defaultFont != null) _detailWeight.font = defaultFont;
            _detailWeight.fontSize = 16;
            _detailWeight.alignment = TextAlignmentOptions.Center;
            _detailWeight.color = new Color(0.7f, 0.8f, 1f);
            _detailWeight.enableWordWrapping = false;

            var dDescObj = new GameObject("DetailDesc", typeof(RectTransform), typeof(TextMeshProUGUI));
            dDescObj.transform.SetParent(detailObj.transform, false);
            var ddRect = dDescObj.GetComponent<RectTransform>();
            ddRect.anchorMin = new Vector2(0, 0.35f);
            ddRect.anchorMax = new Vector2(1, 1);
            ddRect.offsetMin = new Vector2(20, 0);
            ddRect.offsetMax = new Vector2(-20, -100);
            _detailDescription = dDescObj.GetComponent<TextMeshProUGUI>();
            if (defaultFont != null) _detailDescription.font = defaultFont;
            _detailDescription.fontSize = 15;
            _detailDescription.alignment = TextAlignmentOptions.TopLeft;
            _detailDescription.enableWordWrapping = true;

            // 하단 버리기 조작 UI
            var dropGroupObj = new GameObject("DropGroup", typeof(RectTransform));
            dropGroupObj.transform.SetParent(detailObj.transform, false);
            var dgRect = dropGroupObj.GetComponent<RectTransform>();
            dgRect.anchorMin = new Vector2(0, 0);
            dgRect.anchorMax = new Vector2(1, 0.35f);
            dgRect.offsetMin = new Vector2(15, 15);
            dgRect.offsetMax = new Vector2(-15, 0);

            // - 버튼
            var minusBtnObj = new GameObject("MinusBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            minusBtnObj.transform.SetParent(dropGroupObj.transform, false);
            var mbRect = minusBtnObj.GetComponent<RectTransform>();
            mbRect.anchorMin = new Vector2(0.1f, 0.55f);
            mbRect.anchorMax = new Vector2(0.3f, 0.95f);
            minusBtnObj.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f);
            _dropMinusButton = minusBtnObj.GetComponent<Button>();

            var minusText = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            minusText.transform.SetParent(minusBtnObj.transform, false);
            var mtRect = minusText.GetComponent<RectTransform>();
            mtRect.anchorMin = Vector2.zero;
            mtRect.anchorMax = Vector2.one;
            mtRect.offsetMin = Vector2.zero;
            mtRect.offsetMax = Vector2.zero;
            var minusTMP = minusText.GetComponent<TextMeshProUGUI>();
            if (defaultFont != null) minusTMP.font = defaultFont;
            minusTMP.text = "-";
            minusTMP.alignment = TextAlignmentOptions.Center;
            minusTMP.fontSize = 24;
            minusTMP.enableWordWrapping = false;
            var countObj = new GameObject("CountText", typeof(RectTransform), typeof(TextMeshProUGUI));
            countObj.transform.SetParent(dropGroupObj.transform, false);
            var cRect = countObj.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0.35f, 0.55f);
            cRect.anchorMax = new Vector2(0.65f, 0.95f);
            _dropCountText = countObj.GetComponent<TextMeshProUGUI>();
            if (defaultFont != null) _dropCountText.font = defaultFont;
            _dropCountText.fontSize = 22;
            _dropCountText.alignment = TextAlignmentOptions.Center;
            _dropCountText.text = "1";
            _dropCountText.enableWordWrapping = false;

            // + 버튼
            var plusBtnObj = new GameObject("PlusBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            plusBtnObj.transform.SetParent(dropGroupObj.transform, false);
            var pbRect = plusBtnObj.GetComponent<RectTransform>();
            pbRect.anchorMin = new Vector2(0.7f, 0.55f);
            pbRect.anchorMax = new Vector2(0.9f, 0.95f);
            plusBtnObj.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f);
            _dropPlusButton = plusBtnObj.GetComponent<Button>();

            var plusText = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            plusText.transform.SetParent(plusBtnObj.transform, false);
            var ptRect = plusText.GetComponent<RectTransform>();
            ptRect.anchorMin = Vector2.zero;
            ptRect.anchorMax = Vector2.one;
            ptRect.offsetMin = Vector2.zero;
            ptRect.offsetMax = Vector2.zero;
            var plusTMP = plusText.GetComponent<TextMeshProUGUI>();
            if (defaultFont != null) plusTMP.font = defaultFont;
            plusTMP.text = "+";
            plusTMP.alignment = TextAlignmentOptions.Center;
            plusTMP.fontSize = 24;
            plusTMP.enableWordWrapping = false;
            var confirmBtnObj = new GameObject("ConfirmDropBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            confirmBtnObj.transform.SetParent(dropGroupObj.transform, false);
            var cbRect = confirmBtnObj.GetComponent<RectTransform>();
            cbRect.anchorMin = new Vector2(0.1f, 0.05f);
            cbRect.anchorMax = new Vector2(0.9f, 0.45f);
            confirmBtnObj.GetComponent<Image>().color = new Color(0.75f, 0.25f, 0.25f);
            _dropConfirmButton = confirmBtnObj.GetComponent<Button>();

            var confirmText = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            confirmText.transform.SetParent(confirmBtnObj.transform, false);
            var ctRect = confirmText.GetComponent<RectTransform>();
            ctRect.anchorMin = Vector2.zero;
            ctRect.anchorMax = Vector2.one;
            ctRect.offsetMin = new Vector2(6, 0);
            ctRect.offsetMax = new Vector2(-6, 0);
            var confirmTMP = confirmText.GetComponent<TextMeshProUGUI>();
            if (defaultFont != null) confirmTMP.font = defaultFont;
            confirmTMP.text = "발밑에 버리기 (Drop)";
            confirmTMP.alignment = TextAlignmentOptions.Center;
            confirmTMP.fontSize = 18;
            confirmTMP.enableWordWrapping = false;
        }
    }
}
