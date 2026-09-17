using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// 动机 Need（囚犯劳工系统）：无人看管 + 饥饿时下降，有殖民者在附近（监督）+ 吃饱 +
    /// 领到工资时上升。低于阈值（设置可调，默认 20%）囚犯拒绝工作；低于 50% 时
    /// 全局工作速度线性打折（最低 ×0.6，见 StatPart_PrisonerMotivation）。
    ///
    /// 所有类人 pawn 都会持有该 need（NeedDef 未加殖民者限制），但对非本殖民地囚犯
    /// 冻结且不在需求列表显示，无开销无副作用。
    /// </summary>
    public class Need_Motivation : Need
    {
        // NeedInterval 每 150 tick 触发一次（原版需求节拍），60000 tick = 1 游戏天
        private const int IntervalTicks = 150;
        private const float BaseFallPerDay = 0.03f;        // 基准缓慢消退
        private const float SupervisedRisePerDay = 0.09f;  // 附近有自由殖民者（被看管）
        private const float WellFedRisePerDay = 0.05f;     // 食物需求 >50%
        private const float HungryFallPerDay = 0.15f;      // 食物需求 <25%
        private const float SuperviseRadius = 12f;

        public Need_Motivation(Pawn pawn) : base(pawn) { }

        public override void SetInitialLevel() => CurLevel = 0.5f;

        public override bool ShowOnNeedList =>
            PrisonerWorkSystem.Active
            && PrisonersPayToEat2Mod.Settings != null && PrisonersPayToEat2Mod.Settings.enableMotivation
            && pawn != null && pawn.IsPrisonerOfColony;

        protected override bool IsFrozen =>
            !PrisonerWorkSystem.Active || pawn == null || !pawn.IsPrisonerOfColony;

        public override void NeedInterval()
        {
            if (IsFrozen) return;
            if (!PrisonersPayToEat2Mod.Settings.enableMotivation) return;
            if (!pawn.Spawned || pawn.Dead) return;

            float perDay = -BaseFallPerDay;
            if (IsSupervised(pawn)) perDay += SupervisedRisePerDay;
            var food = pawn.needs?.food;
            if (food != null)
            {
                if (food.CurLevelPercentage > 0.5f) perDay += WellFedRisePerDay;
                else if (food.CurLevelPercentage < 0.25f) perDay -= HungryFallPerDay;
            }
            CurLevel = Mathf.Clamp01(CurLevel + perDay * IntervalTicks / 60000f);
        }

        /// <summary>有存活、未倒地的自由殖民者在附近（看管加成）。</summary>
        private static bool IsSupervised(Pawn pawn)
        {
            var map = pawn.MapHeld;
            if (map == null) return false;
            float r2 = SuperviseRadius * SuperviseRadius;
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                var c = colonists[i];
                if (c == null || c.Dead || c.Downed) continue;
                if ((c.Position - pawn.Position).LengthHorizontalSquared <= r2) return true;
            }
            return false;
        }

        /// <summary>工资入账时的小幅动机提升（"拿到钱就有干劲"）。</summary>
        public void NotifyWageEarned(float amount)
        {
            if (IsFrozen) return;
            CurLevel = Mathf.Clamp01(CurLevel + Mathf.Min(amount * 0.005f, 0.04f));
        }

        /// <summary>工资发放点统一调用（时薪累积入账 + 计件结算）。</summary>
        public static void NotifyWage(Pawn pawn, float amount)
        {
            if (!PrisonerWorkSystem.Active) return;
            if (PrisonersPayToEat2Mod.Settings == null || !PrisonersPayToEat2Mod.Settings.enableMotivation) return;
            pawn?.needs?.TryGetNeed<Need_Motivation>()?.NotifyWageEarned(amount);
        }

        public override string GetTipString()
        {
            string tip = base.GetTipString();
            var s = PrisonersPayToEat2Mod.Settings;
            if (s != null)
                tip += "\n" + "PPTE2_MotivationTip".Translate(s.motivationWorkThreshold.ToStringPercent());
            return tip;
        }
    }

    /// <summary>
    /// 动机对全局工作速度的修正（注入 WorkSpeedGlobal 的 parts，见 Patches/PPTE2_MotivationStat.xml）：
    /// 动机 0 → ×0.6，0.5 及以上 → ×1.0，之间线性。仅对本殖民地囚犯生效。
    /// </summary>
    public class StatPart_PrisonerMotivation : StatPart
    {
        public static float FactorFor(float level) =>
            level >= 0.5f ? 1f : Mathf.Lerp(0.6f, 1f, level / 0.5f);

        private static float ActiveLevel(StatRequest req)
        {
            if (!req.HasThing || !(req.Thing is Pawn pawn)) return -1f;
            if (!PrisonerWorkSystem.Active) return -1f;
            var s = PrisonersPayToEat2Mod.Settings;
            if (s == null || !s.enableMotivation) return -1f;
            if (!pawn.IsPrisonerOfColony) return -1f;
            return PrisonerWorkSystem.MotivationLevel(pawn);
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            float level = ActiveLevel(req);
            if (level >= 0f) val *= FactorFor(level);
        }

        public override string ExplanationPart(StatRequest req)
        {
            float level = ActiveLevel(req);
            if (level < 0f || level >= 0.5f) return null;
            return "PPTE2_MotivationStatPart".Translate(FactorFor(level).ToStringPercent());
        }
    }
}
