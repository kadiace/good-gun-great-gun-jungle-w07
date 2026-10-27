using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 모듈 해금 알림을 FeedbackUI 경로로 표시하는 View 컴포넌트입니다.
    /// 자체 캔버스는 없으며 알림 문자열 표시를 담당합니다.
    /// </summary>
    public sealed class ModuleUnlockNoticeView : MonoBehaviour
    {
        [SerializeField] private UIManager m_uiManager;

        public void BindReferences(UIManager _uiManager)
        {
            m_uiManager = _uiManager;
        }

        public void ShowNotification(string _message)
        {
            if (string.IsNullOrEmpty(_message))
            {
                return;
            }

            if (m_uiManager != null)
            {
                m_uiManager.Screen<FeedbackUI>()?.ShowNotification(_message);
                return;
            }

            FeedbackUI.For(this)?.ShowNotification(_message);
        }
    }
}
