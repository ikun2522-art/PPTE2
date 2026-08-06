using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Appends meal-ticket controls to a selected colony prisoner's gizmo row:
    /// give tickets, take tickets, configure prisoner, and a read-only balance card.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Harmony_UI
    {
        [HarmonyPostfix]
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var g in __result) yield return g;

            if (!ShouldShow(__instance)) yield break;

            float balance = PrisonersPayToEat2Manager.Current?.Balance(__instance) ?? 0f;

            yield return GiveTicketGizmo(__instance);
            yield return TakeTicketGizmo(__instance, balance);
            yield return PerPrisonerConfigGizmo(__instance);
            yield return new Gizmo_TicketBalance(__instance);
        }

        private static bool ShouldShow(Pawn p)
        {
            if (p == null) return false;
            if (!p.IsPrisonerOfColony) return false;
            if (Find.Selector == null) return false;
            foreach (var sel in Find.Selector.SelectedObjects)
            {
                if (sel == p) return true;
            }
            return false;
        }

        private static Gizmo GiveTicketGizmo(Pawn p)
        {
            return new Command_Action
            {
                defaultLabel = "PPTE2_GizmoGive".Translate(PPTEName.Ticket),
                defaultDesc = "PPTE2_GizmoGiveDesc".Translate(PPTEName.Ticket),
                icon = TexButton.Plus,
                action = () => Find.WindowStack.Add(new Dialog_GiveTickets(p, true)),
                groupKey = 8978101,
            };
        }

        private static Gizmo TakeTicketGizmo(Pawn p, float balance)
        {
            var gizmo = new Command_Action
            {
                defaultLabel = "PPTE2_GizmoTake".Translate(PPTEName.Ticket),
                defaultDesc = "PPTE2_GizmoTakeDesc".Translate(PPTEName.Ticket),
                icon = TexButton.Minus,
                action = () => Find.WindowStack.Add(new Dialog_GiveTickets(p, false)),
                groupKey = 8978102,
            };
            if (balance <= 0)
            {
                gizmo.Disabled = true;
                gizmo.disabledReason = "PPTE2_NoTicketsToTake".Translate(PPTEName.Ticket);
            }
            return gizmo;
        }

        private static Gizmo PerPrisonerConfigGizmo(Pawn p)
        {
            return new Command_Action
            {
                defaultLabel = "PPTE2_GizmoConfig".Translate(),
                defaultDesc = "PPTE2_GizmoConfigDesc".Translate(),
                icon = TexButton.Rename,
                action = () => Find.WindowStack.Add(new Window_PrisonerConfig(p)),
                groupKey = 8978103,
            };
        }
    }
}