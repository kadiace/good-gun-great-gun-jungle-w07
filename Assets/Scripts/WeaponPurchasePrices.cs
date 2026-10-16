namespace WeaponWorkshop
{
    // 구매와 판매 원가가 같은 가격을 참조합니다.
    public static class WeaponPurchasePrices
    {
        public const int Core = 50;
        public const int Module = 30;

        public static int ModuleCredits(ModuleData data, int installedShotgunCount)
        {
            if (data == null) return 0;
            long cost = UnityEngine.Mathf.Max(0, data.ModuleCreditCost);
            if (data.ModuleType == StatType.Shotgun) cost *= (long)installedShotgunCount + 1;
            return (int)System.Math.Min(int.MaxValue, cost);
        }

        public static int InstalledShotgunCount(CubeAssembly assembly)
        {
            if (assembly == null) return 0;
            int count = 0;
            foreach (var block in assembly.GetComponentsInChildren<PlacedBlock>(true))
                if (block.transform.parent == assembly.transform && block.gameObject.activeSelf &&
                    ModuleCounter.TryGetKind(block, out ModuleKind kind) && kind == ModuleKind.Shotgun)
                    count++;
            return count;
        }
    }
}
