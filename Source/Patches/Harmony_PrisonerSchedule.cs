using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// 时间表与活动区补丁（仅内置劳工系统启用时生效；装 Prison Labor 时由它自己处理）：
    ///
    /// 1. Pawn_TimetableTracker.CurrentAssignment —— 原版对囚犯硬编码返回 Anything
    ///    ("!pawn.IsColonist || pawn.IsPrisonerOfColony")。postfix 让启用系统的囚犯
    ///    真正读取时间表格子（tracker 由捕获时的补丁补建）。JobGiver_Work.GetPriority
    ///    读这个属性，所以 Work/Anything 时段允许工作、Joy/Sleep 时段优先级自动降低。
    /// 2. Pawn_PlayerSettings.RespectsAllowedArea —— 原版对非玩家派系返回 false，
    ///    导致 EffectiveAreaRestrictionInPawnCurrentMap 为 null、AI 不执行活动区。
    ///    postfix 对本殖民地囚犯返回 true，寻路/工作扫描的区域限制随之自动生效。
    /// </summary>
    [HarmonyPatch(typeof(Pawn_TimetableTracker), nameof(Pawn_TimetableTracker.CurrentAssignment), MethodType.Getter)]
    public static class Patch_PrisonerTimetable
    {
        private static readonly AccessTools.FieldRef<Pawn_TimetableTracker, Pawn> PawnRef =
            AccessTools.FieldRefAccess<Pawn_TimetableTracker, Pawn>("pawn");

        [HarmonyPostfix]
        public static void Postfix(Pawn_TimetableTracker __instance, ref TimeAssignmentDef __result)
        {
            if (!PrisonerWorkSystem.Active) return;
            var pawn = PawnRef(__instance);
            if (pawn == null || !pawn.IsPrisonerOfColony) return;
            var times = __instance.times;
            if (times == null || times.Count != 24) return;
            __result = times[GenLocalDate.HourOfDay(pawn)] ?? TimeAssignmentDefOf.Anything;
        }
    }

    [HarmonyPatch(typeof(Pawn_PlayerSettings), nameof(Pawn_PlayerSettings.RespectsAllowedArea), MethodType.Getter)]
    public static class Patch_PrisonerRespectsAllowedArea
    {
        private static readonly AccessTools.FieldRef<Pawn_PlayerSettings, Pawn> PawnRef =
            AccessTools.FieldRefAccess<Pawn_PlayerSettings, Pawn>("pawn");

        [HarmonyPostfix]
        public static void Postfix(Pawn_PlayerSettings __instance, ref bool __result)
        {
            if (__result || !PrisonerWorkSystem.Active) return;
            var pawn = PawnRef(__instance);
            if (pawn == null || !pawn.IsPrisonerOfColony) return;
            if (!__instance.SupportsAllowedAreas) return;
            if (pawn.GetLord() != null) return; // 越狱等 lord 状态下不受活动区约束（与原版一致）
            __result = true;
        }
    }
}
