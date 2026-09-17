using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// 囚犯劳作区（每张地图单例，仿 Area_Home）：语义为"该区域内禁止殖民者工作"，
    /// 把工作留给囚犯（囚犯不受影响，机械体不受影响）。注册进 AreaManager 由
    /// Harmony_LaborArea 的补丁完成（新地图 AddStartingAreas / 老存档 PostLoadInit）。
    /// </summary>
    public class Area_PrisonerLabor : Area
    {
        public override string Label => "PPTE2_LaborArea".Translate();
        public override string BaseLabel => "PPTE2_LaborArea".Translate();
        public override Color Color => new Color(1f, 0.55f, 0.1f); // 橙色，与劳作主题一致
        public override bool Mutable => false;
        public override int ListPriority => 8000;

        public Area_PrisonerLabor() { }
        public Area_PrisonerLabor(AreaManager areaManager) : base(areaManager) { }

        public override string GetUniqueLoadID() => "Area_PPTE2_PrisonerLabor_" + ID;
        public override bool AssignableAsAllowed() => false; // 不是 pawn 活动区
    }

    /// <summary>劳作区画笔基类（仿 Designator_AreaHome：参数化 Add/Remove，图标复用原版区域图标）。</summary>
    public abstract class Designator_PrisonerLaborArea : Designator_Cells
    {
        private readonly DesignateMode mode;

        public override bool DragDrawMeasurements => true;
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.Areas;

        protected Designator_PrisonerLaborArea(DesignateMode mode)
        {
            this.mode = mode;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            useMouseIcon = true;
        }

        private Area LaborArea => Map?.areaManager?.Get<Area_PrisonerLabor>();

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map)) return false;
            var area = LaborArea;
            if (area == null) return false;
            return mode == DesignateMode.Add ? !area[c] : (AcceptanceReport)area[c];
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            var area = LaborArea;
            if (area == null) return;
            area[c] = mode == DesignateMode.Add;
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
            LaborArea?.MarkForDraw();
        }
    }

    public class Designator_PrisonerLaborAreaExpand : Designator_PrisonerLaborArea
    {
        public Designator_PrisonerLaborAreaExpand() : base(DesignateMode.Add)
        {
            defaultLabel = "PPTE2_LaborAreaExpand".Translate();
            defaultDesc = "PPTE2_LaborAreaExpandDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/AreaAllowedExpand");
            soundSucceeded = SoundDefOf.Designate_ZoneAdd_AllowedArea;
            tutorTag = "PPTE2_LaborAreaExpand";
        }
    }

    public class Designator_PrisonerLaborAreaClear : Designator_PrisonerLaborArea
    {
        public Designator_PrisonerLaborAreaClear() : base(DesignateMode.Remove)
        {
            defaultLabel = "PPTE2_LaborAreaClear".Translate();
            defaultDesc = "PPTE2_LaborAreaClearDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/AreaAllowedClear");
            soundSucceeded = SoundDefOf.Designate_ZoneAdd_AllowedArea;
            tutorTag = "PPTE2_LaborAreaClear";
        }
    }
}
