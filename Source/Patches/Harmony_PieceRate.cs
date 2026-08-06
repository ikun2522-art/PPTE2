using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// All piece-rate settlement hooks (see <see cref="PieceRateWorker"/> for the unit mapping):
    /// production bills, mining, construction, growing/plant-cutting, cleaning, hauling, research,
    /// plus the generic per-job fallback for unknown work types.
    /// Every hook reads the work type from the pawn's current job at settlement time, so work types
    /// added by other MODs are picked up automatically.
    /// </summary>
    public static class Harmony_PieceRate
    {
        /// <summary>Production tables: one completed recipe batch.</summary>
        [HarmonyPatch(typeof(RecordsUtility), nameof(RecordsUtility.Notify_BillDone))]
        public static class Patch_BillDone
        {
            [HarmonyPostfix]
            static void Postfix(Pawn billDoer)
            {
                var wt = PrisonLaborBridge.CurrentWorkTypeDef(billDoer);
                if (wt != null) PieceRateWorker.Credit(billDoer, wt, 1f);
            }
        }

        /// <summary>Mining: one mined deposit.</summary>
        [HarmonyPatch(typeof(Mineable), nameof(Mineable.DestroyMined))]
        public static class Patch_Mineable
        {
            [HarmonyPostfix]
            static void Postfix(Pawn pawn)
            {
                var wt = PrisonLaborBridge.CurrentWorkTypeDef(pawn);
                if (wt != null) PieceRateWorker.Credit(pawn, wt, 1f);
            }
        }

        /// <summary>Construction: one finished building / floor.</summary>
        [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
        public static class Patch_Frame
        {
            [HarmonyPostfix]
            static void Postfix(Pawn worker)
            {
                var wt = PrisonLaborBridge.CurrentWorkTypeDef(worker);
                if (wt != null) PieceRateWorker.Credit(worker, wt, 1f);
            }
        }

        /// <summary>Growing harvest + PlantCutting: one harvested / cut plant.</summary>
        [HarmonyPatch(typeof(Plant), nameof(Plant.PlantCollected))]
        public static class Patch_Plant
        {
            [HarmonyPostfix]
            static void Postfix(Pawn by)
            {
                var wt = PrisonLaborBridge.CurrentWorkTypeDef(by);
                if (wt != null) PieceRateWorker.Credit(by, wt, 1f);
            }
        }

        /// <summary>
        /// Sowing (PlantsSown) and Cleaning (MessesCleaned). These jobs process target queues, so the
        /// record is the only exact "one unit done" event; rain-washed filth never touches the record.
        /// </summary>
        [HarmonyPatch(typeof(Pawn_RecordsTracker), nameof(Pawn_RecordsTracker.Increment), new Type[] { typeof(RecordDef) })]
        public static class Patch_Records
        {
            [HarmonyPostfix]
            static void Postfix(Pawn_RecordsTracker __instance, RecordDef def)
            {
                var pawn = __instance?.pawn;
                if (pawn == null) return;
                if (def != RecordDefOf.PlantsSown && def != RecordDefOf.MessesCleaned) return;
                var wt = PrisonLaborBridge.CurrentWorkTypeDef(pawn);
                if (wt != null) PieceRateWorker.Credit(pawn, wt, 1f);
            }
        }

        /// <summary>
        /// Hauling: one carried stack placed into storage/container (ThingPlaceMode.Direct while
        /// executing a Hauling job). Dropped-at-feet and failed-haul drops use ThingPlaceMode.Near,
        /// so they never count. DoBill ingredient drops are excluded by the Hauling work-type check.
        /// TargetMethod resolves the exact overload (the method has an out param, which cannot be
        /// expressed in the patch attribute).
        /// </summary>
        [HarmonyPatch]
        public static class Patch_CarryDrop
        {
            static System.Reflection.MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryDropCarriedThing),
                    new Type[] { typeof(IntVec3), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(Action<Thing, int>) });
            }

            [HarmonyPostfix]
            static void Postfix(Pawn_CarryTracker __instance, ThingPlaceMode mode)
            {
                if (mode != ThingPlaceMode.Direct) return;
                var pawn = __instance?.pawn;
                if (pawn == null) return;
                var wt = PrisonLaborBridge.CurrentWorkTypeDef(pawn);
                if (wt != null && wt.defName == "Hauling") PieceRateWorker.Credit(pawn, wt, 1f);
            }
        }

        /// <summary>Research: split the per-tech wage among contributors when a project completes.</summary>
        [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
        public static class Patch_Research
        {
            [HarmonyPostfix]
            static void Postfix(ResearchProjectDef proj)
            {
                PieceRateWorker.DistributeResearch(proj);
            }
        }

        /// <summary>
        /// Generic fallback: one completed work job = one unit, for work types without a dedicated
        /// unit (e.g. work types added by other MODs). The prefix snapshots the ending JobDriver
        /// because EndCurrentJob clears curJob/curDriver during cleanup.
        /// </summary>
        [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob))]
        public static class Patch_JobDone
        {
            [HarmonyPrefix]
            static void Prefix(Pawn_JobTracker __instance, JobCondition condition, out JobDriver __state)
            {
                __state = condition == JobCondition.Succeeded ? __instance.curDriver : null;
            }

            [HarmonyPostfix]
            static void Postfix(JobCondition condition, JobDriver __state)
            {
                if (__state == null || condition != JobCondition.Succeeded) return;
                var pawn = __state.pawn;
                var wt = __state.job?.workGiverDef?.workType;
                PieceRateWorker.CreditJobDone(pawn, wt);
            }
        }
    }
}
