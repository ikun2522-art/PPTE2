using HarmonyLib;
using RimWorld;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Charges colony prisoners meal tickets whenever they eat.
    /// Cost comes from <see cref="MarketPriceHelper.TicketCost(Thing, Pawn)"/>; balances support
    /// fractional tickets. When tickets run out the meal is still eaten (avoid starving to death
    /// unnoticed) but a deficit message is shown. Riot/escaping prisoners skip the check when
    /// ignoreDuringRiot is on.
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

        [HarmonyPrefix]
        public static void Prefix(Thing __instance, Pawn ingester)
        {
            var evt = new IngestEvent();
            evt.applicable = ShouldCharge(ingester, __instance, out evt.ticketCost);
            pending.Remove(__instance);
            pending.Add(__instance, evt);
        }

        [HarmonyPostfix]
        public static void Postfix(Thing __instance, Pawn ingester, float __result)
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
            }
            else
            {
                float partial = balance;
                if (partial > 0f)
                {
                    mgr.TryPay(ingester, partial);
                }
                if (ingester.Spawned)
                    MoteMaker.ThrowText(ingester.DrawPos, ingester.Map, "+" + (cost - partial).ToString("0.##") + "!", Color.red);
                if (PrisonersPayToEat2Mod.Settings.logVerbose)
                    Log.Warning($"[PPTE2] {ingester.LabelShortCap} lacked tickets: need={cost:0.##} has={partial:0.##} (ate anyway, no further penalty this time)");
                Messages.Message("PPTE2_NoTickets".Translate(ingester.LabelShortCap,
                        cost.ToString("0.##"), partial.ToString("0.##"), PPTEName.Ticket),
                    ingester, MessageTypeDefOf.RejectInput);
            }
        }

        private static bool ShouldCharge(Pawn ingester, Thing food, out float ticketCost)
        {
            ticketCost = 0f;
            if (ingester == null || food == null) return false;
            if (!ingester.IsPrisonerOfColony) return false;
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