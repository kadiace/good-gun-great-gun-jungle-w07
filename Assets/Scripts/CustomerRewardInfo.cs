using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // 정액 계약은 생성 때, 달성 여부와 최초 클리어 배율은 납품 때 계산한다.
    public static class CustomerRewardInfo
    {
        public const string BasicRequestId = "weapon/basic";

        public readonly struct Quote
        {
            public readonly int baseReward, maximumBaseReward, price, maximumPrice, matchedRequests;
            public readonly float multiplier;
            public Quote(long reward, long maximum, int matched, float saleMultiplier, int constructionCost = 0, float failedRatio = 0.5f)
            {
                baseReward = Clamp(reward); maximumBaseReward = Clamp(maximum);
                matchedRequests = matched; multiplier = saleMultiplier;
                // 제작비는 클리어 배율에서 제외하고, 충족한 요청이 없으면 절반만 회수한다.
                int costRecovery = matched > 0 ? constructionCost : Scale(constructionCost, failedRatio);
                price = Clamp((long)Scale(baseReward, multiplier) + costRecovery);
                maximumPrice = Clamp((long)Scale(maximumBaseReward, multiplier) + constructionCost);
            }
        }

        public static int RequestReward(CustomerRequestData.Request request, int generatedDay)
            => request == null ? 0 : Clamp((long)Mathf.Max(0, request.reward) +
                (long)Mathf.Max(0, generatedDay) * Mathf.Max(0, request.rewardPerDay));

        public static void InitializeOrder(CustomerProfile profile, int day)
        {
            if (profile == null) return;
            if (!profile.IsDifficult && profile.RequestIds.Length == 0)
                profile.RequestIds = new[] { BasicRequestId };
            var state = profile.OrderState ??= CustomerOrderState.Create();
            // 보상이 저장된 주문은 테이블을 수정해도 재계약하지 않는다.
            if (state.rewards != null) return;
            // 구형 JSON에 없는 정수 필드는 0일 수 있으므로 보상 스냅샷 유무로 이관을 판단한다.
            state.generatedDay = Mathf.Max(0, day);
            var table = TableManager.Instance.CustomerRequest;
            var rewards = new List<CustomerOrderState.RequestReward>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in profile.RequestIds)
            {
                var request = table.FindRequest(id);
                if (request == null || !seen.Add(request.id)) continue;
                rewards.Add(new CustomerOrderState.RequestReward
                { requestId = request.id, amount = RequestReward(request, state.generatedDay) });
            }
            state.rewards = rewards.ToArray();
        }

        public static int ContractReward(CustomerProfile profile, string requestId)
        {
            string canonical = TableManager.Instance.CustomerRequest.FindRequest(requestId)?.id;
            foreach (var row in profile?.OrderState?.rewards ?? Array.Empty<CustomerOrderState.RequestReward>())
                if (row != null && row.requestId == canonical) return Mathf.Max(0, row.amount);
            return 0;
        }

        public static Quote Calculate(CustomerProfile profile, EquipableWeapon weapon, float multiplier,
            CustomerRequestInfo.WeaponSnapshot weaponSnapshot = null)
        {
            if (profile == null) return new Quote(0, 0, 0, multiplier);
            if (profile.IsDifficult)
            {
                int reward = weapon == null ? 0 : Scale(weapon.PaidConstructionCost,
                    TableManager.Instance.CustomerOrder.sale.difficultCustomerRatio);
                return new Quote(reward, reward, 0, multiplier);
            }
            var table = TableManager.Instance.CustomerRequest;
            var snapshot = weaponSnapshot ?? new CustomerRequestInfo.WeaponSnapshot(weapon);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            long rewardTotal = 0, maximum = 0;
            int matched = 0;
            foreach (string id in profile.RequestIds)
            {
                var request = table.FindRequest(id);
                if (request == null || !seen.Add(request.id)) continue;
                int reward = ContractReward(profile, request.id);
                maximum += reward;
                if (!table.MeetsRequest(request.id, snapshot)) continue;
                rewardTotal += reward;
                matched++;
            }
            return new Quote(rewardTotal, maximum, matched, multiplier,
                weapon != null ? weapon.PaidConstructionCost : 0, TableManager.Instance.CustomerOrder.sale.failedSaleRatio);
        }

        public static bool ValidSavedRewards(CustomerOrderState state, string[] requestIds)
        {
            if (state == null || state.rewards == null) return true; // 이전 저장은 복원 일차로 최초 1회 계약.
            if (state.generatedDay < 0 || requestIds == null) return false;
            var table = TableManager.Instance.CustomerRequest;
            var expected = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in requestIds)
            {
                var request = table.FindRequest(id);
                if (request == null) return false;
                expected.Add(request.id);
            }
            foreach (var row in state.rewards)
                if (row == null || row.amount < 0 || !expected.Remove(row.requestId)) return false;
            return expected.Count == 0;
        }

        public static float SaleMultiplier(GameSaveController save)
            => save != null ? save.SaleRewardMultiplier : 1f;

        private static int Clamp(long value) => (int)Math.Max(0, Math.Min(int.MaxValue, value));
        private static int Scale(int value, float ratio)
            => (int)Math.Max(0, Math.Min(int.MaxValue, Math.Round((double)value * ratio, MidpointRounding.ToEven)));
    }
}
