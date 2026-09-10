using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Charges colony prisoners meal tickets whenever they eat.
    /// Cost comes from <see cref="MarketPriceHelper.TicketCost(Thing, Pawn)"/>; balances support
    /// fractional tickets. When tickets run out and meal credit is enabled (default) the meal is
    /// still eaten but the balance goes negative (debt); wages and other income repay the debt
    /// first. When credit is disabled the meal is refused entirely (food is kept, not wasted).
    /// Riot/escaping prisoners skip the check when ignoreDuringRiot is on.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    public static class Harmony_Ingest
    {
        // 单价在前缀里算出，供扣款和"没钱不吃"的拦截判断共用；份数先按饥饿度估算，
        // 进食结束后用 stackCount 的实际差值修正（见 Postfix）。
        private class IngestEvent
        {
            public float unitCost;
            public int units = 1;      // 估算份数（用于前置拦截判断；尸体等堆叠不变的场景也用它计费）
            public int stackBefore;    // 进食前的堆叠数，用于事后精确算份数
            public bool consumable;    // 前缀时可正常食用（未被销毁、未在燃烧）
            public bool applicable;
            public float TicketCost => unitCost * units;
        }

        private static readonly ConditionalWeakTable<Thing, IngestEvent> pending = new ConditionalWeakTable<Thing, IngestEvent>();

        // 赊账关闭时"没钱不吃"的提示节流：同一囚犯每 0.1 天最多弹一次消息
        // （进食尝试会随饥饿周期反复发生，不节流会刷屏）
        private static readonly Dictionary<int, int> lastBlockedMsgTick = new Dictionary<int, int>();
        private const int BlockedMsgIntervalTicks = 6000;

        /// <summary>读档/换存档时清空进程级节流表（thingIDNumber 跨存档会复用，不能留着）。</summary>
        public static void ResetTransientState() => lastBlockedMsgTick.Clear();

        [HarmonyPrefix]
        public static bool Prefix(Thing __instance, Pawn ingester)
        {
            var evt = new IngestEvent();
            evt.applicable = ShouldCharge(ingester, __instance, out evt.unitCost);
            if (!evt.applicable)
            {
                // 防御性清理：若上一次 Ingested 异常中断留下了条目，别让这次误扣费
                pending.Remove(__instance);
                return true;
            }
            evt.units = CountUnits.Estimate(ingester, __instance);
            evt.stackBefore = __instance.stackCount;
            evt.consumable = !__instance.Destroyed && __instance.IngestibleNow;

            // 赊账关闭且买不起这顿饭：拦下进食。Thing.Ingested 内部才销毁食物，
            // 前缀直接跳过 = 食物保留、不加营养，囚犯只是吃不到（不会浪费饭）。
            // 儿童可动用父母的饭票（可用余额 = 自己 + 在押父母），因此用可用余额判断。
            if (!PrisonersPayToEat2Mod.Settings.allowMealDebt)
            {
                var mgr = PrisonersPayToEat2Manager.Current;
                float available = mgr?.AvailableBalance(ingester) ?? 0f;
                if (available < evt.TicketCost)
                {
                    NotifyBlocked(ingester, evt.TicketCost, available);
                    return false;
                }
            }

            pending.Remove(__instance);
            pending.Add(__instance, evt);
            return true;
        }

        /// <summary>
        /// 按实际吃掉的份数计费。份数优先用进食前后的 stackCount 差值反推（生食 / 肉干等
        /// 一次会吃多份，饿极了可能十几份）；差值取不到时退回前缀的估算值——
        /// 尸体每次只吃掉一个部位且 numTaken = 0、堆叠不变，这种按 1 份计（与原行为一致）。
        /// 不用 hook IngestedCalculateAmounts：它是 protected 且被 Corpse / Plant 重写。
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix(Thing __instance, Pawn ingester)
        {
            if (!pending.TryGetValue(__instance, out var evt) || !evt.applicable)
            {
                pending.Remove(__instance);
                return;
            }
            pending.Remove(__instance);
            if (!evt.consumable) return; // 前缀时已销毁/在燃烧：原版报错空返，没有消耗任何食物

            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null) return;

            int units;
            if (__instance.Destroyed) units = Mathf.Max(1, evt.stackBefore);          // 整叠 / 整个被吃光
            else if (evt.stackBefore > __instance.stackCount) units = evt.stackBefore - __instance.stackCount;
            else units = evt.units;                                                  // 堆叠未变（尸体等）

            float cost = evt.unitCost * units;
            float available = mgr.AvailableBalance(ingester);

            if (available >= cost)
            {
                mgr.PayWithSupport(ingester, cost);
                if (ingester.Spawned)
                    MoteMaker.ThrowText(ingester.DrawPos, ingester.Map, "-" + cost.ToString("0.##"), new Color(1f, 0.7f, 0.3f));
                if (PrisonersPayToEat2Mod.Settings.logVerbose)
                    Log.Message($"[PPTE2] {ingester.LabelShortCap} ate {units}x {__instance.LabelNoCount} for {cost:0.##} tickets. balance剩={mgr.Balance(ingester):0.##}");
                return;
            }

            // 余额不足但赊账开启：吃进嘴里，余额扣成负数（欠款），工资等收入优先还债。
            // 赊账关闭时余额不足已被前缀拦截，走不到这里。
            if (PrisonersPayToEat2Mod.Settings.allowMealDebt)
            {
                mgr.PayWithSupport(ingester, cost);
                float debt = cost - available;
                if (ingester.Spawned)
                    MoteMaker.ThrowText(ingester.DrawPos, ingester.Map, "+" + debt.ToString("0.##") + "!", Color.red);
                if (PrisonersPayToEat2Mod.Settings.logVerbose)
                    Log.Warning($"[PPTE2] {ingester.LabelShortCap} ate {units}x {__instance.LabelNoCount} on credit: need={cost:0.##} paid={available:0.##} debt={debt:0.##}");
                Messages.Message("PPTE2_MealOnCredit".Translate(ingester.LabelShortCap,
                        __instance.LabelNoCount, debt.ToString("0.##"), PPTEName.Ticket),
                    ingester, MessageTypeDefOf.RejectInput);
                // 欠账压力（心情 -5；同 def 不叠加，只刷新持续时间）
                var debtThought = PrisonersPayToEat2Manager.GetThought("PPTE2_MealDebt");
                if (debtThought != null)
                    ingester.needs?.mood?.thoughts?.memories?.TryGainMemory(debtThought);
                return;
            }

            // 赊账关闭、前缀放行但实际份数超出估算（理论上不会发生：估算与原版取份数的
            // 算法一致）。食物已经吃掉了，这里仍按实际份数扣款，避免变成白吃。
            mgr.PayWithSupport(ingester, cost);
            Log.Warning($"[PPTE2] {ingester.LabelShortCap} ate {units}x {__instance.LabelNoCount} for {cost:0.##} tickets but could only cover {available:0.##} (meal credit disabled); balance can go negative.");
        }

        /// <summary>赊账关闭且余额不足时的拒绝提示（带节流）。</summary>
        private static void NotifyBlocked(Pawn ingester, float cost, float balance)
        {
            if (PrisonersPayToEat2Mod.Settings.logVerbose)
                Log.Warning($"[PPTE2] {ingester.LabelShortCap} couldn't eat: need={cost:0.##} has={balance:0.##} (meal credit disabled, refused)");

            int now = Find.TickManager.TicksGame;
            if (lastBlockedMsgTick.TryGetValue(ingester.thingIDNumber, out int last)
                && now - last < BlockedMsgIntervalTicks) return;
            lastBlockedMsgTick[ingester.thingIDNumber] = now;

            Messages.Message("PPTE2_NoTicketsBlocked".Translate(ingester.LabelShortCap,
                    cost.ToString("0.##"), PPTEName.Ticket, balance.ToString("0.##")),
                ingester, MessageTypeDefOf.RejectInput);
        }

        private static bool ShouldCharge(Pawn ingester, Thing food, out float ticketCost)
        {
            ticketCost = 0f;
            if (ingester == null || food == null) return false;
            if (!ingester.IsPrisonerOfColony)
            {
                // 顺带清理拦截提示的节流记录（释放/招募/越狱后不再需要）
                lastBlockedMsgTick.Remove(ingester.thingIDNumber);
                return false;
            }
            var s = PrisonersPayToEat2Mod.Settings;
            if (s.ignoreDuringRiot && (ingester.MentalStateDef != null || InPrisonBreak(ingester)))
                return false;
            if (!IsChargeableFood(food.def)) return false;

            ticketCost = MarketPriceHelper.TicketCost(food, ingester);
            return true;
        }

        /// <summary>
        /// 只对"能提供营养的可摄入物"收费：啤酒/仙馔等含营养的照收，
        /// 而魔鬼素、精神茶、醒神药等营养为 0 的药品不该被当成饭收费
        /// （与设置页食物定价列表的过滤条件一致）。
        /// </summary>
        internal static bool IsChargeableFood(ThingDef def)
            => def != null && def.ingestible != null && def.IsNutritionGivingIngestible;

        internal static bool InPrisonBreak(Pawn p)
        {
            if (p?.mindState == null || p.Map == null) return false;
            var lord = p.Map.lordManager?.LordOf(p); // escape lords carry a LordJob named *PrisonBreak*
            return lord != null && lord.LordJob != null
                && lord.LordJob.GetType().Name.Contains("PrisonBreak");
        }
    }

    /// <summary>
    /// 估算一次进食会吃掉几份。逻辑与原版 <c>Thing.IngestedCalculateAmounts</c> 一致
    /// （<c>CeilToInt(饥饿度 / 单份营养)</c>，再被 stackCount 和 maxNumToIngestAtOnce 夹住）。
    /// 正餐的 maxNumToIngestAtOnce = 1，生食 / 肉干等没有该限制，所以份数可能远大于 1。
    /// 仅用于"买不买得起"的前置判断；实际扣费以后缀里 stackCount 的差值为准。
    /// </summary>
    internal static class CountUnits
    {
        public static int Estimate(Pawn eater, Thing food)
        {
            if (eater == null || food == null || food.def == null) return 1;

            float nutritionPerUnit = FoodUtility.NutritionForEater(eater, food);
            if (nutritionPerUnit <= 0.0001f) return 1;

            float wanted = eater.needs?.food?.NutritionWanted
                           ?? (food.GetStatValue(StatDefOf.Nutrition) * food.stackCount);
            var job = eater.jobs?.curJob;
            if (job != null)
            {
                if (job.ingestTotalCount)
                    wanted = food.GetStatValue(StatDefOf.Nutrition) * food.stackCount;
                else if (job.overeat)
                    wanted = Mathf.Max(wanted, 0.75f);
            }

            int units = Mathf.CeilToInt(wanted / nutritionPerUnit);
            units = Mathf.Min(units, food.stackCount);
            int cap = food.def.ingestible?.maxNumToIngestAtOnce ?? 0;
            if (cap > 0) units = Mathf.Min(units, cap);
            return Mathf.Max(units, 1);
        }
    }
}