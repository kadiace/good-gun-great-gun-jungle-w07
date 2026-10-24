using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class UIManager : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private FirstPersonPlayer player;
        [SerializeField]
        private CubeBuilder builder;
        [SerializeField]
        private GameObject eraseModeUI;
        private readonly Dictionary<System.Type, Component> m_screenCache = new Dictionary<System.Type, Component>();
        [SerializeField] private AimUI aimUI;
        [SerializeField] private HoveringUI hoveringUI;
        [SerializeField] private NightRaidUI nightRaidUI;
        public T Screen<T>() where T : Component
        {
            if (m_screenCache.TryGetValue(typeof(T), out Component cached) && cached != null) return (T)cached;
            T found = GetComponentInChildren<T>(true);
            m_screenCache[typeof(T)] = found;
            return found;
        }
        public T[] Screens<T>() where T : Component => GetComponentsInChildren<T>(true);
        private FirstPersonPlayer ResolvePlayer() => player != null ? player : (gameManager != null ? gameManager.Player : null);

        private void Awake()
        {
            transform.localScale = Vector3.one;
            GameManager contextGame = gameManager != null ? gameManager : GameManager.Instance;
            FirstPersonPlayer contextPlayer = contextGame != null ? contextGame.Player : player;
            if (contextPlayer == null) contextPlayer = ResolvePlayer();
            if (builder == null && contextPlayer != null) builder = contextPlayer.GetComponentInChildren<CubeBuilder>(true);
            Interaction interaction = contextPlayer != null ? contextPlayer.GetComponentInChildren<Interaction>(true) : null;
            UIContext context = new UIContext { Game = contextGame, Ui = this, Player = contextPlayer, Builder = builder, Interaction = interaction };
            foreach (MonoBehaviour component in GetComponentsInChildren<MonoBehaviour>(true))
                if (component is IUIScreen uiScreen) uiScreen.InitializeScreen(context);
            if (eraseModeUI != null) eraseModeUI.SetActive(false);
            if (builder != null && eraseModeUI != null) builder.SetEraseModeUI(eraseModeUI);
        }
        private void OnDisable()
        {
            Screen<FeedbackUI>()?.ForceClose();
        }
    }
}
