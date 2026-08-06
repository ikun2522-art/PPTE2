using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// A read-only info card shown in the pawn's gizmo row, displaying the prisoner's
    /// current meal-ticket balance. Not a button — no click interaction.
    /// </summary>
    public class Gizmo_TicketBalance : Gizmo
    {
        private readonly Pawn _pawn;

        public Gizmo_TicketBalance(Pawn pawn)
        {
            _pawn = pawn;
            Order = -999f; // keep it at the end of the gizmo row
        }

        public override float GetWidth(float maxWidth) => 96f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            float w = GetWidth(maxWidth);
            var rect = new Rect(topLeft.x, topLeft.y, w, 75f);

            // dark card with outline
            Widgets.DrawBoxSolidWithOutline(rect,
                new Color(0f, 0f, 0f, 0.45f),
                new Color(0.85f, 0.72f, 0.3f, 0.9f), 1);

            float balance = PrisonersPayToEat2Manager.Current?.Balance(_pawn) ?? 0f;

            // title
            Text.Anchor = TextAnchor.UpperCenter;
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.85f, 0.85f, 0.75f);
            Widgets.Label(new Rect(rect.x, rect.y + 6f, rect.width, 18f), PPTEName.BalanceTitle);

            // big number
            Text.Font = GameFont.Medium;
            GUI.color = balance > 0f ? new Color(0.95f, 0.85f, 0.4f) : new Color(0.95f, 0.4f, 0.3f);
            Widgets.Label(new Rect(rect.x, rect.y + 26f, rect.width, 32f), balance.ToString("0.##"));

            // unit label
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.65f);
            Widgets.Label(new Rect(rect.x, rect.y + 56f, rect.width, 16f), PPTEName.TicketUnit);

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            TooltipHandler.TipRegion(rect, "PPTE2_GizmoBalanceDesc".Translate());

            return new GizmoResult(GizmoState.Clear);
        }
    }
}