using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class CoreDispenser : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform drawRoot;
        [SerializeField] private EquipableWeapon corePrefab;
        [SerializeField] private TMPro.TMP_Text weaponNameLabel;
        [SerializeField] private int presetSlot = -1;
        [SerializeField] private EquipableWeapon weaponPreview;
        private string displayedCode = "\0";
        private WeaponPresets Presets => GameManager.Instance != null && GameManager.Instance.UI != null && GameManager.Instance.UI.Player != null
            ? GameManager.Instance.UI.Player.GetComponent<WeaponPresets>() : null;
        private const int CorePrice = WeaponPurchasePrices.Core;
        public Transform OutlineRoot => drawRoot != null ? drawRoot : transform;
        public bool IsBasicCoreDispenser => presetSlot < 0 && corePrefab != null;

        private void Awake() => UpdateNameLabel();
        private void LateUpdate()
        {
            UpdateNameLabel();
            var presets = Presets;
            if (presetSlot < 0 || presets == null || weaponPreview == null) return;
            string code = presets.GetCode(presetSlot);
            if (displayedCode == code) return;
            displayedCode = code;
            weaponPreview.gameObject.SetActive(!string.IsNullOrEmpty(code));
            if (string.IsNullOrEmpty(code)) return;
            try
            {
                var blueprint = WeaponShareCode.Decode(code);
                var assembly = weaponPreview.GetComponent<CubeAssembly>();
                var player = GameManager.Instance.UI.Player;
                var plan = WeaponShareCode.Resolve(blueprint, assembly, player.GetComponent<PlayerModuleUnlocks>(), CorePrice, out _, out _);
                if (!assembly.RestoreLayout(plan)) return;
                weaponPreview.SetWeaponName(blueprint.name);
                foreach (var behaviour in weaponPreview.GetComponentsInChildren<MonoBehaviour>(true))
                    if (!(behaviour is PresetWeaponPreview)) behaviour.enabled = false;
                foreach (var body in weaponPreview.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
            }
            catch (System.Exception e) { Debug.LogWarning(e.Message, this); }
        }
        private void UpdateNameLabel()
        {
            if (weaponNameLabel == null) return;
            string name = corePrefab != null ? corePrefab.WeaponName : "";
            if (presetSlot >= 0 && Presets != null)
            {
                string code = Presets.GetCode(presetSlot);
                if (string.IsNullOrEmpty(code)) name = "";
                else if (code != displayedCode)
                {
                    try { name = WeaponShareCode.Decode(code).name; }
                    catch (System.Exception) { name = ""; }
                }
                else name = weaponPreview != null ? weaponPreview.WeaponName : name;
            }
            if (weaponNameLabel.text != name) weaponNameLabel.text = name;
        }

        public bool CanInteract(Interaction interactor)
        {
            if (!isActiveAndEnabled || GameManager.GameplayBlocked || corePrefab == null ||
                interactor == null || interactor.Player == null) return false;
            if (presetSlot >= 0 && interactor.Player.GetComponent<WeaponPresets>() != null &&
                string.IsNullOrEmpty(interactor.Player.GetComponent<WeaponPresets>().GetCode(presetSlot))) return false;
            WeaponHolder holder = interactor.Player.GetComponent<WeaponHolder>();
            PlayerWallet wallet = PlayerWallet.ForPlayer(interactor.Player);
            return holder != null && holder.CanEquip && holder.EquippedWeapon == null &&
                wallet != null;
        }

        public bool Interact(Interaction interactor)
        {
            if (!CanInteract(interactor)) return false;
            WeaponHolder holder = interactor.Player.GetComponent<WeaponHolder>();
            PlayerWallet wallet = PlayerWallet.ForPlayer(interactor.Player);
            if (wallet == null) return false;
            long dollarTotal = CorePrice;
            long materialTotal = 0;
            var templateAssembly = corePrefab.GetComponent<CubeAssembly>();
            CubeAssembly.LayoutSnapshot presetPlan = null;
            WeaponShareCode.Blueprint blueprint = null;
            var presets = interactor.Player.GetComponent<WeaponPresets>();
            if (presetSlot >= 0 && presets != null)
            {
                try
                {
                    blueprint = WeaponShareCode.Decode(presets.GetCode(presetSlot));
                    presetPlan = WeaponShareCode.Resolve(blueprint, templateAssembly, interactor.Player.GetComponent<PlayerModuleUnlocks>(),
                        CorePrice, out int money, out int materials);
                    dollarTotal += money; materialTotal += materials;
                }
                catch (System.Exception e) { FeedbackUI.For(this)?.ShowNotification(e.Message); return false; }
            }
            else if (templateAssembly != null)
            {
                foreach (var block in templateAssembly.GetComponentsInChildren<PlacedBlock>())
                {
                    if (block.transform.parent != templateAssembly.transform) continue;
                    GetModulePrice(templateAssembly, block, out int dollars, out int materials);
                    dollarTotal += dollars;
                    materialTotal += materials;
                }
            }
            if (dollarTotal > int.MaxValue || materialTotal > int.MaxValue) return false;
            EquipableWeapon purchasedCore = null;
            bool purchased = wallet.TryPurchaseModule((int)dollarTotal, (int)materialTotal, () =>
            {
                EquipableWeapon core = Instantiate(corePrefab, transform.position, transform.rotation);
                purchasedCore = core;
                core.name = corePrefab.name;
                if (presetPlan != null)
                {
                    if (!core.GetComponent<CubeAssembly>().RestoreLayout(presetPlan)) { Destroy(core.gameObject); return false; }
                    core.SetWeaponName(blueprint.name);
                    core.GetComponentInChildren<StatCollector>()?.Recalculate();
                }
                if (holder.Equip(core)) return true;
                Destroy(core.gameObject);
                return false;
            });
            if (!purchased) return false;
            // RestoreLayout already restores the preset's cost. Record this purchase once, not add it again.
            purchasedCore.RestoreConstructionPurchaseCost((int)dollarTotal);
            var assembly = purchasedCore.GetComponent<CubeAssembly>();
            if (assembly != null)
                foreach (var block in assembly.GetComponentsInChildren<PlacedBlock>())
                {
                    if (block.transform.parent != assembly.transform) continue;
                    GetModulePrice(assembly, block, out int dollars, out int materials);
                    block.RecordPurchaseCost(dollars);
                    block.RecordModuleCreditCost(materials);
                }
            if (TutorialManager.Instance != null) TutorialManager.Instance.NotifySignal("core-picked-up", this);
            return true;
        }

        private static void GetModulePrice(CubeAssembly assembly, PlacedBlock block, out int dollars, out int materials)
        {
            var data = assembly.GetModuleData(block.ModuleSlot);
            dollars = data != null ? Mathf.Max(0, data.BuyPrice) : WeaponPurchasePrices.Module;
            materials = data != null ? Mathf.Max(0, data.ModuleCreditCost) : 0;
        }
    }
}
