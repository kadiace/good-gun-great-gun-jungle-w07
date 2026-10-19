using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class Start_UI : MonoBehaviour
{
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private string gameSceneName = "Main";
    [SerializeField] private WeaponWorkshop.SaveSlotsUI loadSlotsUI;
    [SerializeField] private UnityEngine.UI.Button keepStartGameButton;
    private bool loading;

    private void Awake()
    {
        loading = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (menuPanel != null) menuPanel.SetActive(true);
        RefreshSaveAvailability();
    }

    // LogKit 자동 초기화(AfterSceneLoad)가 Awake보다 늦으므로 세션 시작 기록은 Start에서 한다.
    private void Start() => WeaponWorkshop.PlayerIdentity.LogSessionStart();

    // 세이브 파일 읽고 지 상태 결정하는쪽으로 로직 바꿈요
    public void RefreshSaveAvailability()
    {
        if (keepStartGameButton == null) return;
        bool hasAvailableSave = WeaponWorkshop.GameSaveFiles.LatestSlot() >= 0;
        keepStartGameButton.gameObject.SetActive(hasAvailableSave);
    }

    public void KeepStartGame()
    {
        if (loading) return;
        int slot = WeaponWorkshop.GameSaveFiles.LatestSlot();
        if (slot < 0)
        {
            WeaponWorkshop.GameSaveFiles.PendingLoad = null;
            RefreshSaveAvailability();
            WeaponWorkshop.FeedbackUI.For(this)?.ShowNotification("불러올 수 있는 저장 파일이 없습니다.");
            return;
        }
        LoadSlot(slot);
    }

    public void OpenLoadPanel() => loadSlotsUI?.Open();

    public void LoadSlot(int slot)
    {
        if (loading) return;
        if (!WeaponWorkshop.GameSaveFiles.TryReadSlot(slot, out var data)) return;
        WeaponWorkshop.GameSaveFiles.PendingLoad = data;
        WeaponWorkshop.PlayerIdentity.Adopt(data.saveId);
        BeginGame(string.IsNullOrEmpty(data.sceneName) ? gameSceneName : data.sceneName);
    }

    public void GameStart()
    {
        if (loading) return;
        WeaponWorkshop.GameSaveFiles.PendingLoad = null;
        WeaponWorkshop.GameSaveFiles.ClearRaidRecovery();
        WeaponWorkshop.PlayerIdentity.BeginNewSave();
        BeginGame();
    }

    private void BeginGame(string savedScene = null)
    {
        if (loading) return;
        string sceneToLoad = string.IsNullOrEmpty(savedScene) ? gameSceneName : savedScene;
        if (!Application.CanStreamedLevelBeLoaded(sceneToLoad))
        {
            WeaponWorkshop.GameSaveFiles.PendingLoad = null;
            Debug.LogError($"시작할 씬을 빌드 목록에 등록하세요: {gameSceneName}", this);
            return;
        }
        loading = true;
        LogKit.GameLogger.Data("Info", "System", string.IsNullOrEmpty(savedScene) ? "새 게임 시작" : $"저장 게임 시작({savedScene})",
            new WeaponWorkshop.GameStartLog
            {
                installId = WeaponWorkshop.PlayerIdentity.InstallId, saveId = WeaponWorkshop.PlayerIdentity.SaveId,
                savedScene = savedScene ?? "", newGame = string.IsNullOrEmpty(savedScene),
                retired = WeaponWorkshop.GameSaveFiles.PendingLoad?.retired == true,
                endChestOpened = WeaponWorkshop.GameSaveFiles.PendingLoad?.endChestOpened == true
            });
        SceneManager.LoadSceneAsync(sceneToLoad);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
