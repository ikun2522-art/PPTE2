using RimWorld;
using Verse;
using Verse.AI;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Gate node of the injected prisoner-work think subtree (see Defs/ThinkTreeDefs).
    /// When satisfied, the prisoner uses the colonist-style needs/work priority sorter
    /// (food/rest/joy beat work when urgent, exactly like colonists); otherwise evaluation
    /// falls through to the vanilla prisoner branch, so a lazy/motivation-starved prisoner
    /// simply idles in their cell as usual.
    /// </summary>
    public class ThinkNode_PrisonerWorkGate : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            if (!PrisonerWorkSystem.IsWorkEnabled(pawn)) return false;
            if (pawn.InMentalState || pawn.Downed) return false;
            // wounded/sick prisoners fall through to the prisoner branch's medical bed-rest
            if (HealthAIUtility.ShouldSeekMedicalRest(pawn)) return false;
            if (!PrisonerWorkSystem.MotivationAllowsWork(pawn)) return false;
            return true;
        }
    }
}
