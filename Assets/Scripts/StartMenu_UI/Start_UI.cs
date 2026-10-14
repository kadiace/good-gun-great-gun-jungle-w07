using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class Start_UI : MonoBehaviour
{
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private string gameSceneName = "SampleScene";
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

    public void RefreshSaveAvailability()
    {
        if (keepStartGameButton != null) keepStartGameButton.interactable = WeaponWorkshop.GameSaveFiles.LatestSlot() >= 0;
    }

    public void KeepStartGame()
    {
        if (loading) return;
        int slot = WeaponWorkshop.GameSaveFiles.LatestSlot();
        if (slot < 0)
        {
            // Never let a hidden recovery copy or stale in-memory load start a game by itself.
            WeaponWorkshop.GameSaveFiles.PendingLoad = null;
            RefreshSaveAvailability();
            WeaponWorkshop.FeedbackUI.For(this)?.ShowNotification("불러올 수 있는 저장 파일이 없습니다.");
            return;
        }
        if (WeaponWorkshop.GameSaveFiles.TryReadRaidRecovery(out var recovery) &&
            (!WeaponWorkshop.GameSaveFiles.TryReadSlot(slot, out var saved) || recovery.savedUtcTicks > saved.savedUtcTicks))
        {
            WeaponWorkshop.GameSaveFiles.PendingLoad = recovery;
            BeginGame(string.IsNullOrEmpty(recovery.sceneName) ? gameSceneName : recovery.sceneName);
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
        BeginGame(string.IsNullOrEmpty(data.sceneName) ? gameSceneName : data.sceneName);
    }

    public void GameStart()
    {
        if (loading) return;
        WeaponWorkshop.GameSaveFiles.PendingLoad = null;
        WeaponWorkshop.RaidDeathLoot.Clear();
        WeaponWorkshop.GameSaveFiles.ClearRaidRecovery();
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
