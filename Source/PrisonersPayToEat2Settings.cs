using RimWorld;
using Verse;
using UnityEngine;
using System.Collections.Generic;

namespace PrisonersPayToEat2
{
    public class PrisonersPayToEat2Settings : ModSettings
    {
        // Global toggles & multipliers
        public bool enableOrganHarvest = true;
        public float foodPriceMultiplier = 1.0f;
        public float wageMultiplier = 1.0f;
        public float silverToTicketRate = 1.0f;
        public int startingTicketsPerPrisoner = 3;
        public bool ignoreDuringRiot = true;
        public bool logVerbose = false;

        // Custom display name for the meal ticket currency (empty = default localized name).
        public string customTicketName = "";

        // Hourly meal-ticket wage per WorkTypeDef (work eligibility is decided by Prison Labor).
        public float defaultWagePerHour = 2f;
        public Dictionary<string, float> workTypeWages = new Dictionary<string, float>();

        // Per-foodDef custom price override (absolute ticket cost per eaten unit; supports decimals).
        public Dictionary<string, float> customFoodPrices = new Dictionary<string, float>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableOrganHarvest, "enableOrganHarvest", true);
            Scribe_Values.Look(ref foodPriceMultiplier, "foodPriceMultiplier", 1.0f);
            Scribe_Values.Look(ref wageMultiplier, "wageMultiplier", 1.0f);
            Scribe_Values.Look(ref defaultWagePerHour, "defaultWagePerHour", 2f);
            Scribe_Values.Look(ref silverToTicketRate, "silverToTicketRate", 1.0f);
            Scribe_Values.Look(ref startingTicketsPerPrisoner, "startingTicketsPerPrisoner", 3);
            Scribe_Values.Look(ref ignoreDuringRiot, "ignoreDuringRiot", true);
            Scribe_Values.Look(ref logVerbose, "logVerbose", false);
            Scribe_Values.Look(ref customTicketName, "customTicketName", "");
            Scribe_Collections.Look(ref workTypeWages, "workTypeWages", LookMode.Value, LookMode.Value);
            if (workTypeWages == null) workTypeWages = new Dictionary<string, float>();
            Scribe_Collections.Look(ref customFoodPrices, "customFoodPrices", LookMode.Value, LookMode.Value);
            if (customFoodPrices == null) customFoodPrices = new Dictionary<string, float>();
        }

        /// <summary>Hourly wage for a given WorkTypeDef.defName; falls back to defaultWagePerHour.</summary>
        public float GetWageForWorkType(string defName)
        {
            if (defName != null && workTypeWages.TryGetValue(defName, out float v)) return v;
            return defaultWagePerHour;
        }

        // ================= Settings window: self-drawn tab bar + per-tab scrolling =================

        private int _curTab; // 0=General, 1=WorkTypeWages, 2=FoodPrices

        public void DoWindowContents(Rect inRect)
        {
            const float tabBarH = 38f;
            string[] tabLabels =
            {
                "PPTE2_TabGeneral".Translate(),
                "PPTE2_TabWorkWages".Translate(),
                "PPTE2_TabFoodPrices".Translate()
            };

            float tabW = inRect.width / tabLabels.Length;
            for (int i = 0; i < tabLabels.Length; i++)
            {
                var btn = new Rect(inRect.x + i * tabW + 2f, inRect.y, tabW - 4f, tabBarH - 4f);
                bool active = _curTab == i;
                Color saved = GUI.color;
                if (active) GUI.color = new Color(1f, 0.85f, 0.45f); // highlight selected tab
                if (Widgets.ButtonText(btn, tabLabels[i])) _curTab = i;
                GUI.color = saved;
                if (active)
                {
                    Widgets.DrawBoxSolidWithOutline(
                        new Rect(btn.x, btn.yMax - 2f, btn.width, 3f),
                        new Color(1f, 0.85f, 0.45f), new Color(1f, 0.85f, 0.45f), 0);
                }
            }
            Widgets.DrawLineHorizontal(inRect.x, inRect.y + tabBarH, inRect.width);

            var content = new Rect(inRect.x, inRect.y + tabBarH + 8f, inRect.width,
                inRect.height - tabBarH - 8f);

            switch (_curTab)
            {
                case 0: DrawGeneralTab(content); break;
                case 1: DrawWorkTypeWagesTab(content); break;
                default: DrawFoodPricesTab(content); break;
            }
        }

        // ================= Tab 1: General =================

        private Vector2 _generalScroll = Vector2.zero;

        private void DrawGeneralTab(Rect rect)
        {
            // Content height: 5 sliders * 54 + 3 checkboxes * 30 + padding = ~380
            var view = new Rect(0f, 0f, rect.width - 20f, 400f);
            Widgets.BeginScrollView(rect, ref _generalScroll, view);

            float colW = (view.width - 40f) / 2f;

            // left column: checkboxes hugging labels
            float ly = 4f;
            DrawCheckboxRow(0f, ref ly, "PPTE2_EnableOrganHarvest".Translate(), ref enableOrganHarvest);
            DrawCheckboxRow(0f, ref ly, "PPTE2_IgnoreRiot".Translate(), ref ignoreDuringRiot);
            DrawCheckboxRow(0f, ref ly, "PPTE2_VerboseLog".Translate(), ref logVerbose);

            // ticket name editor
            Widgets.Label(new Rect(0f, ly + 2f, 200f, 24f), "PPTE2_TicketNameLabel".Translate());
            customTicketName = Widgets.TextField(new Rect(210f, ly, colW - 220f, 26f), customTicketName);
            if (!customTicketName.NullOrEmpty())
            {
                GUI.color = new Color(0.7f, 0.7f, 0.65f);
                Widgets.Label(new Rect(0f, ly + 30f, colW, 20f),
                    "PPTE2_TicketNamePreview".Translate(customTicketName));
                GUI.color = Color.white;
                ly += 54f;
            }
            else
            {
                GUI.color = new Color(0.7f, 0.7f, 0.65f);
                Widgets.Label(new Rect(0f, ly + 30f, colW, 20f),
                    "PPTE2_TicketNamePreview".Translate("PPTE2_TicketDefaultName".Translate()));
                GUI.color = Color.white;
                ly += 54f;
            }

            // right column: sliders
            float rx = colW + 40f;
            float ry = 4f;
            DrawSliderRow(rx, ref ry, colW, "PPTE2_FoodMul".Translate(foodPriceMultiplier.ToString("0.00")),
                ref foodPriceMultiplier, 0.1f, 10f);
            DrawSliderRow(rx, ref ry, colW, "PPTE2_WageMul".Translate(wageMultiplier.ToString("0.00")),
                ref wageMultiplier, 0.1f, 10f);
            DrawSliderRow(rx, ref ry, colW, "PPTE2_DefaultWagePerHour".Translate(defaultWagePerHour.ToString("0.0")),
                ref defaultWagePerHour, 0f, 20f);
            DrawSliderRow(rx, ref ry, colW, "PPTE2_SilverRate".Translate(silverToTicketRate.ToString("0.00")),
                ref silverToTicketRate, 0.1f, 10f);
            float startTickets = startingTicketsPerPrisoner;
            DrawSliderRow(rx, ref ry, colW, "PPTE2_StartingTickets".Translate(startingTicketsPerPrisoner),
                ref startTickets, 0f, 50f);
            startingTicketsPerPrisoner = (int)startTickets;

            Widgets.EndScrollView();
        }

        private static void DrawCheckboxRow(float x, ref float y, string label, ref bool value)
        {
            const float labelW = 320f;
            Widgets.Label(new Rect(x, y + 2f, labelW, 24f), label);
            Widgets.Checkbox(new Vector2(x + labelW + 8f, y), ref value);
            y += 30f;
        }

        private static void DrawSliderRow(float x, ref float y, float width, string label, ref float value, float min, float max)
        {
            Widgets.Label(new Rect(x, y, width, 24f), label);
            y += 26f;
            value = Widgets.HorizontalSlider(new Rect(x, y, width - 20f, 22f), value, min, max);
            y += 28f;
        }

        // ================= Tab 2: Per-work-type wages =================

        private static List<WorkTypeDef> _cachedWorkTypes;
        private Vector2 _workWageScroll = Vector2.zero;

        private void DrawWorkTypeWagesTab(Rect rect)
        {
            // toolbar
            float toolY = rect.y + 2f;
            GUI.color = new Color(0.7f, 0.7f, 0.65f);
            Widgets.Label(new Rect(rect.x, toolY + 2f, rect.width - 330f, 24f),
                "PPTE2_WorkTypeWagesHint".Translate());
            GUI.color = Color.white;
            if (Widgets.ButtonText(new Rect(rect.xMax - 320f, toolY, 155f, 26f), "PPTE2_ResetWorkTypeWages".Translate()))
                workTypeWages.Clear();
            if (Widgets.ButtonText(new Rect(rect.xMax - 155f, toolY, 155f, 26f), "PPTE2_FillWorkTypeWages".Translate()))
            {
                EnsureWorkTypesCached();
                foreach (var wt in _cachedWorkTypes) workTypeWages[wt.defName] = defaultWagePerHour;
            }

            // scrollable list filling the whole remaining tab area
            var listRect = new Rect(rect.x, toolY + 34f, rect.width, rect.yMax - toolY - 34f);
            EnsureWorkTypesCached();
            const float rowH = 26f;
            var view = new Rect(0f, 0f, listRect.width - 20f, 6f + _cachedWorkTypes.Count * rowH);
            Widgets.BeginScrollView(listRect, ref _workWageScroll, view);

            float y = 3f;
            const float labelW = 220f;
            const float inputW = 100f;
            float descW = view.width - labelW - inputW - 24f;

            foreach (var wt in _cachedWorkTypes)
            {
                bool has = workTypeWages.TryGetValue(wt.defName, out float cur);
                string buf = has ? cur.ToString("0.0") : "";

                Widgets.Label(new Rect(2f, y + 2f, labelW, 22f), wt.labelShort ?? wt.defName);
                if (!string.IsNullOrEmpty(wt.description))
                {
                    GUI.color = new Color(0.7f, 0.7f, 0.65f);
                    Widgets.Label(new Rect(2f + labelW + 4f, y + 2f, descW, 22f), wt.description.Truncate(descW));
                    GUI.color = Color.white;
                }

                string newBuf = Widgets.TextField(new Rect(view.width - inputW - 2f, y, inputW, 22f), buf);
                if (newBuf != buf)
                {
                    if (float.TryParse(newBuf, out float res) && res >= 0f) workTypeWages[wt.defName] = res;
                    else workTypeWages.Remove(wt.defName);
                }
                y += rowH;
            }

            Widgets.EndScrollView();
        }

        private static void EnsureWorkTypesCached()
        {
            if (_cachedWorkTypes != null) return;
            _cachedWorkTypes = new List<WorkTypeDef>();
            foreach (var wt in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                _cachedWorkTypes.Add(wt);
            _cachedWorkTypes.Sort((a, b) =>
                string.CompareOrdinal(a.labelShort ?? a.defName, b.labelShort ?? b.defName));
        }

        // ================= Tab 3: Custom food prices =================

        private static List<ThingDef> _cachedFoodDefs;
        private static readonly Dictionary<string, string> _searchTextCache = new Dictionary<string, string>();
        private string _priceSearch = "";
        private Vector2 _priceScroll = Vector2.zero;

        private void DrawFoodPricesTab(Rect rect)
        {
            float toolY = rect.y + 2f;
            Widgets.Label(new Rect(rect.x, toolY + 2f, 60f, 24f), "PPTE2_Search".Translate());
            _priceSearch = Widgets.TextField(new Rect(rect.x + 65f, toolY, 220f, 26f), _priceSearch);
            if (Widgets.ButtonText(new Rect(rect.x + 295f, toolY, 80f, 26f), "PPTE2_ResetSearch".Translate()))
                _priceSearch = "";
            if (Widgets.ButtonText(new Rect(rect.xMax - 150f, toolY, 150f, 26f), "PPTE2_ClearAllFoodPrices".Translate()))
                customFoodPrices.Clear();

            var listRect = new Rect(rect.x, toolY + 34f, rect.width, rect.yMax - toolY - 34f);
            if (_cachedFoodDefs == null) CacheFoodDefs();

            string norm = (_priceSearch ?? "").Trim().ToLowerInvariant();
            var filtered = new List<ThingDef>();
            foreach (var def in _cachedFoodDefs)
            {
                string text;
                if (!_searchTextCache.TryGetValue(def.defName, out text))
                {
                    text = ((def.label ?? "") + "\n" + (def.defName ?? "")).ToLowerInvariant();
                    _searchTextCache[def.defName] = text;
                }
                if (string.IsNullOrEmpty(norm) || text.Contains(norm))
                    filtered.Add(def);
            }

            const float rowH = 30f;
            const float headerH = 26f;
            var view = new Rect(0f, 0f, listRect.width - 20f, headerH + filtered.Count * rowH + 10f);
            Widgets.BeginScrollView(listRect, ref _priceScroll, view);

            const float iconW = 34f;
            const float baseW = 90f;
            const float inputW = 110f;
            const float pad = 8f;
            float nameW = view.width - iconW - baseW - inputW - pad * 3f;

            float x1 = pad;
            float x2 = x1 + iconW + pad;
            float x3 = x2 + nameW + pad;
            float x4 = x3 + baseW + pad;

            GUI.color = new Color(0.8f, 0.8f, 0.6f);
            Widgets.Label(new Rect(x2, 2f, nameW, 22f), "PPTE2_HdrFoodName".Translate());
            Widgets.Label(new Rect(x3, 2f, baseW, 22f), "PPTE2_HdrBaseMarketValue".Translate());
            Widgets.Label(new Rect(x4, 2f, inputW, 22f), "PPTE2_HdrCustomPrice".Translate());
            GUI.color = Color.white;
            Widgets.DrawLineHorizontal(0f, headerH - 2f, view.width);

            float y = headerH;
            for (int i = 0; i < filtered.Count; i++)
            {
                var def = filtered[i];
                if (i % 2 == 0) Widgets.DrawLightHighlight(new Rect(0f, y, view.width, rowH));

                Widgets.ThingIcon(new Rect(x1, y + 2f, 26f, 26f), def);
                Widgets.Label(new Rect(x2, y + 4f, nameW, 24f), def.LabelCap);

                float baseVal = def.BaseMarketValue > 0 ? def.BaseMarketValue
                    : (def.ingestible != null ? def.ingestible.CachedNutrition * 10f : 0f);
                Widgets.Label(new Rect(x3, y + 4f, baseW, 24f), baseVal.ToString("F1"));

                bool has = customFoodPrices.TryGetValue(def.defName, out float cur);
                string buf = has ? cur.ToString("0.##") : "";
                string newBuf = Widgets.TextField(new Rect(x4, y + 3f, inputW, 24f), buf);
                if (newBuf != buf)
                {
                    if (float.TryParse(newBuf, out float res) && res > 0f) customFoodPrices[def.defName] = res;
                    else customFoodPrices.Remove(def.defName);
                }
                y += rowH;
            }

            Widgets.EndScrollView();
        }

        private static void CacheFoodDefs()
        {
            _cachedFoodDefs = new List<ThingDef>();
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def == null) continue;
                if (def.ingestible == null) continue;
                if (def.category != ThingCategory.Item) continue;
                if (def.IsCorpse) continue;
                if (!def.IsNutritionGivingIngestible) continue;
                _cachedFoodDefs.Add(def);
            }
            _cachedFoodDefs.Sort((a, b) =>
                string.CompareOrdinal(a.label ?? a.defName, b.label ?? b.defName));
        }
    }
}