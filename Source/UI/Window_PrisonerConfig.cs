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

        public override Vector2 InitialSize => new Vector2(420f, 360f);

        public Window_PrisonerConfig(Pawn prisoner)
        {
            _prisoner = prisoner;
            _data = PrisonersPayToEat2Manager.Current.DataFor(prisoner);
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
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
            }

            list.End();
        }
    }
}