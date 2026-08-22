using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// The single entry point for all meal-ticket controls of one prisoner (replaces the four
    /// separate gizmos): balance display, give/take tickets, individual configuration, and the
    /// ransom (赎身) request approval.
    /// </summary>
    public class Window_PrisonerMenu : Window
    {
        private readonly Pawn _prisoner;

        public override Vector2 InitialSize => new Vector2(440f, 430f);

        public Window_PrisonerMenu(Pawn prisoner)
        {
            _prisoner = prisoner;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            closeOnClickedOutside = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.ColumnWidth = inRect.width - 8f;
            list.Begin(inRect);

            // title + balance
            Text.Font = GameFont.Medium;
            list.Label(_prisoner.LabelShortCap);
            Text.Font = GameFont.Small;
            list.Gap(2f);
            var mgr = PrisonersPayToEat2Manager.Current;
            float balance = mgr.Balance(_prisoner);
            list.Label("PPTE2_BalanceLabel".Translate(balance.ToString("0.##"), PPTEName.Ticket));
            float owed = mgr.OutstandingOwed(_prisoner);
            if (owed > 0f)
            {
                GUI.color = new Color(1f, 0.7f, 0.6f);
                list.Label("PPTE2_OwesLabel".Translate(owed.ToString("0.##"), PPTEName.Ticket));
                GUI.color = Color.white;
            }
            if (mgr.ChildParentPayActive(_prisoner))
            {
                GUI.color = new Color(0.75f, 0.9f, 0.75f);
                list.Label("PPTE2_ParentPayLine".Translate(
                    mgr.ParentTotalBalance(_prisoner).ToString("0.##"), PPTEName.Ticket));
                GUI.color = Color.white;
            }

            list.Gap(12f);
            float btnW = (inRect.width - 8f) / 3f;
            float by = list.CurHeight;
            if (Widgets.ButtonText(new Rect(inRect.x, by, btnW - 4f, 30f), "PPTE2_MenuGive".Translate()))
                Find.WindowStack.Add(new Dialog_GiveTickets(_prisoner, true));
            if (Widgets.ButtonText(new Rect(inRect.x + btnW, by, btnW - 4f, 30f), "PPTE2_MenuTake".Translate()))
                Find.WindowStack.Add(new Dialog_GiveTickets(_prisoner, false));
            if (Widgets.ButtonText(new Rect(inRect.x + btnW * 2f, by, btnW - 4f, 30f), "PPTE2_MenuConfig".Translate()))
                Find.WindowStack.Add(new Window_PrisonerConfig(_prisoner));
            list.Gap(38f);

            DrawRansomSection(list, mgr, inRect);

            list.End();
        }

        private void DrawRansomSection(Listing_Standard list, PrisonersPayToEat2Manager mgr, Rect inRect)
        {
            var settings = PrisonersPayToEat2Mod.Settings;
            GUI.color = new Color(0.85f, 0.85f, 0.75f);
            list.Label("PPTE2_RansomTitle".Translate());
            GUI.color = Color.white;
            list.Gap(4f);

            if (!settings.enableRansom)
            {
                GUI.color = new Color(0.7f, 0.7f, 0.65f);
                list.Label("PPTE2_RansomDisabledGlobal".Translate());
                GUI.color = Color.white;
                return;
            }

            float cost = mgr.EffectiveRansomCost(_prisoner);
            float minDays = mgr.EffectiveRansomMinDays(_prisoner);
            float days = mgr.ImprisonedDays(_prisoner);
            float available = mgr.AvailableBalance(_prisoner); // 儿童含父母代付
            GUI.color = new Color(0.75f, 0.75f, 0.7f);
            list.Label("PPTE2_RansomNeedTickets".Translate(cost.ToString("0.##"), available.ToString("0.##")));
            list.Label("PPTE2_RansomNeedTime".Translate(minDays.ToString("0.#"), days.ToString("0.#")));
            if (mgr.HasOutstandingLoans(_prisoner))
            {
                GUI.color = new Color(1f, 0.7f, 0.6f);
                list.Label("PPTE2_RansomHasDebt".Translate());
                GUI.color = Color.white;
            }
            GUI.color = Color.white;
            list.Gap(8f);

            var d = mgr.DataFor(_prisoner);
            if (d.ransomRequested)
            {
                // awaiting player decision
                GUI.color = new Color(1f, 0.85f, 0.45f);
                list.Label("PPTE2_RansomRequestPending".Translate(
                    _prisoner.LabelShortCap, cost.ToString("0.##"), PPTEName.Ticket));
                GUI.color = Color.white;
                list.Gap(6f);

                float bw = (inRect.width - 8f) / 2f;
                float by = list.CurHeight;
                if (Widgets.ButtonText(new Rect(inRect.x, by, bw - 4f, 30f), "PPTE2_RansomApprove".Translate()))
                {
                    if (mgr.TryApproveRansom(_prisoner)) Close(); // released
                }
                if (Widgets.ButtonText(new Rect(inRect.x + bw, by, bw - 4f, 30f), "PPTE2_RansomReject".Translate()))
                {
                    mgr.RejectRansom(_prisoner);
                }
                list.Gap(36f);
            }
            else
            {
                float cooldownLeft = (d.ransomDeniedUntilTick - Find.TickManager.TicksGame) / 60000f;
                if (cooldownLeft > 0f)
                {
                    GUI.color = new Color(0.7f, 0.7f, 0.65f);
                    list.Label("PPTE2_RansomCooldown".Translate(cooldownLeft.ToString("0.#")));
                    GUI.color = Color.white;
                }
                else if (mgr.CanRansom(_prisoner))
                {
                    GUI.color = new Color(0.6f, 0.9f, 0.6f);
                    list.Label("PPTE2_RansomEligibleWait".Translate());
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.color = new Color(0.7f, 0.7f, 0.65f);
                    list.Label("PPTE2_RansomNotEligible".Translate());
                    GUI.color = Color.white;
                }
            }
        }
    }
}
