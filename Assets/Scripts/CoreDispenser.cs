using UnityEngine;

namespace WeaponWorkshop
{
    // 코어 및 프리셋 무기 배출기 브리지.
    // 무기 구매·장착·월드 프리뷰 생성 및 지갑 차감은 이 컴포넌트가 유지하고,
    // 무기 라벨 해석 및 오류 알림 표시는 CoreDispenserPresenter가 전담한다.
    public sealed class CoreDispenser : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform drawRoot;
        [SerializeField] private EquipableWeapon corePrefab;
        [SerializeField] private TMPro.TMP_Text weaponNameLabel;
        [SerializeField] private int presetSlot = -1;
        [SerializeField] private EquipableWeapon weaponPreview;

        private readonly CoreDispenserPresenter m_presenter = new CoreDispenserPresenter();

        private string displayedCode = "\0";
        private bool dispensing;
        private const int CorePrice = WeaponPurchasePrices.Core;

        private WeaponPresets Presets => GameManager.Instance != null && GameManager.Instance.Player != null
            ? GameManager.Instance.Player.GetComponent<WeaponPresets>()
            : null;

        public Transform OutlineRoot => drawRoot != null ? drawRoot : transform;
        public int PresetSlot => presetSlot;
        public bool IsBasicCoreDispenser => presetSlot < 0 && corePrefab != null;
        public bool OwnsSavedPreview(PresetWeaponPreview preview) => presetSlot >= 0 &&
            weaponPreview != null && preview != null && preview.transform == weaponPreview.transform;

        private void Awake()
        {
            if (presetSlot >= 0 && weaponPreview != null)
            {
                weaponPreview.gameObject.SetActive(false);
            }

            UpdateNameLabel();
        }

        private void LateUpdate()
        {
            UpdateNameLabel();
            var presets = Presets;
            if (presetSlot < 0 || presets == null || weaponPreview == null)
            {
                return;
            }

            string code = presets.GetCode(presetSlot);
            if (displayedCode == code)
            {
                return;
            }

            displayedCode = code;
            weaponPreview.gameObject.SetActive(!string.IsNullOrEmpty(code));
            if (string.IsNullOrEmpty(code))
            {
                return;
            }

            try
            {
                var blueprint = WeaponShareCode.Decode(code);
                var assembly = weaponPreview.GetComponent<CubeAssembly>();
                var player = GameManager.Instance.Player;
                var plan = WeaponShareCode.Resolve(blueprint, assembly, player.GetComponent<PlayerModuleUnlocks>(), CorePrice, out _, out _);
                if (!assembly.RestoreLayout(plan))
                {
                    return;
                }

                weaponPreview.SetWeaponName(blueprint.name);
                foreach (var behaviour in weaponPreview.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!(behaviour is PresetWeaponPreview))
                    {
                        behaviour.enabled = false;
                    }
                }

                foreach (var body in weaponPreview.GetComponentsInChildren<Rigidbody>(true))
                {
                    body.isKinematic = true;
                    body.useGravity = false;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(e.Message, this);
            }
        }

        private void UpdateNameLabel()
        {
            if (weaponNameLabel == null)
            {
                return;
            }

            string name = m_presenter.ResolveDispenserWeaponName(
                corePrefab,
                presetSlot,
                Presets,
                displayedCode,
                weaponPreview);

            m_presenter.UpdateNameLabel(weaponNameLabel, name);
        }

        // 저장 슬롯만 장착 중 교체를 허용하고, 빈 코어/고정 프리팹 지급기는 맨손 조건을 유지한다.
        public bool CanInteract(Interaction interactor)
        {
            if (!isActiveAndEnabled || dispensing || GameManager.GameplayBlocked || corePrefab == null ||
                interactor == null || interactor.Player == null)
            {
                return false;
            }

            var presets = interactor.Player.GetComponent<WeaponPresets>();
            if (presetSlot >= 0 && (presets == null || string.IsNullOrEmpty(presets.GetCode(presetSlot))))
            {
                return false;
            }

            WeaponHolder holder = interactor.Player.GetComponent<WeaponHolder>();
            PlayerWallet wallet = PlayerWallet.ForPlayer(interactor.Player);
            if (holder == null || !holder.CanEquip || wallet == null)
            {
                return false;
            }

            var held = holder.EquippedWeapon;
            return held == null || presetSlot >= 0 && held.IsEquipped && !held.IsMoving;
        }

        public bool Interact(Interaction interactor)
        {
            if (!CanInteract(interactor))
            {
                return false;
            }

            dispensing = true;
            try
            {
                return TryDispense(interactor);
            }
            finally
            {
                dispensing = false;
            }
        }

