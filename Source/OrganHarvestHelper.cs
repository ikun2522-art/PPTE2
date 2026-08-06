using RimWorld;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Pay-out side of non-lethal organ harvesting. The surgical recipes are defined in XML;
    /// <see cref="RecipeWorker_OrganSale"/> gates per-pawn availability and calls
    /// <see cref="OnOrganRemoved"/> when a harvest succeeds.
    /// </summary>
    public static class OrganHarvestHelper
    {
        public const string RecipeDefKidney = "PPTE2_RemoveKidney";
        public const string RecipeDefLungLobe = "PPTE2_RemoveLungLobe";

        public static int OrganPayout(string organKey) => organKey switch
        {
            "Kidney" => 40,
            "LungLobe" => 35,
            _ => 30
        };

        public static void OnOrganRemoved(Pawn prisoner, string organKey)
        {
            if (prisoner == null) return;
            var mgr = PrisonersPayToEat2Manager.Current;
            mgr.MarkOrganTaken(prisoner, organKey);
            int pay = OrganPayout(organKey);
            mgr.AddTickets(prisoner, pay);
            if (PrisonersPayToEat2Mod.Settings.logVerbose)
                Log.Message($"[PPTE2] {prisoner.LabelShortCap} sold {organKey} for {pay} tickets. Balance={mgr.Balance(prisoner)}");

            var msg = "PPTE2_OrganSold".Translate(prisoner.LabelShortCap, organKey, pay, PPTEName.Ticket);
            Messages.Message(msg, prisoner, MessageTypeDefOf.NeutralEvent);
        }
    }
}