using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Per-prisoner state. Stored in <see cref="PrisonersPayToEat2Manager"/> keyed by Pawn.thingIDNumber.
    /// </summary>
    public class PrisonerTicketData : IExposable
    {
        public float ticketBalance;
        public float foodMultiplier = 1.0f;   // overrides global food price multiplier
        public float wageMultiplier = 1.0f;   // overrides global wage multiplier
        public bool organHarvestOverride;     // explicit per-prisoner override of the global toggle
        public bool organHarvestOverrideEnabled;
        public bool kidneyTaken;
        public bool lungLobeTaken;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ticketBalance, "ticketBalance", 0f);
            Scribe_Values.Look(ref foodMultiplier, "foodMultiplier", 1.0f);
            Scribe_Values.Look(ref wageMultiplier, "wageMultiplier", 1.0f);
            Scribe_Values.Look(ref organHarvestOverride, "organHarvestOverride", false);
            Scribe_Values.Look(ref organHarvestOverrideEnabled, "organHarvestOverrideEnabled", false);
            Scribe_Values.Look(ref kidneyTaken, "kidneyTaken", false);
            Scribe_Values.Look(ref lungLobeTaken, "lungLobeTaken", false);
        }
    }

    /// <summary>
    /// Central store of all prisoner ticket balances and per-prisoner settings.
    /// Survives save/load as a GameComponent.
    /// </summary>
    public class PrisonersPayToEat2Manager : GameComponent
    {
        private Dictionary<int, PrisonerTicketData> data = new Dictionary<int, PrisonerTicketData>();

        // Fractional wage accumulator for Prison Labor work, keyed by pawn.thingIDNumber.
        // We credit tickets continuously as work ticks accumulate (see PrisonLaborWageTicker),
        // so a prisoner never misses pay just because the hourly snapshot happened mid-lunch.
        private Dictionary<int, float> workWageAccum = new Dictionary<int, float>();

        public PrisonersPayToEat2Manager() { }
        public PrisonersPayToEat2Manager(Game game) { }

        public static PrisonersPayToEat2Manager For(Game game) => game.GetComponent<PrisonersPayToEat2Manager>();
        public static PrisonersPayToEat2Manager Current => For(Verse.Current.Game);

        public PrisonerTicketData DataFor(Pawn p)
        {
            if (p == null) return null;
            int id = p.thingIDNumber;
            if (!data.TryGetValue(id, out var d))
            {
                d = new PrisonerTicketData { ticketBalance = PrisonersPayToEat2Mod.Settings.startingTicketsPerPrisoner };
                data[id] = d;
            }
            return d;
        }

        public void AddTickets(Pawn p, float amount)
        {
            if (amount == 0f) return;
            var d = DataFor(p);
            d.ticketBalance += amount;
            if (d.ticketBalance < 0f) d.ticketBalance = 0f;
        }

        public float Balance(Pawn p) => DataFor(p)?.ticketBalance ?? 0f;

        public float GetWorkWageAccum(int pawnId)
        {
            return workWageAccum.TryGetValue(pawnId, out float v) ? v : 0f;
        }

        public void SetWorkWageAccum(int pawnId, float value)
        {
            if (value <= 0.0001f) workWageAccum.Remove(pawnId);
            else workWageAccum[pawnId] = value;
        }

        public bool TryPay(Pawn p, float cost)
        {
            if (cost <= 0f) return true;
            var d = DataFor(p);
            if (d.ticketBalance < cost) return false;
            d.ticketBalance -= cost;
            return true;
        }

        public float EffectiveFoodMultiplier(Pawn p) => DataFor(p)?.foodMultiplier ?? 1.0f;
        public float EffectiveWageMultiplier(Pawn p) => DataFor(p)?.wageMultiplier ?? 1.0f;

        public bool CanHarvestOrgans(Pawn p)
        {
            var s = PrisonersPayToEat2Mod.Settings;
            var d = DataFor(p);
            if (d.organHarvestOverrideEnabled) return d.organHarvestOverride;
            return s.enableOrganHarvest;
        }

        // Mark organ taken after a successful surgery, prevents re-harvest
        public void MarkOrganTaken(Pawn p, string organKey)
        {
            var d = DataFor(p);
            if (organKey == "Kidney") d.kidneyTaken = true;
            else if (organKey == "LungLobe") d.lungLobeTaken = true;
        }

        public bool IsOrganTaken(Pawn p, string organKey)
        {
            var d = DataFor(p);
            return organKey == "Kidney" ? d.kidneyTaken
                 : organKey == "LungLobe" ? d.lungLobeTaken
                 : false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref data, "data", LookMode.Value, LookMode.Deep);
            if (data == null) data = new Dictionary<int, PrisonerTicketData>();
            Scribe_Collections.Look(ref workWageAccum, "workWageAccum", LookMode.Value, LookMode.Value);
            if (workWageAccum == null) workWageAccum = new Dictionary<int, float>();
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            PrisonLaborWageTicker.Tick();
        }
    }
}