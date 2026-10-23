using System.Globalization;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 습격 HUD의 탈출 진행 표시만 담당한다.
    public sealed class RaidHudView : MonoBehaviour
    {
        [SerializeField] private GameObject m_extractionPanel;
        [SerializeField] private Image m_extractionFill;
        [SerializeField] private TMP_Text m_extractionText;

        private bool m_isInitialized;
        private bool m_hasLoggedReferenceError;

        private void Awake()
        {
            if (!m_isInitialized)
            {
                ApplyHiddenState();
            }
        }

        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            ApplyHiddenState();
            if (!ValidateReferences())
            {
                return false;
            }

            m_isInitialized = true;
            return true;
        }

        public void Dispose()
        {
            ApplyHiddenState();
            m_isInitialized = false;
        }

        public void SetExtraction(bool _show, float _progress, float _remaining)
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            m_extractionPanel.SetActive(_show);
            m_extractionFill.type = Image.Type.Filled;
            m_extractionFill.fillMethod = Image.FillMethod.Horizontal;
            m_extractionFill.fillOrigin = 0;
            m_extractionFill.fillAmount = Mathf.Clamp01(_progress);
            m_extractionText.text = "탈출까지 " +
                Mathf.Max(0f, _remaining).ToString("0.0", CultureInfo.InvariantCulture);
        }

        private void ApplyHiddenState()
        {
            if (m_extractionPanel != null)
            {
                m_extractionPanel.SetActive(false);
            }
        }

        private bool ValidateReferences()
        {
            bool isValid = m_extractionPanel != null && m_extractionFill != null &&
                m_extractionText != null;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    $"[RaidHudView] 필수 참조 누락: extractionPanel={m_extractionPanel != null}, " +
                    $"extractionFill={m_extractionFill != null}, extractionText={m_extractionText != null}. " +
                    "NightRaidUI HUD 저작을 실행하세요.", this);
            }

            return isValid;
        }
    }
}
