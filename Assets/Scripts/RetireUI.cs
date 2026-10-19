using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    public sealed class RetireUI : MonoBehaviour
    {
        public const int Cost = 100000;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text continueText;
        [SerializeField] private Button lobbyButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private _DayManager dayManager;
        public bool IsOpen => panel != null && panel.activeSelf;
        public _DayManager DayManager => dayManager;
        private float openedAt;
        private bool firstEnding, leaving;

        private void Awake()
        {
            panel.SetActive(false);
            lobbyButton.onClick.AddListener(ReturnToLobby);
            continueButton.onClick.AddListener(Continue);
        }

        private void OnDestroy()
        {
            lobbyButton.onClick.RemoveListener(ReturnToLobby);
            continueButton.onClick.RemoveListener(Continue);
        }

        public void Open(bool firstEnding)
        {
            this.firstEnding = firstEnding;
            openedAt = Time.unscaledTime;
            continueText.text = $"{dayManager.DayNumber}일차부터 계속";
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            GameManager.Instance?.SetDialogue(true);
        }

        private void LogChoice(string choice) => EndingLog.Choice(EndingLog.Retire, choice,
            dayManager != null ? dayManager.DayNumber : 0, Time.unscaledTime - openedAt, firstEnding);

        private void Continue()
        {
            if (!IsOpen) return;
            LogChoice("continue");
            panel.SetActive(false);
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            GameManager.Instance?.SetDialogue(false);
        }

        private void ReturnToLobby()
        {
            if (leaving || !IsOpen || !Application.CanStreamedLevelBeLoaded("Intro")) return;
            leaving = true;
            LogChoice("lobby");
            SoundManager.RestoreBeforeRaidMusic(true);
            NightRaidTransfer.Abandon();
            TutorialManager.AbandonTraining();
            GameSaveFiles.PendingLoad = null;
            Time.timeScale = 1f;
            SceneManager.LoadSceneAsync("Intro");
        }
    }
}
