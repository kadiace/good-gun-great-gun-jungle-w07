using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 구조 경고 알림을 기존 공유 FeedbackUI 안내로 표시하는 View입니다.
    /// 자체 패널은 없고 표시 문구와 표시 시점은 Presenter가 소유합니다.
    /// </summary>
    public sealed class StructuralFailureWarningView : MonoBehaviour
    {
        /// <summary>
        /// 기존 FeedbackUI 알림 경로로 경고 문구를 한 번 표시합니다.
        /// </summary>
        public void Show(string _message)
        {
            if (string.IsNullOrEmpty(_message))
            {
                return;
            }

            FeedbackUI.For(this)?.ShowNotification(_message);
        }
    }
}
