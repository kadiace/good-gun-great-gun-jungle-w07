using UnityEngine;

/// <summary>
/// 인트로 시작 메뉴의 씬 연결 브리지입니다. 기존 직렬화 참조와 버튼 persistent 호출 대상 public 메서드를 유지하고
/// 실제 동작은 StartMenuView·StartMenuPresenter로 위임합니다. 버튼 onClick을 여기서 다시 구독하지 않습니다.
/// </summary>
public sealed class Start_UI : MonoBehaviour
{
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private string gameSceneName = "Main";
    [SerializeField] private WeaponWorkshop.SaveSlotsUI loadSlotsUI;
    [SerializeField] private UnityEngine.UI.Button keepStartGameButton;
    [SerializeField] private WeaponWorkshop.StartMenuView m_view;

    private WeaponWorkshop.StartMenuPresenter m_presenter;
    private bool m_hasLoggedMissingView;

    private void Awake()
    {
        // 인트로 초기 정책 유지: 시간 배율 복원과 커서 표시.
        WeaponWorkshop.UIInputPolicy.ApplyLobbyPolicy();
        EnsurePresenter();
    }

    // LogKit 자동 초기화(AfterSceneLoad)가 Awake보다 늦으므로 세션 시작 기록은 Start에서 한다.
    private void Start() => WeaponWorkshop.PlayerIdentity.LogSessionStart();

    private void OnDestroy()
    {
        if (m_presenter != null)
        {
            m_presenter.Dispose();
            m_presenter = null;
        }
    }

    public void RefreshSaveAvailability()
    {
        EnsurePresenter();
        m_presenter?.RefreshSaveAvailability();
    }

    public void KeepStartGame()
    {
        EnsurePresenter();
        m_presenter?.ContinueGame();
    }

    public void OpenLoadPanel()
    {
        EnsurePresenter();
        m_presenter?.OpenLoadPanel();
    }

    public void LoadSlot(int slot)
    {
        EnsurePresenter();
        m_presenter?.LoadSlot(slot);
    }

    public void GameStart()
    {
        EnsurePresenter();
        m_presenter?.StartNewGame();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void EnsurePresenter()
    {
        if (m_presenter != null)
        {
            return;
        }

        if (m_view == null)
        {
            if (!m_hasLoggedMissingView)
            {
                m_hasLoggedMissingView = true;
                Debug.LogError($"[{nameof(Start_UI)}] StartMenuView 참조(m_view)가 비어 있습니다. StartUI_Manager 프리팹에 StartMenuView를 추가하고 연결하세요.", this);
            }

            return;
        }

        // 씬 오버라이드가 반영된 브리지 유효 참조를 View에 먼저 라우팅한다.
        m_view.BindReferences(menuPanel, keepStartGameButton);
        var domain = new WeaponWorkshop.StartMenuDomainAdapter(loadSlotsUI, this);
        m_presenter = new WeaponWorkshop.StartMenuPresenter(m_view, domain, gameSceneName);
        m_presenter.Initialize();
    }
}
