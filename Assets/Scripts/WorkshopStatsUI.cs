using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class WorkshopStatsUI : MonoBehaviour
    {
        [SerializeField] private WorkshopSession session;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMPro.TextMeshProUGUI values;
        private string previousText;

        private void Awake() => panel?.SetActive(false);
        private void OnDisable() => panel?.SetActive(false);

        private void LateUpdate()
        {
            Workbench bench = session != null ? session.ActiveWorkbench : null;
            bool visible = session != null && session.IsEditing;
            if (panel != null && panel.activeSelf != visible) panel.SetActive(visible);
            if (!visible || values == null) return;
            PowerCore core = bench.CurrentCore;
            string text = "작업대에 무기를 놓아 주세요";
            if (core != null)
            {
                WeaponStats stats = core.GetCurrentStats();
                text = $"관통      {stats.penetration:0.##}\n연사      {stats.fireRate:0.##} 회/초\n폭발      {stats.explosion:0.##}\n무게      {stats.weight:0.##}\n수직 반동(총구당) {core.MaximumVerticalRecoilPerShotDegrees:0.##}°/발";
            }
            if (text == previousText) return;
            values.text = text;
            previousText = text;
        }
    }
}
