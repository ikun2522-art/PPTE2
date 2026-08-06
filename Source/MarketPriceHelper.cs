using RimWorld;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Converts a food item into the ticket cost a prisoner must pay to eat it.
    /// Cost = market value (silver) * silverToTicketRate * global foodPriceMultiplier * prisoner.foodMultiplier
    /// Supports fractional tickets (e.g. 0.01) — minimum 0.01 per meal.
    /// </summary>
    public static class MarketPriceHelper
    {
        public const float MinTicketCost = 0.01f;

        /// <summary>Approximate market value of a stack of food, in silver.</summary>
        public static float FoodMarketValue(Thing food)
        {
            if (food == null || food.def == null) return 0f;
            float per = food.def.BaseMarketValue;
            // Nutrient paste dispenser meals have no intrinsic def market value; price by nutrition
            if (per <= 0.01f && food.def.ingestible != null)
            {
                per = food.def.ingestible.CachedNutrition * 10f;
            }
            return per;
        }

        public static float TicketCost(Thing food, Pawn prisoner)
        {
            if (food == null) return 0f;
            var s = PrisonersPayToEat2Mod.Settings;
            var mgr = PrisonersPayToEat2Manager.Current;

            // 1) per-food override (absolute ticket price) takes top priority
            if (s.customFoodPrices != null && s.customFoodPrices.TryGetValue(food.def.defName, out float custom))
            {
                float overrideCost = custom * mgr.EffectiveFoodMultiplier(prisoner);
                return UnityEngine.Mathf.Max(overrideCost, MinTicketCost);
            }

            // 2) fallback: market-value formula
            float perItem = FoodMarketValue(food);
            float ticketPerSilver = s.silverToTicketRate;
            float formulaCost = perItem * ticketPerSilver * s.foodPriceMultiplier * mgr.EffectiveFoodMultiplier(prisoner);
            return UnityEngine.Mathf.Max(formulaCost, MinTicketCost);
        }
    }
}