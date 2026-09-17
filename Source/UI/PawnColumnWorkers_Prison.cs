using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace PrisonersPayToEat2
{
    /// <summary>总览列：强制工作开关。点击在 允许/禁止 间切换（覆盖全局默认）。</summary>
    public class PawnColumnWorker_WorkToggle : PawnColumnWorker
    {
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null || pawn == null) return;
            bool enabled = PrisonerWorkSystem.IsWorkEnabled(pawn);
            var cr = new Rect(rect.x + (rect.width - 24f) / 2f, rect.y + (rect.height - 24f) / 2f, 24f, 24f);
            bool cur = enabled;
            Widgets.Checkbox(cr.position, ref cur, 24f);
            if (cur != enabled)
            {
                mgr.DataFor(pawn).workSetting = cur ? PPTE2SocialSetting.Allow : PPTE2SocialSetting.Deny;
                if (!cur) pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced); // 立即停下手头的活
                (cur ? SoundDefOf.Checkbox_TurnedOn : SoundDefOf.Checkbox_TurnedOff).PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(cr, "PPTE2_ColWorkToggleTip".Translate());
        }

        public override int Compare(Pawn a, Pawn b) =>
            PrisonerWorkSystem.IsWorkEnabled(a).CompareTo(PrisonerWorkSystem.IsWorkEnabled(b));
    }

    /// <summary>总览列：动机条（动机系统关闭时整列隐藏）。</summary>
    public class PawnColumnWorker_Motivation : PawnColumnWorker
    {
        public override bool VisibleCurrently =>
            PrisonersPayToEat2Mod.Settings != null && PrisonersPayToEat2Mod.Settings.enableMotivation;

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            float level = PrisonerWorkSystem.MotivationLevel(pawn);
            if (level < 0f) return;
            var bar = rect.ContractedBy(2f, 8f);
            Widgets.FillableBar(bar, level);
            TooltipHandler.TipRegion(bar, "PPTE2_ColMotivationTip".Translate(
                level.ToStringPercent(),
                PrisonersPayToEat2Mod.Settings.motivationWorkThreshold.ToStringPercent()));
        }

        public override int Compare(Pawn a, Pawn b) =>
            PrisonerWorkSystem.MotivationLevel(a).CompareTo(PrisonerWorkSystem.MotivationLevel(b));
    }

    /// <summary>总览列：饭票余额（点击打开该囚犯的饭票菜单）。</summary>
    public class PawnColumnWorker_TicketBalance : PawnColumnWorker
    {
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null || pawn == null) return;
            var cell = rect.ContractedBy(2f, 4f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(cell, mgr.Balance(pawn).ToString("0.##"));
            Text.Anchor = TextAnchor.UpperLeft;
            if (Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);
            TooltipHandler.TipRegion(rect, "PPTE2_ColBalanceTip".Translate(PPTEName.Ticket));
            if (Widgets.ButtonInvisible(rect, false))
                Find.WindowStack.Add(new Window_PrisonerMenu(pawn));
        }

        public override int Compare(Pawn a, Pawn b)
        {
            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null) return 0;
            return mgr.Balance(a).CompareTo(mgr.Balance(b));
        }
    }

    /// <summary>总览列：当前正在做的工种（或空闲/其他）。</summary>
    public class PawnColumnWorker_WorkStatus : PawnColumnWorker
    {
        private static string Status(Pawn pawn)
        {
            if (pawn == null || pawn.Dead) return "-";
            var wt = PrisonLaborBridge.CurrentWorkTypeDef(pawn);
            if (wt != null) return wt.labelShort ?? wt.defName;
            return "PPTE2_StatusIdle".Translate();
        }

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            var cell = rect.ContractedBy(2f, 4f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(cell, Status(pawn));
            Text.Anchor = TextAnchor.UpperLeft;
        }

        public override int Compare(Pawn a, Pawn b) =>
            string.CompareOrdinal(Status(a), Status(b));
    }

    /// <summary>
    /// 日程列：囚犯活动区。原版 PawnColumnWorker_AllowedArea 只给玩家派系画控件，
    /// 这里去掉派系检查（囚犯的 playerSettings/allowedAreas 本就存在，只是 UI 不给画）。
    /// </summary>
    public class PawnColumnWorker_PrisonerAllowedArea : PawnColumnWorker
    {
        public override int GetMinWidth(PawnTable table) => Mathf.Max(base.GetMinWidth(table), 200);
        public override int GetOptimalWidth(PawnTable table) => Mathf.Clamp(273, GetMinWidth(table), GetMaxWidth(table));
        public override int GetMinHeaderHeight(PawnTable table) => Mathf.Max(base.GetMinHeaderHeight(table), 65);

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            if (pawn?.playerSettings == null) return;
            if (pawn.playerSettings.SupportsAllowedAreas)
                AreaAllowedGUI.DoAllowedAreaSelectors(rect, pawn);
        }

        public override void DoHeader(Rect rect, PawnTable table)
        {
            base.DoHeader(rect, table);
            if (Widgets.ButtonText(new Rect(rect.x, rect.y + (rect.height - 65f), Mathf.Min(rect.width, 360f), 32f),
                    "ManageAreas".Translate()))
                Find.WindowStack.Add(new Dialog_ManageAreas(Find.CurrentMap));
        }

        public override int Compare(Pawn a, Pawn b) =>
            (a.playerSettings?.AreaRestrictionInPawnCurrentMap?.ID ?? int.MinValue)
                .CompareTo(b.playerSettings?.AreaRestrictionInPawnCurrentMap?.ID ?? int.MinValue);
    }

    /// <summary>
    /// 工作页优先级列：复用原版绘制/交互，只加"全局允许工种"可见性过滤
    /// （设置里关掉的工种不占列）。
    /// </summary>
    public class PawnColumnWorker_PrisonerWorkPriority : PawnColumnWorker_WorkPriority
    {
        public override bool VisibleCurrently =>
            def.workType != null && def.workType.VisibleCurrently
            && PrisonerWorkSystem.IsAllowedWorkType(def.workType);
    }
}
