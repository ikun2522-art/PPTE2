using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Per-prisoner settings window. Player adjusts individual food+wage multipliers and an
    /// organ-harvest override. Lives on top of the global settings.
    /// </summary>
    public class Window_PrisonerConfig : Window
    {
        private readonly Pawn _prisoner;
        private PrisonerTicketData _data;

        public override Vector2 InitialSize => new Vector2(440f, 700f);

        public Window_PrisonerConfig(Pawn prisoner)
        {
            _prisoner = prisoner;
            _data = PrisonersPayToEat2Manager.Current.DataFor(prisoner);
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
        }

        /// <summary>一行三态按钮（跟随全局 / 允许 / 禁止），复用工资计费方式的行样式。</summary>
        private void DrawSocialFeatureRow(Listing_Standard list, Rect inRect, string label,
            PPTE2SocialSetting current, System.Action<PPTE2SocialSetting> set)
        {
            list.Label(label);
            float w = (inRect.width - 8f) / 3f;
            float by = list.CurHeight;
            Color gold = new Color(1f, 0.85f, 0.45f);
            for (int i = 0; i < 3; i++)
            {
                var v = (PPTE2SocialSetting)i;
                string text = v == PPTE2SocialSetting.Follow ? "PPTE2_SocialFollow".Translate()
                    : v == PPTE2SocialSetting.Allow ? "PPTE2_SocialAllow".Translate()
                    : "PPTE2_SocialDeny".Translate();
                var r = new Rect(inRect.x + i * w, by, w - 4f, 26f);
                if (current == v) GUI.color = gold;
                if (Widgets.ButtonText(r, text)) set(v);
                GUI.color = Color.white;
            }
            list.Gap(34f);
        }

        public override void DoWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.ColumnWidth = inRect.width - 8f;
            list.Begin(inRect);

            // title
            Text.Font = GameFont.Medium;
            list.Label(_prisoner.LabelShortCap);
            Text.Font = GameFont.Small;

            list.Gap(8f);
            list.Label("PPTE2_BalanceLabel".Translate(_data.ticketBalance.ToString("0.##"), PPTEName.Ticket));

            list.Gap(12f);
            list.Label("PPTE2_FoodMulLabel".Translate(_data.foodMultiplier.ToString("0.00")));
            _data.foodMultiplier = list.Slider(_data.foodMultiplier, 0.1f, 10f);

            list.Gap(4f);
            list.Label("PPTE2_WageMulLabel".Translate(_data.wageMultiplier.ToString("0.00")));
            _data.wageMultiplier = list.Slider(_data.wageMultiplier, 0.1f, 10f);

            list.Gap(10f);
            list.Label("PPTE2_WageModeLabel".Translate());
            float buttonW = (inRect.width - 8f) / 3f;
            float by = list.CurHeight;
            Color gold = new Color(1f, 0.85f, 0.45f);
            for (int i = 0; i < 3; i++)
            {
                var mode = (PieceRateMode)i;
                string label = mode == PieceRateMode.Follow ? "PPTE2_WageModeFollow".Translate()
                    : mode == PieceRateMode.PerHour ? "PPTE2_ModePerHour".Translate()
                    : "PPTE2_ModePerItem".Translate();
                var r = new Rect(inRect.x + i * buttonW, by, buttonW - 4f, 26f);
                if (_data.wageMode == mode) GUI.color = gold;
                if (Widgets.ButtonText(r, label)) _data.wageMode = mode;
                GUI.color = Color.white;
            }
            list.Gap(34f);

            // 囚犯社会行为：每项三态（跟随全局 / 允许 / 禁止）
            list.Gap(8f);
            list.Label("PPTE2_SocialSettingsTitle".Translate());
            list.Gap(4f);
            DrawSocialFeatureRow(list, inRect, "PPTE2_FeatureLoan".Translate(),
                _data.loanSetting, v => _data.loanSetting = v);
            DrawSocialFeatureRow(list, inRect, "PPTE2_FeatureRobbery".Translate(),
                _data.robberySetting, v => _data.robberySetting = v);
            DrawSocialFeatureRow(list, inRect, "PPTE2_FeatureBegging".Translate(),
                _data.beggingSetting, v => _data.beggingSetting = v);

            // 父母代付（仅儿童可见）：跟随全局 / 允许 / 禁止
            if (PrisonersPayToEat2Manager.IsChild(_prisoner))
            {
                list.Gap(6f);
                list.Label("PPTE2_ChildPayTitle".Translate());
                list.Gap(4f);
                DrawSocialFeatureRow(list, inRect, "PPTE2_FeatureChildPay".Translate(),
                    _data.childPaySetting, v => _data.childPaySetting = v);
            }

            list.Gap(12f);
            bool overrideOn = _data.organHarvestOverrideEnabled;
            list.CheckboxLabeled("PPTE2_OverrideOrganHarvest".Translate(), ref overrideOn);
            _data.organHarvestOverrideEnabled = overrideOn;
            if (overrideOn)
            {
                bool allow = _data.organHarvestOverride;
                list.CheckboxLabeled("PPTE2_AllowOrganHarvest".Translate(), ref allow);
                _data.organHarvestOverride = allow;
            }
            else
            {
                // when override is off, show the effective global value for context
                list.Label("    " + "PPTE2_GlobalSetting".Translate(
                    PrisonersPayToEat2Mod.Settings.enableOrganHarvest
                        ? "PPTE2_Enabled".Translate() : "PPTE2_Disabled".Translate()));
            }

            list.Gap(12f);
            if (list.ButtonText("PPTE2_ResetDefaults".Translate()))
            {
                _data.foodMultiplier = 1.0f;
                _data.wageMultiplier = 1.0f;
                _data.wageMode = PieceRateMode.Follow;
                _data.organHarvestOverrideEnabled = false;
                _data.organHarvestOverride = false;
                _data.ransomTicketOverride = 0f;
                _data.ransomMinDaysOverride = 0f;
                _data.loanSetting = PPTE2SocialSetting.Follow;
                _data.robberySetting = PPTE2SocialSetting.Follow;
                _data.beggingSetting = PPTE2SocialSetting.Follow;
                _data.childPaySetting = PPTE2SocialSetting.Follow;
            }

            list.Gap(16f);
            list.Label("PPTE2_RansomOverrideTitle".Translate());
            list.Gap(4f);

            // ransom ticket override (0 = follow global)
            string bufTicket = _data.ransomTicketOverride > 0f ? _data.ransomTicketOverride.ToString("0.##") : "";
            float oy = list.CurHeight;
            Widgets.Label(new Rect(inRect.x, oy + 2f, 230f, 24f), "PPTE2_RansomTicketOverrideLabel".Translate());
            string newBufTicket = Widgets.TextField(new Rect(inRect.x + 240f, oy, 110f, 26f), bufTicket);
            if (newBufTicket != bufTicket)
            {
                if (float.TryParse(newBufTicket, out float res) && res > 0f) _data.ransomTicketOverride = res;
                else _data.ransomTicketOverride = 0f;
            }
            list.Gap(32f);

            // ransom minimum days override (0 = follow global)
            string bufDays = _data.ransomMinDaysOverride > 0f ? _data.ransomMinDaysOverride.ToString("0.#") : "";
            float oy2 = list.CurHeight;
            Widgets.Label(new Rect(inRect.x, oy2 + 2f, 230f, 24f), "PPTE2_RansomMinDaysOverrideLabel".Translate());
            string newBufDays = Widgets.TextField(new Rect(inRect.x + 240f, oy2, 110f, 26f), bufDays);
            if (newBufDays != bufDays)
            {
                if (float.TryParse(newBufDays, out float res) && res > 0f) _data.ransomMinDaysOverride = res;
                else _data.ransomMinDaysOverride = 0f;
            }
            list.Gap(32f);

            // effective values currently in use
            var mgr = PrisonersPayToEat2Manager.Current;
            GUI.color = new Color(0.7f, 0.7f, 0.65f);
            list.Label("PPTE2_RansomEffective".Translate(
                mgr.EffectiveRansomCost(_prisoner).ToString("0.##"),
                mgr.EffectiveRansomMinDays(_prisoner).ToString("0.#")));
            GUI.color = Color.white;

            list.End();
        }
    }
}