using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop
{
    public sealed class SaveSlotsUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject openSaveSlotsButton;
        [SerializeField] private TMPro.TextMeshProUGUI title;
        [SerializeField] private Button[] slotButtons;
        [SerializeField] private TMPro.TextMeshProUGUI[] slotTexts;
        [SerializeField] private Button autoSaveButton;
        [SerializeField] private TMPro.TextMeshProUGUI autoSaveText;
        [SerializeField] private GameSaveController saveController;
        [SerializeField] private Start_UI startMenu;
        [SerializeField] private Button[] deleteButtons;
        [SerializeField] private Button autoSaveDeleteButton;
        [SerializeField] private bool confirmDelete = true;
        private void Awake()
        {
            ResolveStartMenu();
            EnsureAutoSaveSlot();
            FitSlotLayout();
            EnsureDeleteButtons();
            FitDeleteButtons();
            Refresh();
            if (panel != gameObject) Close();
        }

        private void ResolveStartMenu()
        {
            if (startMenu != null) return;
            var menus = FindObjectsByType<Start_UI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(menu => menu.gameObject.scene == gameObject.scene).ToArray();
            if (menus.Length == 1) startMenu = menus[0];
        }
        public void Initialize() => Close();
        private void OnDisable() { Close(); }
        public void Open()
        {
            if (saveController == null && startMenu == null)
            {
                FeedbackUI.For(this)?.ShowNotification("저장 관리자 연결이 없습니다.");
                return;
            }
            if(NightRaidController.Instance != null && NightRaidController.Instance.IsBusy)
            {
                FeedbackUI.For(this)?.ShowNotification("공방에 귀환한 뒤 저장하세요.");
                return;
            }
            if (title != null) title.text = saveController != null ? "게임 저장" : "게임 불러오기";
            if (panel != null) panel.SetActive(true);
            Canvas.ForceUpdateCanvases();
            FitSlotLayout();
            FitDeleteButtons();
            Refresh();
            if (mainPanel != null) mainPanel.SetActive(false);
            if (panel != null && openSaveSlotsButton != null) openSaveSlotsButton.SetActive(false);
        }
        public void Close()
        {
            FeedbackUI.For(this)?.CancelChoice(this);
            if (panel != null) panel.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(true);
            if (openSaveSlotsButton != null) openSaveSlotsButton.SetActive(true);
        }

        // 찾았다 십색기 이거야
        
        public void Refresh()
        {
            bool hasAutoSave = GameSaveFiles.TryReadAutoSave(out _); // 자동 저장 슬롯이 존재하는지 확인하는 변수임
            if (autoSaveText != null)
                autoSaveText.text = "자동 저장\n" + GameSaveFiles.AutoSaveDescription();
            if (autoSaveButton != null)
            {
                autoSaveButton.gameObject.SetActive(hasAutoSave); // 화면 띄우는거 
                autoSaveButton.interactable = saveController == null && startMenu != null && hasAutoSave; // 버튼 못만지게 하는거
            }
            if (autoSaveDeleteButton != null)
            {
                autoSaveDeleteButton.gameObject.SetActive(saveController == null && startMenu != null && hasAutoSave); // 위랑 조건 같게 ㅇㅇ
                autoSaveDeleteButton.interactable = GameSaveFiles.AutoSaveExists;
            }

            for (int i = 0; i < GameSaveFiles.SlotCount; i++)
            {
                if (deleteButtons != null && i < deleteButtons.Length && deleteButtons[i] != null)
                {
                    deleteButtons[i].gameObject.SetActive(saveController == null && startMenu != null);
                    deleteButtons[i].interactable = saveController == null && startMenu != null && GameSaveFiles.Exists(i);
                }
                if (slotTexts != null && i < slotTexts.Length && slotTexts[i] != null)
                    slotTexts[i].text = $"슬롯 {i + 1}\n{GameSaveFiles.Description(i)}";
                if (slotButtons != null && i < slotButtons.Length && slotButtons[i] != null)
                    slotButtons[i].interactable = saveController != null || GameSaveFiles.TryRead(i, out _);
            }

            FitSlotLayout(); // 갱신해서 안깨지게
        }

        private void EnsureAutoSaveSlot()
        {
            if (autoSaveButton != null || slotButtons == null || slotButtons.Length == 0 || slotButtons[0] == null)
                return;

            Button template = slotButtons[0];
            Transform parent = template.transform.parent;
            if (parent == null) return;

            int siblingIndex = template.transform.GetSiblingIndex();
            GameObject autoSlot = Instantiate(template.gameObject, parent, false);
            autoSlot.name = "AutoSaveSlot";
            autoSaveButton = autoSlot.GetComponent<Button>();
            if (autoSaveButton == null)
            {
                Destroy(autoSlot);
                return;
            }

            for (int i = 0; i < autoSaveButton.onClick.GetPersistentEventCount(); i++)
                autoSaveButton.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
            autoSaveButton.onClick.AddListener(LoadAutoSave);
            autoSaveText = autoSlot.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            autoSaveButton.transform.SetSiblingIndex(siblingIndex);

            if (parent.GetComponent<LayoutGroup>() == null && slotButtons.Length > 1 && slotButtons[1] != null)
            {
                RectTransform firstManual = slotButtons[0].transform as RectTransform;
                RectTransform secondManual = slotButtons[1].transform as RectTransform;
                RectTransform autoRect = autoSaveButton.transform as RectTransform;
                float halfSpacing = Mathf.Abs(secondManual.anchoredPosition.x - firstManual.anchoredPosition.x) * 0.5f;
                if (halfSpacing > 0f)
                {
                    for (int i = 0; i < slotButtons.Length; i++)
                    {
                        RectTransform manualRect = slotButtons[i] != null ? slotButtons[i].transform as RectTransform : null;
                        if (manualRect != null) manualRect.anchoredPosition += Vector2.right * halfSpacing;
                    }
                    autoRect.anchoredPosition = firstManual.anchoredPosition - Vector2.right * (halfSpacing * 2f);
                }
            }
        }

        private void EnsureDeleteButtons()
        {
            if (deleteButtons == null || deleteButtons.Length != GameSaveFiles.SlotCount)
                Array.Resize(ref deleteButtons, GameSaveFiles.SlotCount);

            for (int i = 0; i < deleteButtons.Length; i++)
            {
                if (deleteButtons[i] == null && slotButtons != null && i < slotButtons.Length && slotButtons[i] != null)
                    deleteButtons[i] = CreateDeleteButton(slotButtons[i].transform.parent, $"DeleteSlot{i + 1}",
                        slotTexts != null && i < slotTexts.Length && slotTexts[i] != null ? slotTexts[i].font : null);

                if (deleteButtons[i] != null)
                {
                    for (int listener = 0; listener < deleteButtons[i].onClick.GetPersistentEventCount(); listener++)
                        deleteButtons[i].onClick.SetPersistentListenerState(listener, UnityEngine.Events.UnityEventCallState.Off);
                    deleteButtons[i].onClick.RemoveAllListeners();
                    int slot = i;
                    deleteButtons[i].onClick.AddListener(() => DeleteSlot(slot));
                }
            }

            if (autoSaveButton != null && autoSaveDeleteButton == null)
                autoSaveDeleteButton = CreateDeleteButton(autoSaveButton.transform.parent, "DeleteAutoSave",
                    autoSaveText != null ? autoSaveText.font : slotTexts != null && slotTexts.Length > 0 && slotTexts[0] != null ? slotTexts[0].font : null);
            if (autoSaveDeleteButton != null)
            {
                for (int listener = 0; listener < autoSaveDeleteButton.onClick.GetPersistentEventCount(); listener++)
                    autoSaveDeleteButton.onClick.SetPersistentListenerState(listener, UnityEngine.Events.UnityEventCallState.Off);
                autoSaveDeleteButton.onClick.RemoveAllListeners();
                autoSaveDeleteButton.onClick.AddListener(DeleteAutoSave);
            }
        }

        private static Button CreateDeleteButton(Transform parent, string objectName, TMPro.TMP_FontAsset font)
        {
            if (parent == null) return null;

            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.layer = parent.gameObject.layer;
            var rect = (RectTransform)buttonObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -155f);
            rect.sizeDelta = new Vector2(340f, 48f);

            var layout = parent.GetComponent<LayoutGroup>();
            if (layout != null) buttonObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.48f, 0.12f, 0.13f, 1f);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.colors = new ColorBlock
            {
                normalColor = new Color(0.48f, 0.12f, 0.13f, 1f),
                highlightedColor = new Color(0.68f, 0.16f, 0.17f, 1f),
                pressedColor = new Color(0.32f, 0.08f, 0.09f, 1f),
                selectedColor = new Color(0.56f, 0.14f, 0.15f, 1f),
                disabledColor = new Color(0.25f, 0.22f, 0.22f, 0.65f),
                colorMultiplier = 1f,
                fadeDuration = 0.12f
            };

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            labelObject.layer = buttonObject.layer;
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var label = labelObject.GetComponent<TMPro.TextMeshProUGUI>();
            label.font = font;
            label.text = "삭제";
            label.fontSize = 24f;
            label.fontStyle = TMPro.FontStyles.Bold;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            label.color = Color.white;
            label.raycastTarget = false;

            buttonObject.SetActive(false);
            return button;
        }

        private void FitDeleteButtons()
        {
            float rowY = GetDeleteRowY();
            if (slotButtons != null && deleteButtons != null)
                for (int i = 0; i < deleteButtons.Length && i < slotButtons.Length; i++)
                    AlignDeleteButton(deleteButtons[i], slotButtons[i], rowY);
            AlignDeleteButton(autoSaveDeleteButton, autoSaveButton, rowY);
        }

        private float GetDeleteRowY()
        {
            if (deleteButtons != null)
                foreach (var button in deleteButtons)
                    if (button != null && button.transform is RectTransform rect) return rect.anchoredPosition.y;
            if (slotButtons != null && slotButtons.Length > 0 && slotButtons[0] != null && slotButtons[0].transform is RectTransform card)
                return card.anchoredPosition.y - card.rect.height * 0.5f - 40f;
            return -155f;
        }

        private static void AlignDeleteButton(Button deleteButton, Button slotButton, float rowY)
        {
            if (deleteButton == null || slotButton == null || !(deleteButton.transform is RectTransform deleteRect) ||
                !(slotButton.transform is RectTransform slotRect)) return;
            deleteRect.anchoredPosition = new Vector2(slotRect.anchoredPosition.x, rowY);
            float width = slotRect.rect.width > 0f ? slotRect.rect.width : slotRect.sizeDelta.x;
            if (width > 0f) deleteRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        // 슬롯 위치 조정 ㅇㅇ
        private void FitSlotLayout()
        {
            if (slotButtons == null || autoSaveButton == null || slotButtons.Length == 0)
            {
                FitDeleteButtons();
                return;
            }
            var parent = autoSaveButton.transform.parent;
            var layout = parent != null ? parent.GetComponent<HorizontalLayoutGroup>() : null;
            var parentRect = parent as RectTransform;
            if (layout == null || parentRect == null || parentRect.rect.width <= 0f)
            {
                FitDeleteButtons();
                return;
            }

            bool hasAutoSaveSlot = autoSaveButton.gameObject.activeSelf;
            int count = slotButtons.Length + (hasAutoSaveSlot ? 1 : 0); // 있는지 없는지 체크하기 
            float available = parentRect.rect.width - layout.padding.horizontal - layout.spacing * (count - 1);
            if (available <= 0f) { FitDeleteButtons(); return; }
            float width = available / count;
            foreach (Button slot in slotButtons)
            {
                if (slot == null) continue;
                var rect = slot.transform as RectTransform;
                if (rect != null) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            }
            var autoRect = autoSaveButton.transform as RectTransform;
            if (hasAutoSaveSlot && autoRect != null) autoRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            FitDeleteButtons(); // 애도 뭐 잘보이게 조절하는놈
        }

        private void LoadAutoSave()
        {
            if (saveController != null || startMenu == null || !GameSaveFiles.TryReadAutoSave(out _)) return;
            startMenu.LoadSlot(GameSaveFiles.AutoSaveSlotIndex);
        }
        public void DeleteSlot(int slot)
        {
            if (saveController != null || startMenu == null || slot < 0 || slot >= GameSaveFiles.SlotCount || !GameSaveFiles.Exists(slot)) return;
            if (confirmDelete)
            {
                var feedback = FeedbackUI.For(this);
                if (feedback != null && feedback.IsChoiceOpen) return;
                if (feedback != null && feedback.ShowChoice(this, $"슬롯 {slot + 1}을 삭제하시겠습니까?", () => DeleteConfirmed(slot)))
                {
                    return;
                }
            }
            DeleteConfirmed(slot);
        }

        public void DeleteAutoSave()
        {
            if (saveController != null || startMenu == null || !GameSaveFiles.AutoSaveExists) return;
            if (confirmDelete)
            {
                var feedback = FeedbackUI.For(this);
                if (feedback != null && feedback.IsChoiceOpen) return;
                if (feedback != null && feedback.ShowChoice(this, "자동 저장 파일을 삭제하시겠습니까?", DeleteAutoSaveConfirmed))
                {
                    return;
                }
            }
            DeleteAutoSaveConfirmed();
        }

        private void DeleteAutoSaveConfirmed()
        {
            try
            {
                GameSaveFiles.DeleteAutoSave();
                Refresh();
                startMenu?.RefreshSaveAvailability();
                FeedbackUI.For(this)?.ShowNotification("자동 저장 파일을 삭제했습니다.");
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            {
                FeedbackUI.For(this)?.ShowNotification("자동 저장 파일을 삭제하지 못했습니다. " + e.Message);
            }
        }
        private void DeleteConfirmed(int slot)
        {
            try
            {
                GameSaveFiles.Delete(slot);
                Refresh();
                if (startMenu != null) startMenu.RefreshSaveAvailability();
                FeedbackUI.For(this)?.ShowNotification($"슬롯 {slot + 1}을 삭제했습니다.");
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            {
                FeedbackUI.For(this)?.ShowNotification("저장 파일을 삭제하지 못했습니다. " + e.Message);
            }
        }
        public void SelectSlot(int slot)
        {
            if (slot < 0 || slot >= GameSaveFiles.SlotCount) return;
            if (saveController == null) { if (startMenu != null) startMenu.LoadSlot(slot); return; }
            if (saveController.ConfirmOverwrite && GameSaveFiles.Exists(slot))
            {
                FeedbackUI.For(this)?.ShowChoice(this, $"슬롯 {slot + 1}을 덮어쓰시겠습니까?", () => SaveConfirmed(slot));
                return;
            }
            SaveConfirmed(slot);
        }
        private void SaveConfirmed(int slot)
        {
            if (saveController.Save(slot, out string error))
            {
                Refresh();
                FeedbackUI.For(this)?.ShowNotification($"슬롯 {slot + 1}에 저장했습니다.");
            }
            else FeedbackUI.For(this)?.ShowNotification(error);
        }
    }

}
