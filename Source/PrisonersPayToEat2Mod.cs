using HarmonyLib;
using Verse;
using System.Reflection;

namespace PrisonersPayToEat2
{
    public class PrisonersPayToEat2Mod : Mod
    {
        public const string HarmonyId = "aaa.prisonerspaytoeat2";
        public const string ModVersion = "2.0.1";

        public static PrisonersPayToEat2Settings Settings;
        public static PrisonersPayToEat2Mod Instance;

        public PrisonersPayToEat2Mod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<PrisonersPayToEat2Settings>();

            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.Message($"[PrisonersPayToEat2] v{ModVersion} loaded (RimWorld 1.6). PrisonLabor={PrisonLaborBridge.Present}, OrganHarvest={Settings.enableOrganHarvest}, FoodMul={Settings.foodPriceMultiplier}, WageMul={Settings.wageMultiplier}");
        }

        public override string SettingsCategory() => "PPTE2_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }
    }
}