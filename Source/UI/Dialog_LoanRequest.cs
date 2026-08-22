using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// 囚犯向玩家借款的逐笔批准弹窗（设置「允许向玩家借款」开启后由
    /// <see cref="PrisonerSocialTicker"/> 触发）。同意=凭空增发票（无息、纯救济），
    /// 拒绝=该囚犯借款冷却照常生效。
    /// </summary>
    public class Dialog_LoanRequest : Window
    {
        private readonly Pawn _borrower;

        public override Vector2 InitialSize => new Vector2(380f, 170f);

        public Dialog_LoanRequest(Pawn borrower)
        {
            _borrower = borrower;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        private float RequestedAmount
        {
            get
            {
                var s = PrisonersPayToEat2Mod.Settings;
                var mgr = PrisonersPayToEat2Manager.Current;
                float balance = mgr?.Balance(_borrower) ?? 0f;
                return Mathf.Min(s.maxLoanAmount, Mathf.Max(1f, s.loanTriggerBalance - balance));
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "PPTE2_PlayerLoanTitle".Translate());
            Text.Font = GameFont.Small;

            var bodyRect = new Rect(0f, 40f, inRect.width, 60f);
            Widgets.Label(bodyRect, "PPTE2_PlayerLoanBody".Translate(
                _borrower.LabelShortCap, RequestedAmount.ToString("0.##"), PPTEName.Ticket));

            float btnW = (inRect.width - 8f) / 2f;
            var approveRect = new Rect(0f, inRect.height - 38f, btnW - 4f, 32f);
            var rejectRect = new Rect(inRect.width - btnW + 4f, inRect.height - 38f, btnW - 4f, 32f);

            if (Widgets.ButtonText(approveRect, "PPTE2_PlayerLoanApprove".Translate()))
            {
                PrisonersPayToEat2Manager.Current?.ApprovePlayerLoan(_borrower);
                Close();
            }
            if (Widgets.ButtonText(rejectRect, "PPTE2_PlayerLoanReject".Translate()))
            {
                PrisonersPayToEat2Manager.Current?.RejectPlayerLoan(_borrower);
                Close();
            }
        }
    }
}
