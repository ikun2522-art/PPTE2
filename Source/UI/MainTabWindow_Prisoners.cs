using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// 「囚犯」主标签页（内置劳工系统的管理界面）。自画子标签行切换 4 张囚犯表：
    /// 总览（工作开关/动机/饭票余额/状态）、工作（1-4 优先级，只列全局允许的工种）、
    /// 日程（时间表 + 活动区）、分配（医疗/着装/饮食/药品）。
    ///
    /// 不继承 MainTabWindow_PawnTable（它的 table/CreateTable 是 private，没法换表），
    /// 自己持有一份 PawnTable 并按子标签重建。安装 Prison Labor 或关闭总开关时显示停用提示。
    /// </summary>
    public class MainTabWindow_Prisoners : MainTabWindow
    {
        private enum PrisonerTab { Overview, Work, Schedule, Assign }

        private PrisonerTab _tab = PrisonerTab.Overview;
        private PawnTable _table;
        private int _lastDirtyTick = -1;

        private const float TabBarH = 38f;
        private const float BottomSpace = 53f; // 与 MainTabWindow_PawnTable 一致的底部留白

        protected override float Margin => 6f;

        private IEnumerable<Pawn> Pawns
        {
            get
            {
                var map = Find.CurrentMap;
                if (map == null) yield break;
                var list = map.mapPawns.PrisonersOfColony;
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null && !list[i].Dead)
                        yield return list[i];
            }
        }

        private PawnTableDef CurrentTableDef
        {
            get
            {
                string defName = _tab == PrisonerTab.Work ? "PPTE2_PrisonersWork"
                    : _tab == PrisonerTab.Schedule ? "PPTE2_PrisonersSchedule"
                    : _tab == PrisonerTab.Assign ? "PPTE2_PrisonersAssign"
                    : "PPTE2_PrisonersOverview";
                return DefDatabase<PawnTableDef>.GetNamedSilentFail(defName);
            }
        }

        public override Vector2 RequestedTabSize
        {
            get
            {
                if (_table == null) return Vector2.zero;
                return new Vector2(_table.Size.x + Margin * 2f,
                    _table.Size.y + TabBarH + BottomSpace + Margin * 2f);
            }
        }

        public override void PostOpen()
        {
            base.PostOpen();
            CreateTable();
            SetInitialSizeAndPosition();
        }

        public override void Notify_ResolutionChanged()
        {
            CreateTable();
            base.Notify_ResolutionChanged();
        }

        private void CreateTable()
        {
            var def = CurrentTableDef;
            if (def == null) { _table = null; return; }
            _table = new PawnTable(def, () => Pawns,
                UI.screenWidth - (int)(Margin * 2f),
                UI.screenHeight - 35 - (int)(TabBarH + BottomSpace + Margin * 2f));
        }

        public override void DoWindowContents(Rect rect)
        {
            if (!PrisonerWorkSystem.Active)
            {
                // 停用提示：装了 Prison Labor（它接管）或设置里关了总开关
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(0.8f, 0.8f, 0.75f);
                Widgets.Label(rect, PrisonLaborBridge.Present
                    ? "PPTE2_PrisonTabDisabledPL".Translate()
                    : "PPTE2_PrisonTabDisabledSetting".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            if (_table == null) CreateTable();
            if (_table == null) return;

            // 囚犯进出（捕获/释放/招募）后刷新行；30 tick 一次的脏检查足够
            int now = Find.TickManager.TicksGame;
            if (now - _lastDirtyTick > 30)
            {
                _lastDirtyTick = now;
                _table.SetDirty();
            }

            DrawTabBar(rect);

            var tablePos = new Vector2(rect.x, rect.y + TabBarH);
            _table.PawnTableOnGUI(tablePos);

            // 日程页：画左上角的时间类型选择器（与原版 Restrict 标签页一致）
            if (_tab == PrisonerTab.Schedule)
                TimeAssignmentSelector.DrawTimeAssignmentSelectorGrid(new Rect(rect.x, rect.y + TabBarH, 191f, 65f));
        }

        private void DrawTabBar(Rect rect)
        {
            string[] labels =
            {
                "PPTE2_SubTabOverview".Translate(),
                "PPTE2_SubTabWork".Translate(),
                "PPTE2_SubTabSchedule".Translate(),
                "PPTE2_SubTabAssign".Translate()
            };
            float tabW = 160f;
            Color gold = new Color(1f, 0.85f, 0.45f);
            for (int i = 0; i < labels.Length; i++)
            {
                var btn = new Rect(rect.x + i * tabW + 2f, rect.y, tabW - 4f, TabBarH - 6f);
                bool active = (int)_tab == i;
                Color saved = GUI.color;
                if (active) GUI.color = gold;
                if (Widgets.ButtonText(btn, labels[i]))
                {
                    _tab = (PrisonerTab)i;
                    CreateTable(); // 换表重建
                }
                GUI.color = saved;
                if (active)
                    Widgets.DrawBoxSolidWithOutline(
                        new Rect(btn.x, btn.yMax - 1f, btn.width, 3f), gold, gold, 0);
            }
            Widgets.DrawLineHorizontal(rect.x, rect.y + TabBarH - 2f, rect.width);
        }
    }
}
