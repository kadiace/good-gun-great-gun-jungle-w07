using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    public sealed class RetireUI : MonoBehaviour
    {
        public const int Cost = 1000000;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text continueText;
        [SerializeField] private Button lobbyButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private _DayManager dayManager;
        public bool IsOpen => panel != null && panel.activeSelf;

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

        public void Open()
        {
            continueText.text = $"{dayManager.DayNumber}일차부터 계속";
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            GameManager.Instance?.SetDialogue(true);
        }

        private void Continue()
        {
            panel.SetActive(false);
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            GameManager.Instance?.SetDialogue(false);
        }

        private void ReturnToLobby()
        {
            if (!Application.CanStreamedLevelBeLoaded("Intro")) return;
            SoundManager.RestoreBeforeRaidMusic(true);
            NightRaidTransfer.Abandon();
            TutorialManager.AbandonTraining();
            GameSaveFiles.PendingLoad = null;
            Time.timeScale = 1f;
            SceneManager.LoadSceneAsync("Intro");
        }
    }
}
