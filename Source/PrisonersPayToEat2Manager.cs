using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
        public PieceRateMode wageMode = PieceRateMode.Follow; // per-prisoner hourly/piece-rate override
        public bool organHarvestOverride;     // explicit per-prisoner override of the global toggle
        public bool organHarvestOverrideEnabled;
        public bool kidneyTaken;
        public bool lungLobeTaken;
        public float ransomTicketOverride;    // ransom tickets needed; 0 = follow global setting
        public float ransomMinDaysOverride;   // minimum days imprisoned; 0 = follow global setting
        public bool ransomRequested;          // prisoner asked to buy freedom, awaiting player decision
        public int ransomDeniedUntilTick = -1; // rejection cooldown (TicksGame); -1 = none

        public void ExposeData()
        {
            Scribe_Values.Look(ref ticketBalance, "ticketBalance", 0f);
            Scribe_Values.Look(ref foodMultiplier, "foodMultiplier", 1.0f);
            Scribe_Values.Look(ref wageMultiplier, "wageMultiplier", 1.0f);
            Scribe_Values.Look(ref wageMode, "wageMode", PieceRateMode.Follow);
            Scribe_Values.Look(ref organHarvestOverride, "organHarvestOverride", false);
            Scribe_Values.Look(ref organHarvestOverrideEnabled, "organHarvestOverrideEnabled", false);
            Scribe_Values.Look(ref kidneyTaken, "kidneyTaken", false);
            Scribe_Values.Look(ref lungLobeTaken, "lungLobeTaken", false);
            Scribe_Values.Look(ref ransomTicketOverride, "ransomTicketOverride", 0f);
            Scribe_Values.Look(ref ransomMinDaysOverride, "ransomMinDaysOverride", 0f);
            Scribe_Values.Look(ref ransomRequested, "ransomRequested", false);
            Scribe_Values.Look(ref ransomDeniedUntilTick, "ransomDeniedUntilTick", -1);
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

        // Transient (not serialized) research contribution tracking for piece-rate billing:
        // project -> contributing pawn -> research speed * ticks. Cleared on load and when a
        // project completes (see PieceRateWorker.DistributeResearch).
        public Dictionary<ResearchProjectDef, Dictionary<Pawn, float>> researchContrib =
            new Dictionary<ResearchProjectDef, Dictionary<Pawn, float>>();

        // Tick (TicksGame) at which each pawn became a prisoner of the colony, keyed by
        // pawn.thingIDNumber. Used for the ransom minimum-imprisonment-time requirement.
        private Dictionary<int, int> prisonStartTick = new Dictionary<int, int>();

        // Meal-ticket health-status hediff maintenance (see EnsureTicketHediffs).
        private static HediffDef ticketHediffDef;
        private int lastHediffCheckTick = -1;

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

        // ================= Ransom (赎身) =================

        /// <summary>Tickets the prisoner must pay to buy freedom (per-prisoner override wins).</summary>
        public float EffectiveRansomCost(Pawn p)
        {
            var d = DataFor(p);
            if (d.ransomTicketOverride > 0f) return d.ransomTicketOverride;
            return PrisonersPayToEat2Mod.Settings.ransomTicketCost;
        }

        /// <summary>Minimum days imprisoned before ransom is allowed (override wins).</summary>
        public float EffectiveRansomMinDays(Pawn p)
        {
            var d = DataFor(p);
            if (d.ransomMinDaysOverride > 0f) return d.ransomMinDaysOverride;
            return PrisonersPayToEat2Mod.Settings.ransomMinDays;
        }

        /// <summary>Whole days since the pawn became a prisoner (0 when unknown).</summary>
        public float ImprisonedDays(Pawn p)
        {
            if (p == null) return 0f;
            if (!prisonStartTick.TryGetValue(p.thingIDNumber, out int start)) return 0f;
            return Mathf.Max(0f, (Find.TickManager.TicksGame - start) / 60000f);
        }

        /// <summary>True when balance and imprisonment-time requirements are met.</summary>
        public bool CanRansom(Pawn p)
        {
            if (p == null || !p.IsPrisonerOfColony) return false;
            if (Balance(p) < EffectiveRansomCost(p)) return false;
            if (ImprisonedDays(p) < EffectiveRansomMinDays(p)) return false;
            return true;
        }

        public bool HasPrisonStartTick(int pawnId) => prisonStartTick.ContainsKey(pawnId);
        public void SetPrisonStartTick(int pawnId, int tick) => prisonStartTick[pawnId] = tick;
        public void RemovePrisonStartTick(int pawnId) => prisonStartTick.Remove(pawnId);

        /// <summary>Forget imprisonment start for pawns that are no longer prisoners (released/recruited/escaped/dead).</summary>
        public void PrunePrisonStartTicks(HashSet<int> livePrisonerIds)
        {
            if (livePrisonerIds == null) return;
            var stale = new List<int>();
            foreach (int id in prisonStartTick.Keys)
                if (!livePrisonerIds.Contains(id)) stale.Add(id);
            foreach (int id in stale) prisonStartTick.Remove(id);
        }

        /// <summary>Clear per-session request state when a pawn starts a fresh imprisonment.</summary>
        public void ResetRansomSession(int pawnId)
        {
            if (!data.TryGetValue(pawnId, out var d)) return;
            d.ransomRequested = false;
            d.ransomDeniedUntilTick = -1;
        }

        /// <summary>The prisoner asks to buy freedom; the player must approve before release.</summary>
        public void RequestRansom(Pawn p)
        {
            var d = DataFor(p);
            if (d.ransomRequested) return;
            d.ransomRequested = true;
            float cost = EffectiveRansomCost(p);
            Messages.Message("PPTE2_RansomRequestMsg".Translate(
                p.LabelShortCap, cost.ToString("0.##"), PPTEName.Ticket), p, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>
        /// Player approves the ransom: the prisoner pays the tickets and is released immediately
        /// (same as a warden releasing them). Returns false (and voids the request) when the
        /// requirements are no longer met, e.g. the balance dropped since the request.
        /// </summary>
        public bool TryApproveRansom(Pawn p)
        {
            var d = DataFor(p);
            if (!d.ransomRequested) return false;
            if (!CanRansom(p))
            {
                d.ransomRequested = false;
                d.ransomDeniedUntilTick = -1;
                Messages.Message("PPTE2_RansomNoLongerEligible".Translate(p.LabelShortCap),
                    p, MessageTypeDefOf.RejectInput);
                return false;
            }

            float cost = EffectiveRansomCost(p);
            TryPay(p, cost);
            d.ransomRequested = false;
            d.ransomDeniedUntilTick = -1;
            prisonStartTick.Remove(p.thingIDNumber);
            GenGuest.PrisonerRelease(p); // automatic release
            Messages.Message("PPTE2_RansomApproved".Translate(
                p.LabelShortCap, cost.ToString("0.##"), PPTEName.Ticket), p, MessageTypeDefOf.PositiveEvent);
            return true;
        }

        /// <summary>Player rejects the request; the prisoner may ask again after a cooldown.</summary>
        public void RejectRansom(Pawn p)
        {
            var d = DataFor(p);
            d.ransomRequested = false;
            d.ransomDeniedUntilTick = Find.TickManager.TicksGame + RansomTicker.DenyCooldownTicks;
            Messages.Message("PPTE2_RansomRejected".Translate(p.LabelShortCap),
                p, MessageTypeDefOf.NeutralEvent);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref data, "data", LookMode.Value, LookMode.Deep);
            if (data == null) data = new Dictionary<int, PrisonerTicketData>();
            Scribe_Collections.Look(ref workWageAccum, "workWageAccum", LookMode.Value, LookMode.Value);
            if (workWageAccum == null) workWageAccum = new Dictionary<int, float>();
            Scribe_Collections.Look(ref prisonStartTick, "prisonStartTick", LookMode.Value, LookMode.Value);
            if (prisonStartTick == null) prisonStartTick = new Dictionary<int, int>();
            // research contributions are transient; drop stale refs on load
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                researchContrib.Clear();
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            PrisonLaborWageTicker.Tick();
            RansomTicker.Tick();
            EnsureTicketHediffs();
        }

        /// <summary>
        /// Keeps the meal-ticket "health status" hediff in sync: adds it to every colony prisoner
        /// (so the balance shows in their health tab) and removes leftovers from pawns that are no
        /// longer prisoners (released / recruited / escaped / dead).
        /// </summary>
        private void EnsureTicketHediffs()
        {
            int now = Find.TickManager.TicksGame;
            if (now - lastHediffCheckTick < 120) return; // check every 0.05 day
            lastHediffCheckTick = now;

            if (ticketHediffDef == null)
            {
                // not cached while defs are still loading (null is retried next check)
                ticketHediffDef = DefDatabase<HediffDef>.GetNamedSilentFail("PPTE2_MealTickets");
                if (ticketHediffDef == null) return;
            }

            foreach (var map in Find.Maps)
            {
                if (map == null) continue;

                var prisoners = map.mapPawns.PrisonersOfColony;
                for (int i = 0; i < prisoners.Count; i++)
                {
                    var pawn = prisoners[i];
                    if (pawn == null || pawn.Dead) continue;
                    var set = pawn.health?.hediffSet;
                    if (set == null) continue;
                    if (set.GetFirstHediffOfDef(ticketHediffDef) == null)
                        pawn.health.AddHediff(HediffMaker.MakeHediff(ticketHediffDef, pawn));
                }

                // sweep spawned pawns for leftover ticket hediffs (no longer prisoners)
                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (pawn == null || pawn.Dead) continue;
                    if (pawn.IsPrisonerOfColony) continue;
                    var set = pawn.health?.hediffSet;
                    if (set == null) continue;
                    var hediff = set.GetFirstHediffOfDef(ticketHediffDef);
                    if (hediff != null) pawn.health.RemoveHediff(hediff);
                }
            }
        }
    }
}