using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Centralised meal-ticket display name. The player can override it in the mod settings
    /// (default: localized "meal ticket"/"饭票"). All UI text goes through this so renaming
    /// applies everywhere instantly.
    /// </summary>
    public static class PPTEName
    {
        /// <summary>Custom name from settings, or empty when the player hasn't set one.</summary>
        public static string Custom
        {
            get => PrisonersPayToEat2Mod.Settings?.customTicketName ?? "";
        }

        /// <summary>Effective display name of a single ticket.</summary>
        public static string Ticket => Custom.NullOrEmpty() ? "PPTE2_TicketDefaultName".Translate() : Custom;

        /// <summary>Used as a unit, e.g. "12 饭票".</summary>
        public static string TicketUnit => Custom.NullOrEmpty() ? "PPTE2_TicketDefaultUnit".Translate() : Custom;

        /// <summary>"饭票余额" style balance label with the custom name.</summary>
        public static string BalanceTitle => "PPTE2_BalanceCardTitleFormat".Translate(Ticket);
    }
}