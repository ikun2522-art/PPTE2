using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Adds a single meal-ticket control to a selected colony prisoner's gizmo row: one button that
    /// opens the prisoner ticket menu (give/take tickets, individual settings, ransom approval).
    /// All mod-specific gizmos live inside that menu instead of cluttering the gizmo row.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Harmony_UI
    {
        [HarmonyPostfix]
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var g in __result) yield return g;

            if (!ShouldShow(__instance)) yield break;

            yield return PrisonerMenuGizmo(__instance);
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

        private static Gizmo PrisonerMenuGizmo(Pawn p)
        {
            // mark the button when a ransom request awaits the player's decision
            var d = PrisonersPayToEat2Manager.Current?.DataFor(p);
            bool ransomPending = d != null && d.ransomRequested && PrisonersPayToEat2Mod.Settings.enableRansom;
            return new Command_Action
            {
                defaultLabel = "PPTE2_GizmoMenu".Translate() + (ransomPending ? " (!)" : ""),
                defaultDesc = "PPTE2_GizmoMenuDesc".Translate(),
                icon = TexButton.Info,
                action = () => Find.WindowStack.Add(new Window_PrisonerMenu(p)),
                groupKey = 8978100,
            };
        }
    }
}
