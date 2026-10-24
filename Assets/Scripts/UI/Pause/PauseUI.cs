using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class PauseUI : MonoBehaviour
    {
        [SerializeField] private PauseView m_view;
        [SerializeField] private UIRoot m_uiRoot;
        private PausePresenter m_presenter;
        private bool m_hasLoggedWiringError;
        public PauseView View => m_view;

        private void Awake() => InitializeReferences();
        private void OnDestroy()
        {
            if (m_presenter != null) { m_presenter.Dispose(); m_presenter = null; }
            else m_view?.Dispose();
        }
        private void InitializeReferences()
        {
            if (m_view == null || m_uiRoot == null)
            {
                if (!m_hasLoggedWiringError)
                {
                    m_hasLoggedWiringError = true;
                    Debug.LogError($"[PauseUI] Required references are missing (m_uiRoot={m_uiRoot != null}, m_view={m_view != null})", this);
                }
                return;
            }
            m_uiRoot.ValidateLayers();
            m_view.Initialize();
        }
        private bool EnsurePausePresenter(GameManager _game)
        {
            if (m_view == null || m_uiRoot == null || _game == null) return false;
            if (m_presenter != null && !m_presenter.IsBoundTo(_game)) { m_presenter.Dispose(); m_presenter = null; }
            if (m_presenter == null) m_presenter = new PausePresenter(m_view, _game, m_uiRoot);
            return m_presenter.Initialize();
        }
        public void Refresh(GameManager _game)
        {
            if (_game == null) return;
            bool wasPaused = (m_presenter != null && m_presenter.IsOpen) || (m_view != null && m_view.IsVisible);
            bool pauseChanged = wasPaused != _game.IsPaused;
            if (pauseChanged && !_game.IsPaused) GetComponentInParent<UIManager>()?.Screen<SoundSettingsUI>()?.Close();
            if (!EnsurePausePresenter(_game)) return;
            m_presenter.Refresh();
        }
    }
}
