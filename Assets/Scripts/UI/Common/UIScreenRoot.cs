using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 레이어 바로 아래에 두는 화면 묶음 루트(중첩 프리팹 루트)를 표시합니다.
    /// 항상 켜 둔 채 화면 브리지와 View를 담고, 실제 패널은 그 자식으로 둡니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIScreenRoot : MonoBehaviour
    {
        /// <summary>
        /// 대상을 형제 중 맨 뒤로 보내고, 대상이 화면 묶음 안에 있으면 묶음 루트까지 같이 맨 뒤로 보내 레이어 안에서 가장 위에 그린다.
        /// </summary>
        public static void BringToFront(Transform _target)
        {
            if (_target == null)
            {
                return;
            }

            UIScreenRoot screenRoot = _target.GetComponentInParent<UIScreenRoot>(true);
            for (Transform current = _target; current != null; current = current.parent)
            {
                current.SetAsLastSibling();
                if (screenRoot == null || current == screenRoot.transform)
                {
                    return;
                }
            }
        }
    }
}
