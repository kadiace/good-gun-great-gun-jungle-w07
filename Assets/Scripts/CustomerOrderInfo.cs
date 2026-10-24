using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // CustomerOrderData 테이블을 읽는 로직. 호출측은 TableManager.Instance.CustomerOrder로 테이블을 받아 확장 메서드로 읽는다.
    public static class CustomerOrderInfo
    {
        public static CustomerOrderData.Day FindDay(this CustomerOrderData table, int day)
        {
            foreach (var row in table?.days ?? Array.Empty<CustomerOrderData.Day>()) if (row != null && row.day == day) return row;
            return null;
        }
        public static CustomerOrderData.Guest FindGuest(this CustomerOrderData table, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var row in table?.guests ?? Array.Empty<CustomerOrderData.Guest>()) if (row != null && row.id == id) return row;
            return null;
        }
        public static CustomerOrderData.Pool FindPool(this CustomerOrderData table, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var row in table?.pools ?? Array.Empty<CustomerOrderData.Pool>()) if (row != null && row.id == id) return row;
            return null;
        }
        public static CustomerOrderData.PoolRequest FindEntry(this CustomerOrderData.Pool pool, string requestId)
        {
            var requests = TableManager.Instance.CustomerRequest;
            string id = requests.FindRequest(requestId)?.id ?? requestId;
            foreach (var row in pool?.requests ?? Array.Empty<CustomerOrderData.PoolRequest>())
                if (row != null && (requests.FindRequest(row.id)?.id ?? row.id) == id) return row;
            return null;
        }

        /// <summary>같이 요구하면 만족할 수 없는 조합인지. 손님 풀 항목의 excludes를 양방향으로 본다.</summary>
        public static bool Conflicts(this CustomerOrderData.Pool pool, string first, string second)
        {
            var requests = TableManager.Instance.CustomerRequest;
            var a = requests.FindRequest(first); var b = requests.FindRequest(second);
            return a == null || b == null || Excludes(pool.FindEntry(a.id), b.id) || Excludes(pool.FindEntry(b.id), a.id);
        }
        private static bool Excludes(CustomerOrderData.PoolRequest entry, string id)
        {
            foreach (string excluded in entry?.excludes ?? Array.Empty<string>())
                if (TableManager.Instance.CustomerRequest.FindRequest(excluded)?.id == id) return true;
            return false;
        }

        /// <summary>
        /// 손님 표의 값을 독립적인 런타임 주문으로 복사합니다. 튜토리얼 거절 뒤 요구사항도 원본과 분리합니다.
        /// </summary>
        public static CustomerOrderData.Guest Copy(CustomerOrderData.Guest source) => source == null ? new CustomerOrderData.Guest() : new CustomerOrderData.Guest
        {
            id = source.id, name = source.name, isDifficult = source.isDifficult, endsClear = source.endsClear,
            happyLine = source.happyLine, angryLine = source.angryLine, unlockDay = source.unlockDay,
            tutorialRejectionLine = source.tutorialRejectionLine,
            tutorialFollowupRequestIds = (string[])(source.tutorialFollowupRequestIds ?? Array.Empty<string>()).Clone(),
            requestIds = (string[])(source.requestIds ?? Array.Empty<string>()).Clone(),
            lines = (string[])(source.lines ?? Array.Empty<string>()).Clone(),
        };
        public static CustomerOrderData.Day Copy(CustomerOrderData.Day source) => source == null ? new CustomerOrderData.Day() : new CustomerOrderData.Day
        {
            day = source.day, rent = source.rent, useRequestPool = source.useRequestPool, extraGenerated = source.extraGenerated,
            minRequests = source.minRequests, maxRequests = source.maxRequests,
        };

        // 로드 직후 한 번 호출한다. 요청 표가 먼저 로드되어 있어야 한다.
        /// <summary>
        /// 주문 표의 식별자와 요청 참조를 점검합니다. 튜토리얼 거절 이후의 요청도 같은 요청 표를 참조해야 합니다.
        /// </summary>
        public static void Validate(CustomerOrderData table, CustomerRequestData requests)
        {
            if (table.sale == null) table.sale = new CustomerOrderData.Sale();
            if (float.IsNaN(table.sale.difficultCustomerRatio) || float.IsInfinity(table.sale.difficultCustomerRatio) ||
                table.sale.difficultCustomerRatio < 0f) Debug.LogError("진상 손님 보상 비율을 확인하세요.");
            var days = new HashSet<int>();
            foreach (var row in table.days ?? Array.Empty<CustomerOrderData.Day>())
                if (row == null || !days.Add(row.day)) Debug.LogError("주문 표에 빈/중복 일차가 있습니다.");
            var guests = new HashSet<string>();
            foreach (var row in table.guests ?? Array.Empty<CustomerOrderData.Guest>())
            {
                if (row == null || string.IsNullOrEmpty(row.id) || !guests.Add(row.id)) { Debug.LogError("주문 표에 빈/중복 손님 ID가 있습니다."); continue; }
                if (row.lines == null || row.lines.Length == 0) Debug.LogError("주문 표 손님 대사가 없습니다: " + row.name);
                CheckRequests(requests, row.requestIds, "손님 " + row.name);
                CheckRequests(requests, row.tutorialFollowupRequestIds, "튜토리얼 후속 주문 " + row.name);
            }
            foreach (var pool in table.pools ?? Array.Empty<CustomerOrderData.Pool>())
            {
                if (pool == null || string.IsNullOrEmpty(pool.id)) { Debug.LogError("주문 표에 빈 손님 풀 ID가 있습니다."); continue; }
                foreach (var entry in pool.requests ?? Array.Empty<CustomerOrderData.PoolRequest>())
                {
                    CheckRequests(requests, new[] { entry?.id }, "손님 풀 " + pool.id);
                    CheckRequests(requests, entry?.excludes, "손님 풀 " + pool.id);
                }
            }
        }
        private static void CheckRequests(CustomerRequestData requests, string[] ids, string owner)
        {
            foreach (string id in ids ?? Array.Empty<string>())
                if (requests.FindRequest(id) == null) Debug.LogError($"주문 표 {owner}의 요청 ID가 요청 표에 없습니다: {id}");
        }
    }
}
