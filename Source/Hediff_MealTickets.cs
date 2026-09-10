using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// 以"健康状态"方式在健康标签页显示囚犯的饭票余额（参考第一代 PPTE 的做法）。
    /// 余额实时读取；颜色随余额高低变化（绿 = 充裕 / 黄 = 一般 / 橙 = 紧张 / 红 = 见底或欠款）。
    /// 欠款（赊账，余额为负）显示为"欠 X 张"。
    /// 儿童在"合并钱包"模式下显示家庭合计余额（自己 + 在押父母的正余额）。
    /// 该 hediff 由 <see cref="PrisonersPayToEat2Manager"/> 定时挂载到囚犯身上，并在囚犯
    /// 失去囚犯身份（释放/招募/越狱/死亡）后由 ShouldRemove + 管理器清理移除。
    /// </summary>
    public class Hediff_MealTickets : Hediff
    {
        public override string LabelBase
        {
            get
            {
                float tickets = DisplayTickets();
                if (tickets < 0f)
                    return "PPTE2_HediffLabel_Debt".Translate(
                        (-tickets).ToString("0.##"), PPTEName.Ticket, PPTEName.TicketUnit);
                return "PPTE2_HediffLabel".Translate(tickets.ToString("0.##"), PPTEName.Ticket);
            }
        }

        public override string LabelInBrackets => null;

        public override string SeverityLabel => DisplayTickets().ToString("0.##");

        public override string TipStringExtra
        {
            get
            {
                if (pawn == null) return base.TipStringExtra;
                var mgr = PrisonersPayToEat2Manager.Current;
                float tickets = mgr?.Balance(pawn) ?? 0f;
                string tip = tickets < 0f
                    ? "PPTE2_HediffTip_Debt".Translate(
                        (-tickets).ToString("0.##"), PPTEName.Ticket, PPTEName.TicketUnit)
                    : "PPTE2_HediffTip_Balance".Translate(
                        tickets.ToString("0.##"), PPTEName.Ticket, PPTEName.TicketUnit);
                if (mgr != null && mgr.ChildParentPayActive(pawn))
                {
                    if (PrisonersPayToEat2Mod.Settings.childPayMode == ChildPayMode.MergedPool)
                        tip += "\n" + "PPTE2_HediffTip_MergedPool".Translate(
                            (mgr.Balance(pawn) + mgr.ParentTotalBalance(pawn)).ToString("0.##"),
                            PPTEName.Ticket);
                    else
                        tip += "\n" + "PPTE2_HediffTip_ParentSupport".Translate(
                            mgr.ParentTotalBalance(pawn).ToString("0.##"),
                            PPTEName.Ticket, mgr.SupportingParentNames(pawn));
                }
                return tip;
            }
        }

        public override Color LabelColor
        {
            get
            {
                float tickets = DisplayTickets();
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

        /// <summary>合并钱包模式下儿童显示家庭合计（自己余额 + 父母正余额），其余显示自己的余额。</summary>
        private float DisplayTickets()
        {
            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null || pawn == null) return 0f;
            if (mgr.ChildParentPayActive(pawn)
                && PrisonersPayToEat2Mod.Settings.childPayMode == ChildPayMode.MergedPool)
                return mgr.Balance(pawn) + mgr.ParentTotalBalance(pawn);
            return mgr.Balance(pawn);
        }
    }
}
