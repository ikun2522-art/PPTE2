using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// 囚犯劳作区的两块补丁：
    ///
    /// 1. AreaManager 注册（PatchAll 自动应用）：新地图 AddStartingAreas 与老存档
    ///    ExposeData(PostLoadInit) 各补一次 Area_PrisonerLabor（照 vanilla Biotech
    ///    PollutionClear 的补区模式）。
    ///
    /// 2. 殖民者避让（手动 Apply，从 Mod 构造器调用）：劳作区语义是"禁止殖民者在此工作"。
    ///    HasJobOnThing/HasJobOnCell 是虚方法且各 WorkGiver 大量 override，补丁基类拦不到
    ///    override，所以在启动时枚举 WorkGiver_Scanner 的全部子类，给每个自己声明的
    ///    HasJobOnThing/HasJobOnCell 打 postfix（MOD 新增 WorkGiver 同样覆盖）。
    ///    囚犯（派系非玩家）与机械体不受影响。
    /// </summary>
    [HarmonyPatch(typeof(AreaManager), nameof(AreaManager.AddStartingAreas))]
    public static class Patch_AreaManager_StartingAreas
    {
        [HarmonyPostfix]
        public static void Postfix(AreaManager __instance) => LaborAreaColonistBlock.EnsureArea(__instance);
    }

    [HarmonyPatch(typeof(AreaManager), nameof(AreaManager.ExposeData))]
    public static class Patch_AreaManager_ExposeData
    {
        [HarmonyPostfix]
        public static void Postfix(AreaManager __instance)
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit) LaborAreaColonistBlock.EnsureArea(__instance);
        }
    }

    public static class LaborAreaColonistBlock
    {
        public static void EnsureArea(AreaManager mgr)
        {
            if (mgr == null) return;
            if (mgr.Get<Area_PrisonerLabor>() != null) return;
            mgr.AllAreas.Add(new Area_PrisonerLabor(mgr));
        }

        /// <summary>给所有 WorkGiver_Scanner 自声明的 HasJobOnThing/HasJobOnCell 打 postfix。</summary>
        public static void Apply(Harmony harmony)
        {
            var postfixThing = new HarmonyMethod(typeof(LaborAreaColonistBlock), nameof(HasJobOnThingPostfix));
            var postfixCell = new HarmonyMethod(typeof(LaborAreaColonistBlock), nameof(HasJobOnCellPostfix));
            var baseType = typeof(WorkGiver_Scanner);
            foreach (var type in GenTypes.AllTypes)
            {
                if (type == null || !baseType.IsAssignableFrom(type)) continue;
                PatchIfDeclared(harmony, type, "HasJobOnThing", postfixThing);
                PatchIfDeclared(harmony, type, "HasJobOnCell", postfixCell);
            }
        }

        private static void PatchIfDeclared(Harmony harmony, Type type, string name, HarmonyMethod postfix)
        {
            MethodInfo m = type.GetMethod(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (m == null) return;
            try { harmony.Patch(m, postfix: postfix); }
            catch (Exception ex) { Log.Warning($"[PPTE2] labor-area patch on {type.Name}.{name} failed: {ex.Message}"); }
        }

        public static void HasJobOnThingPostfix(Pawn pawn, Thing t, ref bool __result)
        {
            if (t != null) BlockIfLaborArea(pawn, t.PositionHeld, ref __result);
        }

        public static void HasJobOnCellPostfix(Pawn pawn, IntVec3 c, ref bool __result)
        {
            BlockIfLaborArea(pawn, c, ref __result);
        }

        private static void BlockIfLaborArea(Pawn pawn, IntVec3 c, ref bool result)
        {
            if (!result || !PrisonerWorkSystem.Active) return;
            if (pawn == null || !c.IsValid) return;
            // 只挡玩家派系的自由工作者（殖民者/奴隶）；囚犯派系非玩家，天然不受影响
            if (pawn.Faction != Faction.OfPlayer || pawn.IsPrisonerOfColony) return;
            if (pawn.RaceProps == null || pawn.RaceProps.IsMechanoid) return;
            var map = pawn.MapHeld;
            if (map == null) return;
            var area = map.areaManager.Get<Area_PrisonerLabor>();
            if (area != null && area[c]) result = false;
        }
    }
}
