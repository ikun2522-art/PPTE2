using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Built-in prisoner labour system (v2.3+). Active only when enabled in settings AND
    /// Prison Labor is NOT installed (Prison Labor then keeps its role as the labour provider).
    ///
    /// How it works: a think-tree subtree (Defs/ThinkTreeDefs/PPTE2_ThinkTrees.xml) is injected
    /// into Humanlike_PostDuty via the vanilla insertTag mechanism. Its gate node
    /// (<see cref="ThinkNode_PrisonerWorkGate"/>) checks <see cref="IsWorkEnabled"/>; inside,
    /// vanilla JobGiver_Work does the actual scanning. Vanilla's JobGiver_Work.PawnCanUseWorkGiver
    /// blocks non-colonists, so a Harmony postfix (Harmony_PrisonWorkCore) whitelists our
    /// work-enabled prisoners for the allowed work types. Everything downstream (job drivers,
    /// piece-rate hooks, hourly wage detection via job.workGiverDef) then works unchanged.
    /// </summary>
    public static class PrisonerWorkSystem
    {
        /// <summary>Default allowed work types ("粗活"): unskilled physical labour.</summary>
        public static List<string> DefaultWorkTypes() => new List<string>
        {
            "Mining", "Construction", "Growing", "PlantCutting", "Hauling", "Cleaning"
        };

        /// <summary>
        /// Work types prisoners can never do, regardless of settings: violent/sensitive/self-care
        /// types (wardens feeding themselves, doctors, hunting with weapons, firefighting etc.).
        /// These are hidden from the settings list entirely.
        /// </summary>
        public static readonly HashSet<string> NeverAllowedWorkTypes = new HashSet<string>
        {
            "Firefighter", "Patient", "PatientBedRest", "Doctor", "Warden", "Handling",
            "BasicWorker", "Hunting"
        };

        /// <summary>Built-in labour system is on (settings toggle) and Prison Labor is absent.</summary>
        public static bool Active
        {
            get
            {
                var s = PrisonersPayToEat2Mod.Settings;
                return s != null && s.enableBuiltinPrisonLabor && !PrisonLaborBridge.Present;
            }
        }

        /// <summary>
        /// Whether work wages can be earned at all right now: either Prison Labor provides the
        /// labour, or our built-in system does. Replaces the old "!PrisonLaborBridge.Present ->
        /// no income" gate in the wage ticker and piece-rate settlement.
        /// </summary>
        public static bool WorkIncomeEnabled => PrisonLaborBridge.Present || Active;

        /// <summary>
        /// Is this pawn currently supposed to be working for the colony as a prisoner?
        /// Cheap enough to be called from the think-tree gate on every think scan.
        /// </summary>
        public static bool IsWorkEnabled(Pawn pawn)
        {
            if (!Active) return false;
            if (pawn == null || pawn.Dead || !pawn.Spawned) return false;
            if (!pawn.IsPrisonerOfColony) return false;
            if (!pawn.RaceProps.Humanlike) return false;
            var map = pawn.MapHeld;
            if (map == null || !map.IsPlayerHome) return false;
            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null) return false;
            switch (mgr.DataFor(pawn).workSetting)
            {
                case PPTE2SocialSetting.Allow: return true;
                case PPTE2SocialSetting.Deny: return false;
                default: return PrisonersPayToEat2Mod.Settings.prisonerWorkDefaultOn;
            }
        }

        /// <summary>Global allow-list of work types prisoners may do (settings-driven).</summary>
        public static bool IsAllowedWorkType(WorkTypeDef wt)
        {
            if (wt == null) return false;
            if (NeverAllowedWorkTypes.Contains(wt.defName)) return false;
            var s = PrisonersPayToEat2Mod.Settings;
            return s != null && s.prisonerAllowedWorkTypes.Contains(wt.defName);
        }

        /// <summary>
        /// Motivation gate (v2.3 motivation system): when the motivation need exists and is
        /// enabled, prisoners below the laziness threshold refuse to work. Kept def-name based
        /// so the think-tree gate compiles/loads even before the need is touched elsewhere.
        /// </summary>
        public static bool MotivationAllowsWork(Pawn pawn)
        {
            var s = PrisonersPayToEat2Mod.Settings;
            if (s == null || !s.enableMotivation) return true;
            float level = MotivationLevel(pawn);
            if (level < 0f) return true; // need not present (def missing / non-humanlike)
            return level >= s.motivationWorkThreshold;
        }

        /// <summary>Current motivation level 0..1, or -1 when the need isn't available.</summary>
        public static float MotivationLevel(Pawn pawn)
        {
            if (pawn?.needs == null) return -1f;
            var def = DefDatabase<NeedDef>.GetNamedSilentFail("PPTE2_Motivation");
            if (def == null) return -1f;
            var need = pawn.needs.TryGetNeed(def);
            return need?.CurLevel ?? -1f;
        }

        /// <summary>
        /// Gives a colony prisoner the pawn trackers vanilla withholds from non-player-faction
        /// pawns (outfits/drugs/timetable) and initialises work settings with prisoner defaults.
        /// Called from the SetGuestStatus patch on capture and from a periodic sweep
        /// (covers old saves and any capture path that skips SetGuestStatus).
        /// </summary>
        public static void EnsurePrisonerSetup(Pawn pawn)
        {
            if (!Active) return;
            if (pawn == null || pawn.Dead || !pawn.IsPrisonerOfColony) return;
            if (!pawn.RaceProps.Humanlike) return;
            if (pawn.outfits == null) pawn.outfits = new Pawn_OutfitTracker(pawn);
            if (pawn.drugs == null) pawn.drugs = new Pawn_DrugPolicyTracker(pawn);
            if (pawn.timetable == null) pawn.timetable = new Pawn_TimetableTracker(pawn);
            var ws = pawn.workSettings;
            if (ws != null && !ws.EverWork)
            {
                ws.EnableAndInitializeIfNotAlreadyInitialized();
                ApplyDefaultPriorities(pawn);
            }
        }

        /// <summary>Default prisoner priorities: everything off, allowed work types at normal (3).</summary>
        public static void ApplyDefaultPriorities(Pawn pawn)
        {
            var ws = pawn?.workSettings;
            if (ws == null || !ws.EverWork) return;
            ws.DisableAll();
            foreach (var wt in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (!IsAllowedWorkType(wt)) continue;
                if (pawn.WorkTypeIsDisabled(wt)) continue;
                ws.SetPriority(wt, Pawn_WorkSettings.DefaultPriority);
            }
        }

        /// <summary>
        /// 设置里改动允许工种/总开关后调用：把允许列表同步到所有在押囚犯的优先级
        /// （新允许的工种置 3，不再允许的置 0；会覆盖玩家对这两类工种的个别调整，属预期语义）。
        /// </summary>
        public static void OnAllowedTypesChanged()
        {
            if (!Active || Verse.Current.Game == null) return;
            foreach (var map in Find.Maps)
            {
                if (map == null) continue;
                foreach (var pawn in map.mapPawns.PrisonersOfColony)
                {
                    EnsurePrisonerSetup(pawn);
                    var ws = pawn.workSettings;
                    if (ws == null || !ws.EverWork) continue;
                    foreach (var wt in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                    {
                        if (pawn.WorkTypeIsDisabled(wt)) continue;
                        bool allowed = IsAllowedWorkType(wt);
                        int cur = ws.GetPriority(wt);
                        if (allowed && cur == 0) ws.SetPriority(wt, Pawn_WorkSettings.DefaultPriority);
                        else if (!allowed && cur > 0) ws.SetPriority(wt, 0);
                    }
                }
            }
        }
    }
}
