using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// 囚犯社会行为 ticker：借款 / 抢劫 / 乞讨。
    /// 三个行为都由囚犯 AI 按自身状态（余额、饥饿、心情、好感度）自动发起，
    /// 全局开关 + 每囚犯三态覆盖（跟随全局/允许/禁止）共同控制（见
    /// <see cref="PrisonersPayToEat2Manager.FeatureAllowed"/>）。
    /// 抢劫为"原版真打"：双方互指近战 job，谁先倒地谁输（可能致死）；
    /// 战斗进行中由本 ticker 监控结算，超时未分出胜负则取消。
    /// </summary>
    public static class PrisonerSocialTicker
    {
        private const int CheckStride = 250;           // 每 250 tick 扫一次
        private const int FightTimeoutTicks = 30000;   // 打架最长 0.5 天，超时取消
        private const int BadLoanSweepInterval = 60000; // 每天清扫一次坏账

        // 各行为的发起冷却（Transient，不存档；记录上次发起时的 TicksGame）
        private static readonly Dictionary<int, int> lastLoanAttempt = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> lastRobAttempt = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> lastBegAttempt = new Dictionary<int, int>();

        // 进行中的抢劫打架
        private class ActiveFight
        {
            public int robberId;
            public int victimId;
            public float victimBalanceAtStart;
            public int startTick;
        }
        private static readonly List<ActiveFight> fights = new List<ActiveFight>();

        /// <summary>
        /// 清空进程级缓存状态。这些表以 thingIDNumber 为键且不随存档保存，而读档不会重载
        /// 程序集，所以换存档时必须清掉：否则上一局的"进行中打架"会在新存档里被补结算，
        /// 冷却/提示节流也会错误继承。
        /// </summary>
        public static void ResetTransientState()
        {
            lastLoanAttempt.Clear();
            lastRobAttempt.Clear();
            lastBegAttempt.Clear();
            fights.Clear();
        }

        public static void Tick()
        {
            var game = Current.Game;
            if (game == null) return;
            int now = game.tickManager.TicksGame;
            if (now % CheckStride != 0) return;

            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null) return;

            foreach (var map in game.Maps)
            {
                if (map == null) continue;
                var prisoners = map.mapPawns.PrisonersOfColony;
                for (int i = 0; i < prisoners.Count; i++)
                {
                    var pawn = prisoners[i];
                    if (pawn == null || pawn.Dead || pawn.Downed || pawn.InMentalState) continue;
                    // 未生成在地图上的囚犯（被装进容器/休眠舱/跨图转移中）pawn.Map 为 null，
                    // 后面的 PickTarget 会在 map.mapPawns 上抛空引用
                    if (!pawn.Spawned || pawn.MapHeld == null) continue;

                    TryBeg(pawn, mgr, now);
                    TryLoan(pawn, mgr, now);
                    TryRob(pawn, mgr, now);
                }
            }

            SettleFights(mgr, now);
            if (now % BadLoanSweepInterval == 0) SweepBadLoans(mgr);
        }

        // ================= 通用 =================

        private static bool CooldownReady(Dictionary<int, int> dict, int id, int now, float cooldownDays)
        {
            return !dict.TryGetValue(id, out int last) || (now - last) >= (int)(cooldownDays * 60000f);
        }

        private static void Mark(Dictionary<int, int> dict, int id, int now) => dict[id] = now;

        /// <summary>选一个同地图的囚犯目标：按分数选最高的（分数越高越可能被选中）。</summary>
        private static Pawn PickTarget(Pawn self, Map map, System.Func<Pawn, float> score)
        {
            if (self == null || map == null) return null; // 未生成的囚犯没有地图
            Pawn best = null;
            float bestScore = float.NegativeInfinity;
            var prisoners = map.mapPawns.PrisonersOfColony;
            for (int i = 0; i < prisoners.Count; i++)
            {
                var other = prisoners[i];
                if (other == null || other == self || other.Dead || other.Downed || other.InMentalState) continue;
                float s = score(other);
                if (s > bestScore)
                {
                    bestScore = s;
                    best = other;
                }
            }
            return best;
        }

        // ================= 乞讨 =================

        private static void TryBeg(Pawn beggar, PrisonersPayToEat2Manager mgr, int now)
        {
            var s = PrisonersPayToEat2Mod.Settings;
            if (!mgr.FeatureAllowed(beggar, SocialFeatureKind.Begging)) return;
            if (!CooldownReady(lastBegAttempt, beggar.thingIDNumber, now, s.beggingCooldownDays)) return;
            if (mgr.AvailableBalance(beggar) >= s.loanTriggerBalance) return; // 不穷不讨（儿童可算上父母代付）
            // 需要有点"惨"：饿或心情差
            if (beggar.needs?.food != null && beggar.needs.food.CurLevel > 0.5f
                && (beggar.needs.mood == null || beggar.needs.mood.CurLevel > 0.4f)) return;
            Mark(lastBegAttempt, beggar.thingIDNumber, now);

            // 目标：偏向对我好感度高 + 有余额的囚犯
            var target = PickTarget(beggar, beggar.Map,
                other => (mgr.Balance(other) <= 0.01f ? -999f : other.relations.OpinionOf(beggar) + Rand.Range(0f, 30f)));
            if (target == null) return;

            float opinion = target.relations.OpinionOf(beggar);
            float chance = Mathf.Clamp(s.beggingChanceBase + opinion * s.beggingChancePerOpinion, 0.05f, 0.95f);
            if (!Rand.Chance(chance))
            {
                GainThought(beggar, "PPTE2_BeggingFailed", target);
                if (s.logVerbose)
                    Log.Message($"[PPTE2] {beggar.LabelShortCap} begged {target.LabelShortCap} and was refused (opinion={opinion:0}).");
                return;
            }

            float minAmt = Mathf.Min(s.beggingAmountMin, s.beggingAmountMax);
            float maxAmt = Mathf.Max(s.beggingAmountMin, s.beggingAmountMax);
            float amount = Mathf.Min(Rand.Range(minAmt, maxAmt + 0.999f), mgr.Balance(target));
            if (amount <= 0.01f) return;
            mgr.AddTickets(target, -amount);
            mgr.AddTickets(beggar, amount); // 经 AddTickets：乞讨所得优先还自己的借款
            GainThought(target, "PPTE2_Begged", beggar);
            GainThought(beggar, "PPTE2_BeggingSucceeded", target); // 讨到饭的宽慰
            Messages.Message("PPTE2_BegSuccess".Translate(
                    beggar.LabelShortCap, target.LabelShortCap, amount.ToString("0.##"), PPTEName.Ticket),
                beggar, MessageTypeDefOf.NeutralEvent);
            if (s.logVerbose)
                Log.Message($"[PPTE2] {beggar.LabelShortCap} begged {amount:0.##} tickets from {target.LabelShortCap} (opinion={opinion:0}).");
        }

        // ================= 借款 =================

        private static void TryLoan(Pawn borrower, PrisonersPayToEat2Manager mgr, int now)
        {
            var s = PrisonersPayToEat2Mod.Settings;
            if (!mgr.FeatureAllowed(borrower, SocialFeatureKind.Loan)) return;
            if (!CooldownReady(lastLoanAttempt, borrower.thingIDNumber, now, s.loanCooldownDays)) return;
            if (mgr.AvailableBalance(borrower) >= s.loanTriggerBalance) return;
            if (mgr.HasOutstandingLoans(borrower)) return; // 已有借款不能再借，防止债务链
            Mark(lastLoanAttempt, borrower.thingIDNumber, now);

            // 找贷方：余额充裕（>=3 倍触发线）、对我好感度高者优先
            float lenderMin = s.loanTriggerBalance * 3f;
            var lender = PickTarget(borrower, borrower.Map, other =>
            {
                if (!mgr.FeatureAllowed(other, SocialFeatureKind.Loan)) return float.NegativeInfinity;
                if (mgr.Balance(other) < lenderMin) return float.NegativeInfinity;
                return other.relations.OpinionOf(borrower) + Rand.Range(0f, 40f);
            });

            if (lender == null)
            {
                // 没有囚犯愿意借 → 向玩家借（开关开启时逐笔弹窗批准）
                if (s.allowPlayerLoans && !borrower.InMentalState)
                    Find.WindowStack.Add(new Dialog_LoanRequest(borrower));
                return;
            }

            float amount = Mathf.Min(s.maxLoanAmount, Mathf.Max(1f, s.loanTriggerBalance - mgr.Balance(borrower)));
            float interest = mgr.LoanInterestFor(lender, borrower);
            // MakeLoan 会在赊账关闭时按贷方可动用余额封顶，返回实际放款额（0 = 贷方出不起）
            float lent = mgr.MakeLoan(borrower, lender, amount, interest);
            if (lent <= 0.01f) return;
            Messages.Message("PPTE2_LoanMade".Translate(
                    borrower.LabelShortCap, lender.LabelShortCap, lent.ToString("0.##"), PPTEName.Ticket,
                    (interest * 100f).ToString("0")),
                borrower, MessageTypeDefOf.NeutralEvent);
            if (s.logVerbose)
                Log.Message($"[PPTE2] {borrower.LabelShortCap} borrowed {lent:0.##} tickets from {lender.LabelShortCap} at {interest * 100f:0}% interest.");
        }

        // ================= 抢劫 =================

        private static void TryRob(Pawn robber, PrisonersPayToEat2Manager mgr, int now)
        {
            var s = PrisonersPayToEat2Mod.Settings;
            if (!mgr.FeatureAllowed(robber, SocialFeatureKind.Robbery)) return;
            if (!CooldownReady(lastRobAttempt, robber.thingIDNumber, now, s.robberyCooldownDays)) return;
            if (mgr.AvailableBalance(robber) >= s.loanTriggerBalance) return; // 有钱不抢（儿童可算上父母代付）
            if (robber.needs?.food == null || robber.needs.food.CurLevel >= s.robberyHungerThreshold) return; // 不够饿不抢
            if (robber.needs?.mood != null && robber.needs.mood.CurLevel > 0.35f) return; // 心情好不抢（低频）
            Mark(lastRobAttempt, robber.thingIDNumber, now);

            // 目标：有票 + 格斗弱 + 无暴力（更好欺负）优先
            // 分数 = 余额加成 - 格斗等级 + 无暴力加成 + 随机
            var victim = PickTarget(robber, robber.Map, other =>
                mgr.Balance(other) <= 0.01f ? float.NegativeInfinity
                : mgr.Balance(other) * 0.2f
                  - (other.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 5f)
                  + (other.WorkTagIsDisabled(WorkTags.Violent) ? 15f : 0f)
                  + Rand.Range(0f, 40f));
            if (victim == null) return;
            // 抢劫者无暴力能力会让近战 job 空转，跳过（受害者无暴力则照常被抢，原版机制下他不会还手）
            if (robber.WorkTagIsDisabled(WorkTags.Violent)) return;

            // 原版真打：双方互指近战，谁先倒地谁输（可能致死）
            var job1 = JobMaker.MakeJob(JobDefOf.AttackMelee, victim);
            robber.jobs.StartJob(job1, JobCondition.InterruptForced);
            var job2 = JobMaker.MakeJob(JobDefOf.AttackMelee, robber);
            victim.jobs.StartJob(job2, JobCondition.InterruptForced);

            fights.Add(new ActiveFight
            {
                robberId = robber.thingIDNumber,
                victimId = victim.thingIDNumber,
                victimBalanceAtStart = mgr.Balance(victim),
                startTick = now
            });
            if (s.logVerbose)
                Log.Message($"[PPTE2] {robber.LabelShortCap} is robbing {victim.LabelShortCap} (melee fight started).");
        }

        /// <summary>监控进行中的抢劫打架，分出胜负或超时后结算。</summary>
        private static void SettleFights(PrisonersPayToEat2Manager mgr, int now)
        {
            var s = PrisonersPayToEat2Mod.Settings;
            for (int i = fights.Count - 1; i >= 0; i--)
            {
                var f = fights[i];
                var robber = PrisonersPayToEat2Manager.FindPawnById(f.robberId);
                var victim = PrisonersPayToEat2Manager.FindPawnById(f.victimId);
                if (robber == null || victim == null)
                {
                    fights.RemoveAt(i);
                    continue;
                }

                bool robberDown = robber.Dead || robber.Downed;
                bool victimDown = victim.Dead || victim.Downed;
                if (!robberDown && !victimDown && now - f.startTick < FightTimeoutTicks) continue;
                fights.RemoveAt(i);

                if (robberDown && !victimDown)
                {
                    // 抢劫者倒地：受害者反击成功，抢劫者 0 收益
                    GainThought(robber, "PPTE2_RobberyFailed", victim);
                    GainThought(victim, "PPTE2_RobberyDefended", robber); // 打退抢劫者，解气
                    Messages.Message("PPTE2_RobberyFailedMsg".Translate(robber.LabelShortCap, victim.LabelShortCap),
                        robber, MessageTypeDefOf.NegativeEvent);
                    if (s.logVerbose)
                        Log.Message($"[PPTE2] {robber.LabelShortCap}'s robbery of {victim.LabelShortCap} failed.");
                }
                else if (victimDown && !robberDown)
                {
                    // 受害者倒地/死亡：抢劫成功，拿走开局余额的一部分
                    float take = Mathf.Min(f.victimBalanceAtStart * s.robberyTakePercent, mgr.Balance(victim));
                    if (take > 0.01f)
                    {
                        mgr.AddTickets(victim, -take);
                        mgr.AddTickets(robber, take); // 经 AddTickets：赃款优先还自己的借款
                    }
                    GainThought(victim, "PPTE2_Robbed", robber);
                    GainThought(robber, "PPTE2_RobberySucceeded", victim); // 得手快感
                    Messages.Message("PPTE2_RobberySuccessMsg".Translate(
                            robber.LabelShortCap, victim.LabelShortCap, take.ToString("0.##"), PPTEName.Ticket),
                        robber, MessageTypeDefOf.NegativeEvent);
                    if (s.logVerbose)
                        Log.Message($"[PPTE2] {robber.LabelShortCap} robbed {take:0.##} tickets from {victim.LabelShortCap}.");
                }
                // 双方都倒地（同归于尽）或超时未分胜负：不结算
            }
        }

        // ================= 坏账 =================

        /// <summary>每天清扫：借方已不在押（释放/招募/越狱/死亡）或贷方已找不到 → 记为坏账。</summary>
        private static void SweepBadLoans(PrisonersPayToEat2Manager mgr)
        {
            var s = PrisonersPayToEat2Mod.Settings;
            for (int i = mgr.loans.Count - 1; i >= 0; i--)
            {
                var loan = mgr.loans[i];
                var borrower = PrisonersPayToEat2Manager.FindPawnById(loan.borrowerId);
                if (borrower != null && borrower.IsPrisonerOfColony) continue; // 还在押，等还

                var lender = PrisonersPayToEat2Manager.FindPawnById(loan.lenderId);
                if (lender != null && s.badDebtMoodPenalty)
                    GainThought(lender, "PPTE2_LoanDefaulted", borrower);
                // 债务作废，借方不该再背着"借款负担"（否则这笔心情减益会一直留着）
                var loanTakenDef = PrisonersPayToEat2Manager.GetThought("PPTE2_LoanTaken");
                if (loanTakenDef != null)
                    borrower?.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDef(loanTakenDef);
                mgr.loans.RemoveAt(i);
                if (lender != null)
                    Messages.Message("PPTE2_LoanDefaultedMsg".Translate(
                            borrower?.LabelShortCap ?? "?", lender.LabelShortCap, loan.owed.ToString("0.##"), PPTEName.Ticket),
                        lender, MessageTypeDefOf.NegativeEvent);
                if (s.logVerbose)
                    Log.Message($"[PPTE2] Loan of {loan.owed:0.##} tickets from {loan.lenderId} to {loan.borrowerId} written off as bad debt.");
            }
        }

        // ================= 思想 =================

        private static void GainThought(Pawn pawn, string thoughtDefName, Pawn otherPawn = null)
        {
            if (pawn == null || pawn.Dead) return;
            var thought = PrisonersPayToEat2Manager.GetThought(thoughtDefName);
            if (thought == null) return;
            pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(thought, otherPawn);
        }
    }
}
