using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class InteractionLabel : MonoBehaviour
    {
        [SerializeField] private string title;
        [SerializeField, TextArea(1, 3)] private string unavailableCondition = "현재는 상호작용할 수 없습니다.";
        public string Title => title;
        public string UnavailableCondition => unavailableCondition;
        public void SetTitle(string value) => title = value;
    }
}
