using HarmonyLib;
using RimWorld;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// 着装方案过滤（仅内置劳工系统启用时生效）：原版囚犯穿衣（JobGiver_PrisonerGetDressed）
    /// 只认牢房地板上的衣服，完全不看 ApparelPolicy。postfix 拦截两个找衣方法的返回值，
    /// 不符合当前着装方案的衣服不穿。囚犯的 outfits tracker 由捕获时的补丁补建，
    /// 默认方案 = outfitDatabase.DefaultOutfit()。
    /// </summary>
    [HarmonyPatch]
    public static class Patch_PrisonerRespectOutfits
    {
        [HarmonyTargetMethods]
        public static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(JobGiver_PrisonerGetDressed), "FindGarmentCoveringPart");
            yield return AccessTools.Method(typeof(JobGiver_PrisonerGetDressed), "FindGarmentSatisfyingTitleRequirement");
        }

        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref Apparel __result)
        {
            if (__result == null || !PrisonerWorkSystem.Active) return;
            var policy = pawn?.outfits?.CurrentApparelPolicy;
            if (policy == null) return;
            if (!policy.filter.Allows(__result)) __result = null;
        }
    }
}
