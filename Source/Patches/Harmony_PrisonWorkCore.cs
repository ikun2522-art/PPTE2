using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Core patches of the built-in prisoner labour system:
    ///  1. JobGiver_Work.PawnCanUseWorkGiver — vanilla refuses every work giver for
    ///     non-colonists ("!giver.def.nonColonistsCanDo && !pawn.IsColonist"). Our postfix
    ///     re-allows work-enabled prisoners for the settings-allowed work types, re-running
    ///     the other vanilla checks (disabled work tags/types, ShouldSkip, capacities) so
    ///     only the colonist gate is bypassed.
    ///  2. Pawn_GuestTracker.SetGuestStatus — vanilla only creates outfits/drugs/timetable
    ///     trackers for player-faction pawns; prisoners don't get them. We add them on capture
    ///     (and lazily for existing prisoners via the manager's periodic sweep), plus
    ///     work-settings initialisation with prisoner default priorities.
    ///  3. WorkGiver_ConstructFinishFrames.JobOnThing — vanilla's first check is
    ///     "t.Faction != pawn.Faction" and prisoner pawns keep their original (hostile)
    ///     faction, so they could never finish colony building frames. The postfix re-runs the
    ///     vanilla logic minus the faction equality for our working prisoners.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_Work), "PawnCanUseWorkGiver")]
    public static class Patch_PawnCanUseWorkGiver
    {
        [HarmonyPostfix]
        public static void Postfix(ref bool __result, Pawn pawn, WorkGiver giver)
        {
            if (__result) return;
            if (!PrisonerWorkSystem.IsWorkEnabled(pawn)) return;
            var def = giver?.def;
            if (def?.workType == null) return;
            if (!PrisonerWorkSystem.IsAllowedWorkType(def.workType)) return;
            if (pawn.WorkTagIsDisabled(def.workTags)) return;
            if (pawn.WorkTypeIsDisabled(def.workType)) return;
            if (giver.ShouldSkip(pawn)) return;
            if (giver.MissingRequiredCapacity(pawn) != null) return;
            __result = true;
        }
    }

    [HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus))]
    public static class Patch_GuestStatusPrisonerSetup
    {
        private static readonly AccessTools.FieldRef<Pawn_GuestTracker, Pawn> PawnRef =
            AccessTools.FieldRefAccess<Pawn_GuestTracker, Pawn>("pawn");

        [HarmonyPostfix]
        public static void Postfix(Pawn_GuestTracker __instance, Faction newHost, GuestStatus guestStatus)
        {
            if (guestStatus != GuestStatus.Prisoner) return;
            if (newHost != Faction.OfPlayer) return;
            PrisonerWorkSystem.EnsurePrisonerSetup(PawnRef(__instance));
        }
    }

    [HarmonyPatch(typeof(WorkGiver_ConstructFinishFrames), nameof(WorkGiver_ConstructFinishFrames.JobOnThing))]
    public static class Patch_ConstructFinishFramesFaction
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, Thing t, bool forced, ref Job __result)
        {
            if (__result != null) return;
            if (!PrisonerWorkSystem.IsWorkEnabled(pawn)) return;
            // vanilla bailed out because the frame belongs to the colony and the prisoner
            // keeps their original faction; redo the vanilla checks without that one line
            if (t == null || t.Faction != Faction.OfPlayer) return;
            if (!(t is Frame frame) || !frame.IsCompleted()) return;
            if (!GenConstruct.CanTouchTargetFromValidCell(frame, pawn)) return;
            if (GenConstruct.FirstBlockingThing(frame, pawn) != null)
            {
                __result = GenConstruct.HandleBlockingThingJob(frame, pawn, forced);
                return;
            }
            if (!GenConstruct.CanConstruct(frame, pawn, checkSkills: true, forced)) return;
            __result = JobMaker.MakeJob(JobDefOf.FinishFrame, frame);
        }
    }
}
