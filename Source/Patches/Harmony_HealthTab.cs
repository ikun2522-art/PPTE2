using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Shows a colony prisoner's meal-ticket balance in a thin strip at the top of the health tab.
    /// The pawn health card is shifted down by the strip height so nothing is covered; the strip
    /// also hints when a ransom request is awaiting the player's decision.
    /// </summary>
    [HarmonyPatch(typeof(HealthCardUtility), nameof(HealthCardUtility.DrawPawnHealthCard))]
    public static class Harmony_HealthTab
    {
        private const float StripHeight = 24f;

        [HarmonyPrefix]
        public static void Prefix(ref Rect outRect, Pawn pawn)
        {
            if (pawn == null || !pawn.IsPrisonerOfColony) return;
            if (outRect.height <= StripHeight + 8f) return;
            outRect.y += StripHeight;
            outRect.height -= StripHeight;
        }

        [HarmonyPostfix]
        public static void Postfix(Rect outRect, Pawn pawn)
        {
            if (pawn == null || !pawn.IsPrisonerOfColony) return;
            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null) return;
            float balance = mgr.Balance(pawn);

            var strip = new Rect(outRect.x, outRect.y - StripHeight, outRect.width, StripHeight);
            Widgets.DrawBoxSolid(strip, new Color(0f, 0f, 0f, 0.45f));

            var labelRect = new Rect(strip.x + 8f, strip.y, strip.width - 16f, strip.height);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.95f, 0.85f, 0.4f);
            Widgets.Label(labelRect, "PPTE2_HealthTabBalance".Translate(
                balance.ToString("0.##"), PPTEName.Ticket, PPTEName.TicketUnit));

            var d = mgr.DataFor(pawn);
            if (d != null && d.ransomRequested && PrisonersPayToEat2Mod.Settings.enableRansom)
            {
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = new Color(1f, 0.65f, 0.4f);
                Widgets.Label(labelRect, "PPTE2_HealthTabRansomPending".Translate());
            }

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }
    }
}
