using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Surgery worker that removes one non-vital organ (kidney or one lung) and pays the prisoner
    /// meal tickets. Inherits <see cref="Recipe_RemoveBodyPart"/> which already handles surgery
    /// failure, spawning the removed organ item, and thoughts — we only restrict the target part,
    /// enforce one-sale-per-organ and pay out on success.
    /// </summary>
    public class RecipeWorker_OrganSale : Recipe_RemoveBodyPart
    {
        public string OrganKey
        {
            get
            {
                if (recipe == null) return null;
                if (recipe.defName == OrganHarvestHelper.RecipeDefKidney)   return "Kidney";
                if (recipe.defName == OrganHarvestHelper.RecipeDefLungLobe) return "LungLobe";
                return null;
            }
        }

        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            var organKey = OrganKey;
            if (organKey == null) yield break;
            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null) yield break;

            if (!mgr.CanHarvestOrgans(pawn)) yield break;     // global/per-prisoner toggle
            if (mgr.IsOrganTaken(pawn, organKey)) yield break; // already sold this organ

            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (!IsOurTarget(part, organKey)) continue;
                if (!MedicalRecipesUtility.IsCleanAndDroppable(pawn, part)) continue;
                if (!IsSafeToRemove(pawn, organKey)) continue; // keep at least one intact organ
                yield return part;
            }
        }

        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (!base.AvailableOnNow(thing, part)) return false;
            if (!(thing is Pawn p)) return true;
            if (!PrisonersPayToEat2Manager.Current?.CanHarvestOrgans(p) ?? true) return false;
            return true;
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            base.ApplyOnPawn(pawn, part, billDoer, ingredients, bill);
            if (pawn.Dead) return; // surgery failed or patient died: no payout
            if (part == null || !pawn.health.hediffSet.GetNotMissingParts().Contains(part))
            {
                OrganHarvestHelper.OnOrganRemoved(pawn, OrganKey); // part successfully removed
            }
        }

        private static bool IsOurTarget(BodyPartRecord part, string organKey)
        {
            if (part?.def == null) return false;
            if (organKey == "Kidney")   return part.def.defName == "Kidney";
            if (organKey == "LungLobe") return part.def.defName == "Lung";
            return false;
        }

        private static bool IsSafeToRemove(Pawn pawn, string organKey)
        {
            string targetDef = organKey == "LungLobe" ? "Lung" : "Kidney";
            int present = pawn.health.hediffSet.GetNotMissingParts()
                .Count(p => p.def != null && p.def.defName == targetDef);
            return present >= 2; // need at least 2 so one remains
        }
    }
}