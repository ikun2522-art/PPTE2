using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>账单工人限制模式（在 vanilla 的指定工人/仅奴隶等之上叠加）：任何人 / 仅囚犯 / 仅殖民者与奴隶。</summary>
    public enum BillWorkerMode { Anyone = 0, PrisonersOnly = 1, ColonistsOnly = 2 }

    /// <summary>
    /// 账单"仅囚犯"功能（仅内置劳工系统启用时出现）：
    ///  - 存储：瞬态 Dictionary&lt;Bill, BillWorkerMode&gt;；持久化走 Bill.ExposeData postfix，
    ///    在账单自身的 scribe 节点里读写 PPTE2_workerMode（postfix 执行时节点尚未关闭）。
    ///  - 执行：Bill.PawnAllowedToStartAnew postfix 按模式否决。
    ///  - UI：账单配置窗口的工人下拉菜单末尾追加两个可切换条目（激活时带 ✓），
    ///    不动对话框布局，零冲突风险。
    /// </summary>
    public static class BillWorkerModeUtility
    {
        private static readonly Dictionary<Bill, BillWorkerMode> Modes = new Dictionary<Bill, BillWorkerMode>();

        public static BillWorkerMode Get(Bill bill)
        {
            if (bill == null) return BillWorkerMode.Anyone;
            return Modes.TryGetValue(bill, out var m) ? m : BillWorkerMode.Anyone;
        }

        public static void Set(Bill bill, BillWorkerMode mode)
        {
            if (bill == null) return;
            if (mode == BillWorkerMode.Anyone) Modes.Remove(bill);
            else Modes[bill] = mode;
        }

        /// <summary>读档后清理已被删除的账单，防引用滞留。</summary>
        public static void Prune()
        {
            if (Modes.Count == 0) return;
            var stale = new List<Bill>();
            foreach (var kv in Modes)
            {
                var bill = kv.Key;
                if (bill == null || bill.billStack == null) { stale.Add(bill); continue; }
                if (bill.billStack.billGiver is Thing t && t.Destroyed) { stale.Add(bill); continue; }
                if (!bill.billStack.Bills.Contains(bill)) stale.Add(bill);
            }
            foreach (var b in stale) Modes.Remove(b);
        }
    }

    [HarmonyPatch(typeof(Bill), nameof(Bill.ExposeData))]
    public static class Patch_BillExposeData
    {
        [HarmonyPostfix]
        public static void Postfix(Bill __instance)
        {
            var mode = BillWorkerModeUtility.Get(__instance);
            Scribe_Values.Look(ref mode, "PPTE2_workerMode", BillWorkerMode.Anyone);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                BillWorkerModeUtility.Set(__instance, mode);
        }
    }

    [HarmonyPatch(typeof(Bill), nameof(Bill.PawnAllowedToStartAnew))]
    public static class Patch_BillPawnAllowed
    {
        [HarmonyPostfix]
        public static void Postfix(Bill __instance, Pawn p, ref bool __result)
        {
            if (!__result || !PrisonerWorkSystem.Active) return;
            switch (BillWorkerModeUtility.Get(__instance))
            {
                case BillWorkerMode.PrisonersOnly:
                    if (!p.IsPrisonerOfColony || !PrisonerWorkSystem.IsWorkEnabled(p)) __result = false;
                    break;
                case BillWorkerMode.ColonistsOnly:
                    if (p.IsPrisonerOfColony) __result = false;
                    break;
            }
        }
    }

    [HarmonyPatch(typeof(Dialog_BillConfig), "GeneratePawnRestrictionOptions")]
    public static class Patch_BillConfigMenu
    {
        private static readonly AccessTools.FieldRef<Dialog_BillConfig, Bill_Production> BillRef =
            AccessTools.FieldRefAccess<Dialog_BillConfig, Bill_Production>("bill");

        [HarmonyPostfix]
        public static IEnumerable<Widgets.DropdownMenuElement<Pawn>> Postfix(
            IEnumerable<Widgets.DropdownMenuElement<Pawn>> __result, Dialog_BillConfig __instance)
        {
            foreach (var e in __result) yield return e;
            if (!PrisonerWorkSystem.Active) yield break;
            var bill = BillRef(__instance);
            if (bill == null) yield break;

            bool prisoners = BillWorkerModeUtility.Get(bill) == BillWorkerMode.PrisonersOnly;
            bool colonists = BillWorkerModeUtility.Get(bill) == BillWorkerMode.ColonistsOnly;
            yield return new Widgets.DropdownMenuElement<Pawn>
            {
                option = new FloatMenuOption(
                    (prisoners ? "✓ " : "") + "PPTE2_BillPrisonersOnly".Translate(),
                    delegate
                    {
                        BillWorkerModeUtility.Set(bill,
                            prisoners ? BillWorkerMode.Anyone : BillWorkerMode.PrisonersOnly);
                    }),
                payload = null
            };
            yield return new Widgets.DropdownMenuElement<Pawn>
            {
                option = new FloatMenuOption(
                    (colonists ? "✓ " : "") + "PPTE2_BillColonistsOnly".Translate(),
                    delegate
                    {
                        BillWorkerModeUtility.Set(bill,
                            colonists ? BillWorkerMode.Anyone : BillWorkerMode.ColonistsOnly);
                    }),
                payload = null
            };
        }
    }
}
