using HarmonyLib;
using RimWorld;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Blocks colony prisoners (meal-credit disabled) from being assigned a food source they
    /// cannot afford — at the <see cref="FoodUtility.TryFindBestFoodSourceFor"/> stage, before
    /// any food is picked up or dispensed.
    ///
    /// Why this exists in addition to <see cref="Harmony_Ingest"/>: the Ingested prefix can only
    /// refuse a meal once the pawn is already chewing it. For a nutrient-paste dispenser the
    /// hopper feedstock is consumed inside <c>Building_NutrientPasteDispenser.TryDispenseFood</c>,
    /// which runs in the "take meal from dispenser" toil — well before Ingested. So a penniless
    /// prisoner would walk to the dispenser, waste raw ingredients, chew the whole time, then get
    /// blocked at Ingested and loop forever. By nulling the food source here the pawn never gets
    /// the Ingest job at all: no walk, no dispense, no wasted feedstock, no loop.
    /// The Ingested prefix stays as a safety net for paths that bypass this search (food handed
    /// to the prisoner, already-carried food, etc.).
    /// </summary>
    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.TryFindBestFoodSourceFor))]
    public static class Harmony_FoodSearch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn eater, ref Thing foodSource, ref ThingDef foodDef, ref bool __result)
        {
            if (!__result || foodSource == null) return;
            if (!ShouldBlock(eater, foodDef, foodSource)) return;

            // Prisoner can't afford this food and credit is disabled — veto the selection.
            foodSource = null;
            foodDef = null;
            __result = false;
        }

        /// <summary>
        /// True when the eater is a colony prisoner who must pay for this meal but can't, and
        /// meal-credit (debt) is off. Mirrors the exemption logic in
        /// <see cref="Harmony_Ingest.ShouldCharge"/> (riot/prison-break skip, ingestible check)
        /// so the two layers agree on who is chargeable.
        /// </summary>
        private static bool ShouldBlock(Pawn eater, ThingDef foodDef, Thing foodSource)
        {
            if (eater == null || !eater.IsPrisonerOfColony) return false;
            var s = PrisonersPayToEat2Mod.Settings;
            // Debt enabled → the Ingested layer lets them eat on credit; don't starve them here.
            if (s.allowMealDebt) return false;
            // Rioting / escaping prisoners eat for free (same as Harmony_Ingest.ShouldCharge).
            if (s.ignoreDuringRiot && (eater.MentalStateDef != null || Harmony_Ingest.InPrisonBreak(eater)))
                return false;

            // Resolve the def of what will actually be eaten. For a dispenser the Thing is the
            // building; the meal def comes from the out-param (MealNutrientPaste). For spawned
            // food the def is on the thing itself and foodDef matches it.
            ThingDef def = foodDef;
            if (def == null)
            {
                if (foodSource is Building_NutrientPasteDispenser disp)
                    def = disp.DispensableDef;
                else
                    def = foodSource.def;
            }
            if (def == null || def.ingestible == null) return false;
            // 只对能提供营养的食物收费（与 Harmony_Ingest.ShouldCharge 保持一致）
            if (!Harmony_Ingest.IsChargeableFood(def)) return false;

            // 按实际会吃掉的份数估算（生食/肉干会一次吃多份），否则这里放行、
            // Ingested 前缀再拦下，囚犯就会走到食物前空嚼一轮又吃不到（正是本补丁要避免的循环）。
            float cost = MarketPriceHelper.TicketCost(def, eater) * CountUnits.Estimate(eater, foodSource);
            float available = PrisonersPayToEat2Manager.Current?.AvailableBalance(eater) ?? 0f;
            return available < cost;
        }
    }
}