        /// <summary>
        /// 디스펜서에서 코어 또는 저장된 프리셋 무기를 제작하여 플레이어에게 지급하고 결제합니다.
        /// </summary>
        /// <param name="interactor">상호작용한 플레이어 Interaction 컴포넌트입니다.</param>
        /// <returns>지급과 결제가 성공했으면 true, 실패했으면 false를 반환합니다.</returns>
        private bool TryDispense(Interaction interactor)
        {
            WeaponHolder holder = interactor.Player.GetComponent<WeaponHolder>();
            PlayerWallet wallet = PlayerWallet.ForPlayer(interactor.Player);
            if (wallet == null)
            {
                return false;
            }

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
                    presetPlan = WeaponShareCode.Resolve(
                        blueprint,
                        templateAssembly,
                        interactor.Player.GetComponent<PlayerModuleUnlocks>(),
                        CorePrice,
                        out int money,
                        out int materials);
                    dollarTotal += money;
                    materialTotal += materials;
                }
                catch (System.Exception e)
                {
                    m_presenter.ShowErrorNotification(this, e.Message);
                    return false;
                }
            }
            else if (templateAssembly != null)
            {
                int shotgunCount = 0;
                foreach (var block in templateAssembly.GetComponentsInChildren<PlacedBlock>(true))
                {
                    if (block.transform.parent != templateAssembly.transform || !block.gameObject.activeSelf)
                    {
                        continue;
                    }

                    GetModulePrice(templateAssembly, block, ref shotgunCount, out int dollars, out int materials);
                    dollarTotal += dollars;
                    materialTotal += materials;
                }
            }

            if (dollarTotal > int.MaxValue || materialTotal > int.MaxValue)
            {
                return false;
            }

            if (wallet.Balance < dollarTotal)
            {
                wallet.CheckPurchaseFunds((int)dollarTotal, false);
                return false;
            }

            EquipableWeapon purchasedCore = null;
            // Preset purchases include their modules, so they are logged apart from a bare core.
            string partKind = presetPlan != null ? corePrefab.name + "+preset" : corePrefab.name;
            bool purchased = wallet.TryPurchaseModule((int)dollarTotal, (int)materialTotal, () =>
            {
                EquipableWeapon core = null;
                try
                {
                    core = Instantiate(corePrefab, transform.position, transform.rotation);
                    purchasedCore = core;
                    core.name = corePrefab.name;
                    if (presetPlan != null)
                    {
                        if (!core.GetComponent<CubeAssembly>().RestoreLayout(presetPlan))
                        {
                            core.gameObject.SetActive(false);
                            Destroy(core.gameObject);
                            return false;
                        }

                        core.SetWeaponName(blueprint.name);
                        core.GetComponentInChildren<StatCollector>()?.Recalculate();
                    }
                }
                catch (System.Exception e)
                {
                    if (core != null)
                    {
                        core.gameObject.SetActive(false);
                        Destroy(core.gameObject);
                    }

                    m_presenter.ShowErrorNotification(this, e.Message);
                    return false;
                }

                bool equipped = presetPlan != null ? holder.TryEquipWithDrop(core) : holder.Equip(core);
                if (equipped)
                {
                    return true;
                }

                core.gameObject.SetActive(false);
                Destroy(core.gameObject);
                return false;
            }, partKind);

            if (!purchased)
            {
                return false;
            }

            wallet.NotifyPartPurchased(partKind, (int)dollarTotal, (int)materialTotal);
            // RestoreLayout already restores the preset's cost. Record this purchase once, not add it again.
            purchasedCore.RestoreConstructionPurchaseCost((int)dollarTotal);
            var assembly = purchasedCore.GetComponent<CubeAssembly>();
            if (assembly != null && presetPlan == null)
            {
                int shotgunCount = 0;
                foreach (var block in assembly.GetComponentsInChildren<PlacedBlock>(true))
                {
                    if (block.transform.parent != assembly.transform || !block.gameObject.activeSelf)
                    {
                        continue;
                    }

                    GetModulePrice(assembly, block, ref shotgunCount, out int dollars, out int materials);
                    block.RecordPurchaseCost(dollars);
                    block.RecordModuleCreditCost(materials);
                }
            }

            if (TutorialManager.Instance != null && presetSlot < 0)
            {
                TutorialManager.Instance.NotifySignal("core-picked-up", this);
            }

            if (TutorialManager.Instance != null && presetSlot == 0 &&
                TutorialManager.Instance.CurrentStep != null && TutorialManager.Instance.CurrentStep.id == "raid-pick-up")
            {
                TutorialManager.Instance.NotifySignal("raid-preset-one-equipped", this);
            }

            return true;
        }

        private static void GetModulePrice(CubeAssembly assembly, PlacedBlock block, ref int shotgunCount, out int dollars, out int materials)
        {
            var data = assembly.GetModuleData(block.ModuleSlot);
            dollars = data != null ? Mathf.Max(0, data.BuyPrice) : WeaponPurchasePrices.Module;
            materials = WeaponPurchasePrices.ModuleCredits(data, shotgunCount);
            if (data != null && data.ModuleType == StatType.Shotgun)
            {
                shotgunCount++;
            }
        }
    }
}
