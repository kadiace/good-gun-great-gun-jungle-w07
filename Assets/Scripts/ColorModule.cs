using UnityEngine;

namespace WeaponWorkshop
{
    // 컬러 모듈 블록 하나의 색. 색은 SO가 아니라 설치한 블록마다 따로 저장한다.
    // 탄 색은 ProjectileStatsSource가 경로의 ColorModule 색을 LightMixer(빛 혼합)로 섞어 칠한다.
    // 겉 통(tintRenderers)만 이 색으로 보이고, 도색(ModuleFacePaint)은 적용하지 않는다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlacedBlock))]
    public sealed class ColorModule : MonoBehaviour
    {
        [SerializeField, Tooltip("고른 색으로 칠할 겉 통 렌더러.")]
        private Renderer[] tintRenderers = new Renderer[0];
        [SerializeField] private Color color = Color.white;
        private MaterialPropertyBlock block;

        public Color Color => color;

        private void OnEnable() => ApplyTint();

        public void SetColor(Color value)
        {
            value.a = 1f;
            color = value;
            ApplyTint();
        }

        private void ApplyTint()
        {
            if (block == null) block = new MaterialPropertyBlock();
            foreach (Renderer renderer in tintRenderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
