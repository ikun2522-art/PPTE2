using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Bridges Avius's "Prison Labor" MOD (packageId avius.prisonlabor).
    /// Prison Labor decides which work types a prisoner may do; we just detect "is currently doing
    /// a work job" via <see cref="CurrentWorkTypeDef"/> and pay the wage configured for that type.
    /// (Prison Labor's Need_Motivation.IsPrisonerWorking is never actually set true, so we can't use it.)
    /// </summary>
    public static class PrisonLaborBridge
    {
        public const string PackageId = "avius.prisonlabor";

        private static bool? _present;
        public static bool Present
        {
            get
            {
                if (_present.HasValue) return _present.Value;
                _present = false;
                foreach (var mc in LoadedModManager.RunningModsListForReading)
                {
                    if (mc.PackageId == PackageId) { _present = true; break; }
                }
                return _present.Value;
            }
        }

        /// <summary>True while the prisoner runs a WorkGiver job (mental breaks/escapes excluded).</summary>
        public static bool IsWorking(Pawn pawn)
        {
            if (pawn == null || pawn.Dead) return false;
            if (pawn.InMentalState) return false;
            return CurrentWorkTypeDef(pawn) != null;
        }

        /// <summary>WorkTypeDef of the pawn's current work job, or null when not working.</summary>
        public static WorkTypeDef CurrentWorkTypeDef(Pawn pawn)
        {
            var job = pawn?.jobs?.curJob;
            if (job == null) return null;
            var wg = job.workGiverDef;
            return wg?.workType;
        }
    }

    /// <summary>
    /// Pays out hourly work wages. Runs from <see cref="PrisonersPayToEat2Manager"/>.
    /// Fractions accumulate per tick and are credited immediately (>=0.01) so short shifts still pay;
    /// a leftover fraction is flushed the moment a work session ends.
    /// </summary>
    public static class PrisonLaborWageTicker
    {
        private const int TicksPerHour = 2500;
        private const int CheckStride = 10; // check every 10 ticks to avoid per-tick GC churn

        // remembers whether a pawn was working at the last check, to settle the accumulator
        // the moment a work session ends
        private static readonly Dictionary<int, bool> wasWorking = new Dictionary<int, bool>();

        public static void Tick()
        {
            if (!PrisonLaborBridge.Present) return;
            var game = Current.Game;
            if (game == null) return;
            var settings = PrisonersPayToEat2Mod.Settings;
            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null) return;

            int ticks = game.tickManager.TicksGame;
            if (ticks % CheckStride != 0) return;
            bool hourlySettle = ticks % TicksPerHour == 0;

            foreach (var map in game.Maps)
            {
                if (map == null) continue;
                var list = map.mapPawns.PrisonersOfColony;
                for (int i = 0; i < list.Count; i++)
                {
                    var pawn = list[i];
                    if (pawn == null || pawn.Dead) continue;

                    int id = pawn.thingIDNumber;
                    bool workingNow = PrisonLaborBridge.IsWorking(pawn);

                    if (workingNow)
                    {
                        string workTypeDefName = PrisonLaborBridge.CurrentWorkTypeDef(pawn)?.defName;
                        float wagePerHour = settings.GetWageForWorkType(workTypeDefName);
                        float perCheck = wagePerHour * settings.wageMultiplier * mgr.EffectiveWageMultiplier(pawn)
                                         * CheckStride / TicksPerHour;

                        float accum = mgr.GetWorkWageAccum(id) + perCheck;
                        if (accum >= 0.01f) // fractional tickets: credit as soon as we have any
                        {
                            mgr.AddTickets(pawn, accum);
                            if (settings.logVerbose)
                                Log.Message($"[PPTE2] {pawn.LabelShortCap} earned {accum:0.##} tickets for working {workTypeDefName ?? "??"}. Balance={mgr.Balance(pawn):0.##}");
                            accum = 0f;
                        }
                        mgr.SetWorkWageAccum(id, accum);
                        wasWorking[id] = true;
                    }
                    else if (wasWorking.ContainsKey(id) && wasWorking[id])
                    {
                        // work session ended: flush the leftover fraction so short shifts pay out
                        float accum = mgr.GetWorkWageAccum(id);
                        if (accum >= 0.01f)
                        {
                            mgr.AddTickets(pawn, accum);
                            if (settings.logVerbose)
                                Log.Message($"[PPTE2] {pawn.LabelShortCap} settled {accum:0.##} leftover work. Balance={mgr.Balance(pawn):0.##}");
                        }
                        mgr.SetWorkWageAccum(id, 0f);
                        wasWorking[id] = false;
                    }
                }
            }

            if (hourlySettle)
            {
                // prune state for prisoners that left the map or died
                var live = new HashSet<int>();
                foreach (var map in game.Maps)
                {
                    if (map == null) continue;
                    foreach (var p in map.mapPawns.PrisonersOfColony)
                        if (p != null) live.Add(p.thingIDNumber);
                }
                var stale = new List<int>();
                foreach (var kv in wasWorking)
                    if (!live.Contains(kv.Key)) stale.Add(kv.Key);
                foreach (var k in stale) { wasWorking.Remove(k); mgr.SetWorkWageAccum(k, 0f); }
            }
        }
    }
}