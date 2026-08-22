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
        // cost is computed in the prefix so the postfix can use it after the meal is consumed
        private class IngestEvent
        {
            public float ticketCost;
            public bool applicable;
        }

        private static readonly ConditionalWeakTable<Thing, IngestEvent> pending = new ConditionalWeakTable<Thing, IngestEvent>();

        // 赊账关闭时"没钱不吃"的提示节流：同一囚犯每 0.1 天最多弹一次消息
        // （进食尝试会随饥饿周期反复发生，不节流会刷屏）
        private static readonly Dictionary<int, int> lastBlockedMsgTick = new Dictionary<int, int>();
        private const int BlockedMsgIntervalTicks = 6000;

        [HarmonyPrefix]
        public static bool Prefix(Thing __instance, Pawn ingester)
        {
            var evt = new IngestEvent();
            evt.applicable = ShouldCharge(ingester, __instance, out evt.ticketCost);
            if (!evt.applicable) return true;

            // 赊账关闭且买不起这顿饭：拦下进食。Thing.Ingested 内部才销毁食物，
            // 前缀直接跳过 = 食物保留、不加营养，囚犯只是吃不到（不会浪费饭）。
            if (!PrisonersPayToEat2Mod.Settings.allowMealDebt)
            {
                var mgr = PrisonersPayToEat2Manager.Current;
                float balance = mgr?.Balance(ingester) ?? 0f;
                if (balance < evt.ticketCost)
                {
                    NotifyBlocked(ingester, evt.ticketCost, balance);
                    return false;
                }
            }

            pending.Remove(__instance);
            pending.Add(__instance, evt);
            return true;
        }

        [HarmonyPostfix]
        public static void Postfix(Thing __instance, Pawn ingester)
        {
            if (!pending.TryGetValue(__instance, out var evt) || !evt.applicable)
            {
                pending.Remove(__instance);
                return;
            }
            pending.Remove(__instance);

            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null) return;

            float cost = evt.ticketCost;
            float balance = mgr.Balance(ingester);

            if (balance >= cost)
            {
                mgr.TryPay(ingester, cost);
                if (ingester.Spawned)
                    MoteMaker.ThrowText(ingester.DrawPos, ingester.Map, "-" + cost.ToString("0.##"), new Color(1f, 0.7f, 0.3f));
                if (PrisonersPayToEat2Mod.Settings.logVerbose)
                    Log.Message($"[PPTE2] {ingester.LabelShortCap} ate {__instance.LabelNoCount} for {cost:0.##} tickets. balance剩={mgr.Balance(ingester):0.##}");
                return;
            }

            // 余额不足但赊账开启：吃进嘴里，余额扣成负数（欠款），工资等收入优先还债。
            // 赊账关闭时余额不足已被前缀拦截，走不到这里。
            if (PrisonersPayToEat2Mod.Settings.allowMealDebt)
            {
                mgr.PayAllowDebt(ingester, cost);
                float debt = cost - balance;
                if (ingester.Spawned)
                    MoteMaker.ThrowText(ingester.DrawPos, ingester.Map, "+" + debt.ToString("0.##") + "!", Color.red);
                if (PrisonersPayToEat2Mod.Settings.logVerbose)
                    Log.Warning($"[PPTE2] {ingester.LabelShortCap} ate {__instance.LabelNoCount} on credit: need={cost:0.##} paid={balance:0.##} debt={debt:0.##}");
                Messages.Message("PPTE2_MealOnCredit".Translate(ingester.LabelShortCap,
                        __instance.LabelNoCount, debt.ToString("0.##"), PPTEName.Ticket),
                    ingester, MessageTypeDefOf.RejectInput);
                // 欠账压力（心情 -5；同 def 不叠加，只刷新持续时间）
                var debtThought = PrisonersPayToEat2Manager.GetThought("PPTE2_MealDebt");
                if (debtThought != null)
                    ingester.needs?.mood?.thoughts?.memories?.TryGainMemory(debtThought);
            }
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
            if (food.def == null || food.def.ingestible == null) return false;

            ticketCost = MarketPriceHelper.TicketCost(food, ingester);
            return true;
        }

        private static bool InPrisonBreak(Pawn p)
        {
            if (p?.mindState == null || p.Map == null) return false;
            var lord = p.Map.lordManager?.LordOf(p); // escape lords carry a LordJob named *PrisonBreak*
            return lord != null && lord.LordJob != null
                && lord.LordJob.GetType().Name.Contains("PrisonBreak");
        }
    }
}