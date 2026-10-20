using System;
namespace WeaponWorkshop
{
    [Serializable] public sealed class RetirementData
    {
        [Serializable] public sealed class Rule
        {
            public int cost, satisfiedSales, lifetimeRevenue;
            public bool revenueIncludesDissatisfied, legacySaveCostOnly;
        }
        public Rule rule;
    }
}
