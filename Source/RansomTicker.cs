using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Ransom (赎身) event detection. Runs from <see cref="PrisonersPayToEat2Manager"/>.
    ///
    /// Tracks when each pawn became a prisoner (for the minimum-imprisonment-time requirement) and
    /// lets prisoners ask to buy their freedom once their ticket balance and time served meet the
    /// requirements. The request is only a proposal — the player must approve it (in the prisoner
    /// menu) before <see cref="PrisonersPayToEat2Manager.TryApproveRansom"/> releases the prisoner.
    /// </summary>
    public static class RansomTicker
    {
        private const int CheckStride = 250;          // light touch every 0.1 day
        private const int AskInterval = 1500;         // eligible prisoners may ask every 0.6 day
        private const float AskChance = 0.5f;         // ...with this chance per check
        private const int PruneInterval = 60000;      // once a day

        /// <summary>Cooldown (ticks) after a rejected request before the prisoner may ask again.</summary>
        public const int DenyCooldownTicks = 120000;  // 2 days

        public static void Tick()
        {
            var settings = PrisonersPayToEat2Mod.Settings;
            if (settings == null || !settings.enableRansom) return;
            var game = Current.Game;
            if (game == null) return;
            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null) return;

            int now = game.tickManager.TicksGame;
            if (now % CheckStride != 0) return;
            bool askNow = now % AskInterval == 0;
            bool pruneNow = now % PruneInterval == 0;

            var live = pruneNow ? new HashSet<int>() : null;
            foreach (var map in game.Maps)
            {
                if (map == null) continue;
                var prisoners = map.mapPawns.PrisonersOfColony;
                for (int i = 0; i < prisoners.Count; i++)
                {
                    var pawn = prisoners[i];
                    if (pawn == null || pawn.Dead) continue;
                    int id = pawn.thingIDNumber;
                    if (live != null) live.Add(id);

                    if (!mgr.HasPrisonStartTick(id))
                    {
                        // a fresh imprisonment session (or ransom was toggled on mid-sentence):
                        // start the clock and clear any leftover request/cooldown state
                        mgr.SetPrisonStartTick(id, now);
                        mgr.ResetRansomSession(id);
                    }

                    if (!askNow) continue;
                    var d = mgr.DataFor(pawn);
                    if (d.ransomRequested) continue;               // already waiting for the player
                    if (now <= d.ransomDeniedUntilTick) continue;  // cooling down after a rejection
                    if (!mgr.CanRansom(pawn)) continue;            // tickets or time not enough yet
                    if (!Rand.Chance(AskChance)) continue;         // the prisoner decides to ask
                    mgr.RequestRansom(pawn);
                }
            }

            // forget imprisonment start for pawns that are no longer prisoners
            if (pruneNow) mgr.PrunePrisonStartTicks(live);
        }
    }
}
