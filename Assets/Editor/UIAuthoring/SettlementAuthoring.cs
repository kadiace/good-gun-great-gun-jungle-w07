using UnityEditor;

using UnityEngine;
using UnityEngine.UI;

using WeaponWorkshop;

// 영업 정산 UI(UIManager.prefab)의 저작 명령. 기존 런타임 생성 경로(DaySettlementUI.CreateSlotTicketDisplay·
// CreateDailyRentText, MoneyRollDisplay 마스크·숫자)를 프리팹 오브젝트로 옮기고 SettlementView 참조를 연결한다.
// 멱등: 이미 저작돼 있으면 새로 만들지 않고 연결만 검증·보정한다. 기존 오브젝트를 삭제·재생성하지 않는다.
public static class SettlementAuthoring
{
    private const string PREFAB_PATH = "Assets/Prefabs/Managers/UIManager.prefab";
    private const string SLOT_TICKET_NAME = "SlotTicketDisplay";
    private const string DAILY_RENT_NAME = "DailyRentDisplay";
    private const string DIGITS_ROOT_NAME = "MoneyDigits";

    [MenuItem("Tools/UI/Authoring/Settlement/Author")]
    public static void Author()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH) == null)
        {
            Debug.LogError($"[UIAuthoring] FAIL Settlement/Author: UIManager 프리팹을 찾을 수 없습니다. {PREFAB_PATH}");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
        try
        {
            string summary = AuthorContents(root);
            if (PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH) == null)
            {
                Debug.LogError("[UIAuthoring] FAIL Settlement/Author: 프리팹 저장 실패 " + PREFAB_PATH);
                return;
            }

            Debug.Log($"[UIAuthoring] OK Settlement/Author: {summary}");
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[UIAuthoring] FAIL Settlement/Author: {exception.Message}\n{exception.StackTrace}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string AuthorContents(GameObject _root)
    {
        DaySettlementUI settlement = _root.GetComponentInChildren<DaySettlementUI>(true);
        if (settlement == null)
        {
            Fail("UIManager 프리팹에 DaySettlementUI 컴포넌트가 없습니다.");
        }

        SerializedObject settlementSo = new SerializedObject(settlement);
        GameObject settlementPanel = ReadObject(settlementSo, "settlementPanel") as GameObject;
        TMPro.TextMeshProUGUI summaryText = ReadObject(settlementSo, "summaryText") as TMPro.TextMeshProUGUI;
        TMPro.TextMeshProUGUI receiptTitle = ReadObject(settlementSo, "receiptTitle") as TMPro.TextMeshProUGUI;
        TMPro.TextMeshProUGUI todayIncomeText = ReadObject(settlementSo, "todayIncomeText") as TMPro.TextMeshProUGUI;
        RectTransform moneyDisplay = ReadObject(settlementSo, "moneyDisplay") as RectTransform;
        TMPro.TextMeshProUGUI moneyText = ReadObject(settlementSo, "moneyText") as TMPro.TextMeshProUGUI;
        RectTransform moduleCreditDisplay = ReadObject(settlementSo, "moduleCreditDisplay") as RectTransform;
        TMPro.TextMeshProUGUI moduleCreditText = ReadObject(settlementSo, "moduleCreditText") as TMPro.TextMeshProUGUI;
        Button declineButton = ReadObject(settlementSo, "declineButton") as Button;
        Button nextDayButton = ReadObject(settlementSo, "nextDayButton") as Button;
        float dailyRentGap = settlementSo.FindProperty("dailyRentGap").floatValue;
        float dailyRentWidth = settlementSo.FindProperty("dailyRentWidth").floatValue;

        Require(settlementPanel, "settlementPanel");
        Require(summaryText, "summaryText");
        Require(receiptTitle, "receiptTitle");
        Require(todayIncomeText, "todayIncomeText");
        Require(moneyDisplay, "moneyDisplay");
        Require(moneyText, "moneyText");
        Require(moduleCreditDisplay, "moduleCreditDisplay");
        Require(moduleCreditText, "moduleCreditText");
        Require(declineButton, "declineButton");

        // 1. 슬롯 티켓 행: ModuleCreditDisplay 복제(기존 CreateSlotTicketDisplay와 같은 이름·위치·아이콘 색).
        Transform currencyGroup = moduleCreditDisplay.parent;
        bool createdSlotTicket = false;
        Transform slotTicket = FindChildByName(currencyGroup, SLOT_TICKET_NAME);
        if (slotTicket == null)
        {
            GameObject clone = Object.Instantiate(moduleCreditDisplay.gameObject, currencyGroup);
            clone.name = SLOT_TICKET_NAME;
            RectTransform displayRect = clone.GetComponent<RectTransform>();
            displayRect.anchoredPosition = moduleCreditDisplay.anchoredPosition -
                new Vector2(0f, moduleCreditDisplay.rect.height + 4.5f);
            Image icon = clone.transform.Find("ModuleIcon") != null
                ? clone.transform.Find("ModuleIcon").GetComponent<Image>() : null;
            if (icon != null)
            {
                icon.color = new Color(0.65f, 0.2f, 1f);
            }

            clone.SetActive(false);
            slotTicket = clone.transform;
            createdSlotTicket = true;
        }

        TMPro.TextMeshProUGUI slotTicketText = slotTicket.Find("ModuleCreditText") != null
            ? slotTicket.Find("ModuleCreditText").GetComponent<TMPro.TextMeshProUGUI>() : null;
        Require(slotTicketText, SLOT_TICKET_NAME + "/ModuleCreditText");

        // 2. 자리세 행: MoneyText 오른쪽에 붙는 TMP(기존 CreateDailyRentText와 같은 앵커·크기·스타일).
        bool createdDailyRent = false;
        Transform dailyRent = FindChildByName(moneyDisplay, DAILY_RENT_NAME);
        if (dailyRent == null)
        {
            GameObject textObject = new GameObject(DAILY_RENT_NAME,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(moneyDisplay, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(moneyDisplay.sizeDelta.x + dailyRentGap, 0f);
            rect.sizeDelta = new Vector2(dailyRentWidth, moneyDisplay.sizeDelta.y * 3f);

            TMPro.TextMeshProUGUI text = textObject.GetComponent<TMPro.TextMeshProUGUI>();
            text.font = moneyText.font;
            text.fontSize = moneyText.fontSize;
            text.fontWeight = moneyText.fontWeight;
            text.fontStyle = moneyText.fontStyle;
            text.color = moneyText.color;
            text.alignment = TMPro.TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            text.overflowMode = TMPro.TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = "자리세: $ 0";
            textObject.SetActive(false);
            dailyRent = textObject.transform;
            createdDailyRent = true;
        }

        TMPro.TextMeshProUGUI dailyRentText = dailyRent.GetComponent<TMPro.TextMeshProUGUI>();
        Require(dailyRentText, DAILY_RENT_NAME);

        // 3. MoneyRollDisplay 마스크·숫자 템플릿(돈·모듈 재화·슬롯 티켓 세 자리).
        MoneyRollDisplay moneyRoll = EnsureRollAssets(moneyText);
        MoneyRollDisplay moduleCreditRoll = EnsureRollAssets(moduleCreditText);
        MoneyRollDisplay slotTicketRoll = EnsureRollAssets(slotTicketText);

        // 4. SettlementView 컴포넌트 추가·참조 연결, 브리지의 m_view 연결.
        bool createdView = false;
        SettlementView view = settlement.GetComponent<SettlementView>();
        if (view == null)
        {
            view = settlement.gameObject.AddComponent<SettlementView>();
            createdView = true;
        }

        SerializedObject viewSo = new SerializedObject(view);
        viewSo.FindProperty("m_slotTicketDisplay").objectReferenceValue = slotTicket.gameObject;
        viewSo.FindProperty("m_slotTicketText").objectReferenceValue = slotTicketText;
        viewSo.FindProperty("m_dailyRentText").objectReferenceValue = dailyRentText;
        viewSo.FindProperty("m_moneyRoll").objectReferenceValue = moneyRoll;
        viewSo.FindProperty("m_moduleCreditRoll").objectReferenceValue = moduleCreditRoll;
        viewSo.FindProperty("m_slotTicketRoll").objectReferenceValue = slotTicketRoll;
        viewSo.ApplyModifiedPropertiesWithoutUndo();

        settlementSo.Update();
        settlementSo.FindProperty("m_view").objectReferenceValue = view;
        settlementSo.ApplyModifiedPropertiesWithoutUndo();

        // 5. 버튼 명시 Navigation(비순환). 다음 날 버튼이 연결돼 있으면 좌우 이웃으로만 맞춘다.
        ApplyExplicitNavigation(declineButton, nextDayButton);
        if (nextDayButton != null)
        {
            ApplyExplicitNavigation(nextDayButton, declineButton);
        }

        bool createdAny = createdSlotTicket || createdDailyRent || createdView;
        return $"{(createdAny ? "생성 포함" : "기존 저작물 검증")}: " +
            $"SlotTicketDisplay{(createdSlotTicket ? "(신규)" : "(기존)")} " +
            $"DailyRentDisplay{(createdDailyRent ? "(신규)" : "(기존)")} " +
            $"SettlementView{(createdView ? "(신규)" : "(기존)")} 연결, " +
            $"MoneyDigits 3곳(money/moduleCredit/slotTicket) 마스크·템플릿·MoneyRollDisplay 연결, " +
            $"ConfirmButton Explicit Navigation, nextDayButton={(nextDayButton != null ? "연결됨" : "미연결(null 유지)")}";
    }

    private static MoneyRollDisplay EnsureRollAssets(TMPro.TextMeshProUGUI _sourceText)
    {
        MoneyRollDisplay roll = _sourceText.GetComponent<MoneyRollDisplay>();
        if (roll == null)
        {
            roll = _sourceText.gameObject.AddComponent<MoneyRollDisplay>();
        }

        RectTransform digitsRoot = FindChildByName(_sourceText.transform, DIGITS_ROOT_NAME) as RectTransform;
        if (digitsRoot == null)
        {
            GameObject digitsObject = new GameObject(DIGITS_ROOT_NAME, typeof(RectTransform), typeof(RectMask2D));
            digitsRoot = digitsObject.GetComponent<RectTransform>();
            digitsRoot.SetParent(_sourceText.transform, false);
            digitsRoot.anchorMin = Vector2.zero;
            digitsRoot.anchorMax = Vector2.one;
            digitsRoot.offsetMin = Vector2.zero;
            digitsRoot.offsetMax = Vector2.zero;
            digitsObject.SetActive(false);
        }

        EnsureDigitTemplate(digitsRoot, "DigitTemplate", _sourceText);
        EnsureDigitTemplate(digitsRoot, "NextDigitTemplate", _sourceText);
        EnsureSeparatorTemplate(digitsRoot, _sourceText);

        SerializedObject rollSo = new SerializedObject(roll);
        rollSo.FindProperty("m_digitRoot").objectReferenceValue = digitsRoot;
        rollSo.FindProperty("m_digitTemplate").objectReferenceValue =
            FindChildByName(digitsRoot, "DigitTemplate").GetComponent<TMPro.TextMeshProUGUI>();
        rollSo.FindProperty("m_nextDigitTemplate").objectReferenceValue =
            FindChildByName(digitsRoot, "NextDigitTemplate").GetComponent<TMPro.TextMeshProUGUI>();
        rollSo.FindProperty("m_separatorTemplate").objectReferenceValue =
            FindChildByName(digitsRoot, "SeparatorTemplate").GetComponent<TMPro.TextMeshProUGUI>();
        rollSo.ApplyModifiedPropertiesWithoutUndo();
        return roll;
    }

    private static void EnsureDigitTemplate(RectTransform _root, string _name, TMPro.TextMeshProUGUI _source)
    {
        if (FindChildByName(_root, _name) != null)
        {
            return;
        }

        GameObject textObject = new GameObject(_name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TMPro.TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(_root, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1f, 1f);

        TMPro.TextMeshProUGUI text = textObject.GetComponent<TMPro.TextMeshProUGUI>();
        text.font = _source.font;
        text.fontWeight = _source.fontWeight;
        text.fontStyle = _source.fontStyle;
        text.fontSize = _source.fontSize;
        text.color = _source.color;
        text.alignment = TMPro.TextAlignmentOptions.Midline;
        text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        text.overflowMode = TMPro.TextOverflowModes.Overflow;
        text.raycastTarget = false;
        textObject.SetActive(false);
    }

    private static void EnsureSeparatorTemplate(RectTransform _root, TMPro.TextMeshProUGUI _source)
    {
        if (FindChildByName(_root, "SeparatorTemplate") != null)
        {
            return;
        }

        EnsureDigitTemplate(_root, "SeparatorTemplate", _source);
        FindChildByName(_root, "SeparatorTemplate").GetComponent<TMPro.TextMeshProUGUI>().text = ",";
    }

    private static void ApplyExplicitNavigation(Selectable _selectable, Selectable _neighbor)
    {
        Navigation navigation = _selectable.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.wrapAround = false;
        navigation.selectOnUp = null;
        navigation.selectOnDown = null;
        navigation.selectOnLeft = _neighbor;
        navigation.selectOnRight = _neighbor;
        _selectable.navigation = navigation;
    }

    private static UnityEngine.Object ReadObject(SerializedObject _so, string _propertyName)
    {
        SerializedProperty property = _so.FindProperty(_propertyName);
        return property != null ? property.objectReferenceValue : null;
    }

    private static void Require(UnityEngine.Object _reference, string _fieldName)
    {
        if (_reference == null)
        {
            Fail($"DaySettlementUI의 {_fieldName} 참조가 비어 있어 저작을 진행할 수 없습니다.");
        }
    }

    private static void Fail(string _reason)
    {
        throw new System.InvalidOperationException(_reason);
    }

    private static Transform FindChildByName(Transform _parent, string _name)
    {
        if (_parent == null)
        {
            return null;
        }

        Transform direct = _parent.Find(_name);
        if (direct != null)
        {
            return direct;
        }

        Transform[] children = _parent.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != _parent && child.name == _name)
            {
                return child;
            }
        }

        return null;
    }
}
