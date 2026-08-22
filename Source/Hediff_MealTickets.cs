using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// 以"健康状态"方式在健康标签页显示囚犯的饭票余额（参考第一代 PPTE 的做法）。
    /// 余额实时读取；颜色随余额高低变化（绿 = 充裕 / 黄 = 一般 / 橙 = 紧张 / 红 = 见底或欠款）。
    /// 欠款（赊账，余额为负）显示为"欠 X 张"。
    /// 该 hediff 由 <see cref="PrisonersPayToEat2Manager"/> 定时挂载到囚犯身上，并在囚犯
    /// 失去囚犯身份（释放/招募/越狱/死亡）后由 ShouldRemove + 管理器清理移除。
    /// </summary>
    public class Hediff_MealTickets : Hediff
    {
        public override string LabelBase
        {
            get
            {
                float tickets = GetTickets();
                if (tickets < 0f)
                    return "PPTE2_HediffLabel_Debt".Translate(
                        (-tickets).ToString("0.##"), PPTEName.Ticket, PPTEName.TicketUnit);
                return "PPTE2_HediffLabel".Translate(tickets.ToString("0.##"), PPTEName.Ticket);
            }
        }

        public override string LabelInBrackets => null;

        public override string SeverityLabel => GetTickets().ToString("0.##");

        public override string TipStringExtra
        {
            get
            {
                if (pawn == null) return base.TipStringExtra;
                float tickets = GetTickets();
                if (tickets < 0f)
                    return "PPTE2_HediffTip_Debt".Translate(
                        (-tickets).ToString("0.##"), PPTEName.Ticket, PPTEName.TicketUnit);
                return "PPTE2_HediffTip_Balance".Translate(
                    tickets.ToString("0.##"), PPTEName.Ticket, PPTEName.TicketUnit);
            }
        }

        public override Color LabelColor
        {
            get
            {
                float tickets = GetTickets();
                if (tickets < 0f) return Color.red;   // 欠款
                if (tickets >= 10f) return Color.green;
                if (tickets >= 1f) return Color.yellow;
                if (tickets >= 0.1f) return new Color(1f, 0.5f, 0f);
                return Color.red;
            }
        }

        public override bool ShouldRemove
        {
            get
            {
                return pawn == null || pawn.Dead || !pawn.IsPrisonerOfColony;
            }
        }

        private float GetTickets()
        {
            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null || pawn == null) return 0f;
            return mgr.Balance(pawn);
        }
    }
}
