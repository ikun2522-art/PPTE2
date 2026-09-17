using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace PrisonersPayToEat2
{
    /// <summary>囚犯社会行为类型（借款/抢劫/乞讨）。</summary>
    public enum SocialFeatureKind { Loan, Robbery, Begging }

    /// <summary>每囚犯对某社会行为的设置：跟随全局 / 强制允许 / 强制禁止。</summary>
    public enum PPTE2SocialSetting { Follow, Allow, Deny }

    /// <summary>儿童饭票扣费模式：先自己的 / 先父母的 / 合并钱包。</summary>
    public enum ChildPayMode { OwnFirst, ParentFirst, MergedPool }

    /// <summary>
    /// 一条未还清的囚犯间借款记录。还款由借方收入自动触发（见
    /// <see cref="PrisonersPayToEat2Manager.AddTickets"/>），还清后从列表移除；
    /// 借方被释放/死亡/越狱则记为坏账（贷方心情减益）。
    /// </summary>
    public class LoanRecord : IExposable
    {
        public int borrowerId;
        public int lenderId;
        public float principal;   // 原始借款额（展示用）
        public float owed;        // 剩余应还（本金+利息）
        public int createdTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref borrowerId, "borrowerId", 0);
            Scribe_Values.Look(ref lenderId, "lenderId", 0);
            Scribe_Values.Look(ref principal, "principal", 0f);
            Scribe_Values.Look(ref owed, "owed", 0f);
            Scribe_Values.Look(ref createdTick, "createdTick", 0);
        }
    }

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
        public PPTE2SocialSetting loanSetting = PPTE2SocialSetting.Follow;     // 借款
        public PPTE2SocialSetting robberySetting = PPTE2SocialSetting.Follow;  // 抢劫
        public PPTE2SocialSetting beggingSetting = PPTE2SocialSetting.Follow;  // 乞讨
        public PPTE2SocialSetting childPaySetting = PPTE2SocialSetting.Follow; // 父母代付（仅儿童）
        public PPTE2SocialSetting workSetting = PPTE2SocialSetting.Follow;     // 强制工作（内置劳工系统）

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
            Scribe_Values.Look(ref loanSetting, "loanSetting", PPTE2SocialSetting.Follow);
            Scribe_Values.Look(ref robberySetting, "robberySetting", PPTE2SocialSetting.Follow);
            Scribe_Values.Look(ref beggingSetting, "beggingSetting", PPTE2SocialSetting.Follow);
            Scribe_Values.Look(ref childPaySetting, "childPaySetting", PPTE2SocialSetting.Follow);
            Scribe_Values.Look(ref workSetting, "workSetting", PPTE2SocialSetting.Follow);
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

        // 未还清的囚犯间借款（借方还清后移除；坏账由 PrisonerSocialTicker 清扫）。
        public List<LoanRecord> loans = new List<LoanRecord>();

        // Meal-ticket health-status hediff maintenance (see EnsureTicketHediffs).
        private static HediffDef ticketHediffDef;
        private int lastHediffCheckTick = -1;

        public PrisonersPayToEat2Manager() { }
        public PrisonersPayToEat2Manager(Game game) { }

        // game 可能为 null（主菜单、读档早期），此时不应抛 NRE 而应返回 null 让调用方跳过
        public static PrisonersPayToEat2Manager For(Game game) => game?.GetComponent<PrisonersPayToEat2Manager>();
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
            // 赊账开启时允许余额为负（欠款由之后工资/卖器官等收入自动优先偿还，
            // 因为收入只是加到余额上，负数先被填平）；关闭时保持旧行为：余额不低于 0。
            if (d.ticketBalance < 0f)
            {
                if (!PrisonersPayToEat2Mod.Settings.allowMealDebt)
                    d.ticketBalance = 0f;
                return; // 赊账欠款还没填平，先不还借款
            }
            // 余额为正且填平了赊账欠款后，剩余部分优先偿还囚犯间借款
            RepayLoans(p, d);
        }

        /// <summary>
        /// 用借方余额中的正数部分自动偿还借款：还给贷方，直到还清。
        /// 借方收入（工资/卖器官/乞讨/玩家发放）都会经由 <see cref="AddTickets"/> 走到这里。
        /// </summary>
        private void RepayLoans(Pawn borrower, PrisonerTicketData d)
        {
            if (loans.Count == 0 || d.ticketBalance <= 0f) return;
            for (int i = loans.Count - 1; i >= 0; i--)
            {
                var loan = loans[i];
                if (loan.borrowerId != borrower.thingIDNumber) continue;
                if (loan.owed <= 0f) { loans.RemoveAt(i); continue; }
                if (d.ticketBalance <= 0f) break;

                var lender = FindPawnById(loan.lenderId);
                if (lender == null)
                {
                    // 贷方已不在场（死亡/被带走/离图）：债务直接作废。否则每笔收入都会从借方
                    // 扣款却无人接收，饭票凭空蒸发，而且借方会被这笔债永久挡住赎身。
                    // 贷方的心情减益由 PrisonerSocialTicker.SweepBadLoans 负责。
                    loans.RemoveAt(i);
                    ClearLoanBurdenThought(borrower);
                    continue;
                }

                float pay = Mathf.Min(loan.owed, d.ticketBalance);
                d.ticketBalance -= pay;
                loan.owed -= pay;
                DataFor(lender).ticketBalance += pay;
                if (loan.owed > 0f) continue;

                // 还清：移除借方的"借款负担"，贷方获得"借款还清"（心情 +5）
                ClearLoanBurdenThought(borrower);
                var repaidThought = GetThought("PPTE2_LoanRepaid");
                if (repaidThought != null)
                    lender.needs?.mood?.thoughts?.memories?.TryGainMemory(repaidThought, borrower);
                Messages.Message("PPTE2_LoanRepaidMsg".Translate(
                        borrower.LabelShortCap, lender.LabelShortCap, loan.principal.ToString("0.##"), PPTEName.Ticket),
                    borrower, MessageTypeDefOf.PositiveEvent);
                loans.RemoveAt(i);
            }
        }

        /// <summary>移除借方的"借款负担"心情（还清或债务作废时调用）。</summary>
        private static void ClearLoanBurdenThought(Pawn borrower)
        {
            var loanTakenDef = GetThought("PPTE2_LoanTaken");
            if (loanTakenDef != null)
                borrower?.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDef(loanTakenDef);
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

        // ================= 儿童父母代付 =================

        /// <summary>是否为儿童（用原版成人判定取反；成人年龄线可被 MOD 调整，跟随游戏定义）。</summary>
        public static bool IsChild(Pawn p)
            => p != null && p.ageTracker != null && !p.ageTracker.Adult;

        /// <summary>儿童的父母：本殖民地、在押、存活的直接父母（生父母/养父母都算，原版父母关系）。</summary>
        public List<Pawn> SupportingParents(Pawn child)
        {
            var result = new List<Pawn>();
            if (child == null || child.relations == null) return result;
            var direct = child.relations.DirectRelations;
            for (int i = 0; i < direct.Count; i++)
            {
                var rel = direct[i];
                if (rel == null || rel.def != PawnRelationDefOf.Parent) continue;
                var parent = rel.otherPawn;
                if (parent == null || parent.Dead) continue;
                if (!parent.IsPrisonerOfColony) continue;
                result.Add(parent);
            }
            return result;
        }

        /// <summary>父母代付是否对该儿童生效（全局开关 × 每囚犯设置 × 至少一位在押父母）。</summary>
        public bool ChildParentPayActive(Pawn child)
        {
            if (!IsChild(child)) return false;
            if (!PrisonersPayToEat2Mod.Settings.enableChildParentPay) return false;
            if (DataFor(child).childPaySetting == PPTE2SocialSetting.Deny) return false;
            return SupportingParents(child).Count > 0;
        }

        /// <summary>父母的可用饭票合计（正余额之和；负余额的父母不参与代付）。</summary>
        public float ParentTotalBalance(Pawn child)
        {
            float total = 0f;
            foreach (var parent in SupportingParents(child))
                total += Mathf.Max(0f, Balance(parent));
            return total;
        }

        /// <summary>父母的名字列表（UI 显示用，逗号分隔）。</summary>
        public string SupportingParentNames(Pawn child)
        {
            var names = new List<string>();
            foreach (var parent in SupportingParents(child))
                names.Add(parent.LabelShortCap);
            return string.Join(", ", names.ToArray());
        }

        /// <summary>
        /// 可用余额：儿童自己的正余额 + 在押父母的正余额（父母代付生效时）；其余情况等于自己的余额。
        /// 用于"买不买得起饭"的判断与社会行为（乞讨/借款）的"穷不穷"触发。
        /// </summary>
        public float AvailableBalance(Pawn p)
        {
            if (p == null) return 0f;
            float own = Mathf.Max(0f, Balance(p));
            if (!ChildParentPayActive(p)) return own;
            return own + ParentTotalBalance(p);
        }

        /// <summary>
        /// 支付饭费或赎身费（含父母代付）。按全局 <see cref="ChildPayMode"/> 决定扣款顺序：
        /// 先自己 = 自己的正余额优先，父母补差；先父母 = 父母按余额比例分摊，自己补差；
        /// 合并钱包 = 孩子与父母按各自正余额比例共同分摊。
        /// 超出家庭可用部分由儿童自己承担（赊账欠款）。无父母代付的普通囚犯行为与原赊账扣款一致。
        /// </summary>
        public void PayWithSupport(Pawn p, float cost)
        {
            if (cost <= 0f) return;
            var d = DataFor(p);
            float remaining = cost;
            if (!ChildParentPayActive(p))
            {
                d.ticketBalance -= remaining;
                return;
            }
            var parents = SupportingParents(p);
            float parentTotal = ParentTotalBalance(p);

            switch (PrisonersPayToEat2Mod.Settings.childPayMode)
            {
                case ChildPayMode.ParentFirst:
                    SplitPay(parents, Mathf.Min(remaining, parentTotal));
                    remaining -= Mathf.Min(remaining, parentTotal);
                    break;
                case ChildPayMode.MergedPool:
                {
                    float pool = Mathf.Max(0f, d.ticketBalance) + parentTotal;
                    var members = new List<Pawn> { p };
                    members.AddRange(parents);
                    SplitPay(members, Mathf.Min(remaining, pool));
                    remaining -= Mathf.Min(remaining, pool);
                    break;
                }
                default: // OwnFirst
                    float ownPay = Mathf.Min(Mathf.Max(0f, d.ticketBalance), remaining);
                    d.ticketBalance -= ownPay;
                    remaining -= ownPay;
                    SplitPay(parents, Mathf.Min(remaining, parentTotal));
                    remaining -= Mathf.Min(remaining, parentTotal);
                    break;
            }
            d.ticketBalance -= remaining; // 超出家庭可用部分成为孩子的欠款
        }

        /// <summary>把 amount 按各 pawn 的正余额比例分摊扣款（最后一个吃下浮点余量）。</summary>
        private static void SplitPay(List<Pawn> pawns, float amount)
        {
            if (pawns == null || pawns.Count == 0 || amount <= 0f) return;
            var mgr = Current;
            var weights = new List<float>(pawns.Count);
            float total = 0f;
            for (int i = 0; i < pawns.Count; i++)
            {
                float w = Mathf.Max(0f, mgr.Balance(pawns[i]));
                weights.Add(w);
                total += w;
            }
            if (total <= 0f) return;
            float remaining = amount;
            for (int i = 0; i < pawns.Count; i++)
            {
                float pay = i == pawns.Count - 1 ? remaining : amount * (weights[i] / total);
                if (pay > 0f) mgr.DataFor(pawns[i]).ticketBalance -= pay;
                remaining -= pay;
                if (remaining <= 0.0001f) break;
            }
        }

        // ================= 囚犯社会行为（借款/抢劫/乞讨） =================

        /// <summary>读取某个社会行为的每囚犯设置（跟随全局/允许/禁止）。</summary>
        public PPTE2SocialSetting FeatureSetting(Pawn p, SocialFeatureKind kind)
        {
            var d = DataFor(p);
            return kind switch
            {
                SocialFeatureKind.Loan => d.loanSetting,
                SocialFeatureKind.Robbery => d.robberySetting,
                _ => d.beggingSetting
            };
        }

        /// <summary>设置某个社会行为的每囚犯覆盖。</summary>
        public void SetFeatureSetting(Pawn p, SocialFeatureKind kind, PPTE2SocialSetting value)
        {
            var d = DataFor(p);
            switch (kind)
            {
                case SocialFeatureKind.Loan: d.loanSetting = value; break;
                case SocialFeatureKind.Robbery: d.robberySetting = value; break;
                default: d.beggingSetting = value; break;
            }
        }

        /// <summary>某囚犯是否被允许发起某社会行为（全局开关 × 每囚犯覆盖）。</summary>
        public bool FeatureAllowed(Pawn p, SocialFeatureKind kind)
        {
            if (p == null) return false;
            var s = PrisonersPayToEat2Mod.Settings;
            bool global = kind switch
            {
                SocialFeatureKind.Loan => s.enableLoans,
                SocialFeatureKind.Robbery => s.enableRobbery,
                _ => s.enableBegging
            };
            if (!global) return false;
            return FeatureSetting(p, kind) switch
            {
                PPTE2SocialSetting.Allow => true,
                PPTE2SocialSetting.Deny => false,
                _ => true
            };
        }

        /// <summary>贷方对借方的借款利率：好感度越低利率越高（可配置公式，钳上下限）。</summary>
        public float LoanInterestFor(Pawn lender, Pawn borrower)
        {
            var s = PrisonersPayToEat2Mod.Settings;
            float opinion = lender?.relations?.OpinionOf(borrower) ?? 0f;
            return Mathf.Clamp(s.loanInterestBase - opinion * s.loanInterestPerOpinion,
                s.loanInterestMin, s.loanInterestMax);
        }

        /// <summary>
        /// 生成一笔囚犯间借款：借方立即到账，贷方立即扣款，记录待还金额（本金+利息）。
        /// 返回实际放款额（赊账关闭时贷方余额不能被扣成负数，按贷方可动用余额封顶，
        /// 避免凭空增发饭票；封顶后为 0 则不放款）。
        /// </summary>
        public float MakeLoan(Pawn borrower, Pawn lender, float amount, float interest)
        {
            if (amount <= 0f || borrower == null || lender == null) return 0f;
            if (!PrisonersPayToEat2Mod.Settings.allowMealDebt)
                amount = Mathf.Min(amount, Mathf.Max(0f, Balance(lender)));
            if (amount <= 0f) return 0f;
            AddTickets(borrower, amount);
            AddTickets(lender, -amount);
            loans.Add(new LoanRecord
            {
                borrowerId = borrower.thingIDNumber,
                lenderId = lender.thingIDNumber,
                principal = amount,
                owed = amount * (1f + interest),
                createdTick = Find.TickManager.TicksGame
            });
            // 借款负担（心情 -4）；还清时由 RepayLoans 移除
            var loanThought = GetThought("PPTE2_LoanTaken");
            if (loanThought != null)
                borrower.needs?.mood?.thoughts?.memories?.TryGainMemory(loanThought, lender);
            return amount;
        }

        /// <summary>该囚犯是否还有未还清的借款（有借款不能赎身，防止"借票赎身跑路"）。</summary>
        public bool HasOutstandingLoans(Pawn p)
        {
            if (p == null) return false;
            foreach (var loan in loans)
                if (loan.borrowerId == p.thingIDNumber && loan.owed > 0.001f) return true;
            return false;
        }

        /// <summary>该囚犯未还清的借款总额（UI 展示用）。</summary>
        public float OutstandingOwed(Pawn p)
        {
            if (p == null) return 0f;
            float sum = 0f;
            foreach (var loan in loans)
                if (loan.borrowerId == p.thingIDNumber) sum += loan.owed;
            return sum;
        }

        /// <summary>玩家同意向囚犯借出饭票（无息、纯救济，不产生借贷记录）。</summary>
        public void ApprovePlayerLoan(Pawn p)
        {
            var s = PrisonersPayToEat2Mod.Settings;
            float amount = Mathf.Min(s.maxLoanAmount, Mathf.Max(1f, s.loanTriggerBalance - Balance(p)));
            AddTickets(p, amount);
            Messages.Message("PPTE2_PlayerLoanApproved".Translate(
                    p.LabelShortCap, amount.ToString("0.##"), PPTEName.Ticket),
                p, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>玩家拒绝向囚犯借出饭票。</summary>
        public void RejectPlayerLoan(Pawn p)
        {
            Messages.Message("PPTE2_PlayerLoanRejected".Translate(p.LabelShortCap),
                p, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>按 thingIDNumber 在所有已生成地图的存活单位中找囚犯（借贷双方查找用）。</summary>
        public static Pawn FindPawnById(int id)
        {
            if (id <= 0) return null;
            foreach (var map in Find.Maps)
            {
                if (map == null) continue;
                foreach (var p in map.mapPawns.AllPawnsSpawned)
                    if (p != null && p.thingIDNumber == id) return p;
            }
            return null;
        }

        // 社会行为思想的延迟缓存（defs 加载完成后才可取）
        private static readonly Dictionary<string, ThoughtDef> thoughtCache = new Dictionary<string, ThoughtDef>();

        /// <summary>获取本 mod 定义的社会行为思想（见 Defs/ThoughtDefs），找不到返回 null。</summary>
        public static ThoughtDef GetThought(string defName)
        {
            if (defName == null) return null;
            if (!thoughtCache.TryGetValue(defName, out var def))
            {
                def = DefDatabase<ThoughtDef>.GetNamedSilentFail(defName);
                if (def != null) thoughtCache[defName] = def;
            }
            return def;
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

        /// <summary>True when available balance (儿童含父母代付) and imprisonment-time requirements are met.</summary>
        public bool CanRansom(Pawn p)
        {
            if (p == null || !p.IsPrisonerOfColony) return false;
            if (AvailableBalance(p) < EffectiveRansomCost(p)) return false;
            if (HasOutstandingLoans(p)) return false; // 有未还清的借款不能赎身
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
            PayWithSupport(p, cost); // 儿童可用父母饭票代付赎身费
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
            // List<T> 必须用单 lookMode 重载；写成 (LookMode.Value, LookMode.Deep) 会绑定到
            // params object[] ctorArgs 重载，导致实际按 LookMode.Value 序列化 IExposable 元素
            // （Scribe_Values 直接报错返回），借款记录会在存读档后全部丢失。
            Scribe_Collections.Look(ref loans, "loans", LookMode.Deep);
            if (loans == null) loans = new List<LoanRecord>();
            // research contributions are transient; drop stale refs on load
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                researchContrib.Clear();
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            PrisonLaborWageTicker.Tick();
            RansomTicker.Tick();
            PrisonerSocialTicker.Tick();
            EnsureTicketHediffs();
        }

        // 读档 / 开新档时清空进程级缓存。这些表以 thingIDNumber 为键（读档后编号会复用），
        // 且读档不会重载程序集，所以必须显式清理：
        //  - 上一局遗留的"进行中打架"会在新存档里被补结算，凭空转账；
        //  - 上一局的借款/抢劫/乞讨冷却与提示节流会错误继承到本局囚犯身上。
        public override void LoadedGame() => ResetTransientState();
        public override void StartedNewGame() => ResetTransientState();

        private void ResetTransientState()
        {
            researchContrib.Clear();
            PrisonerSocialTicker.ResetTransientState();
            PrisonLaborWageTicker.ResetTransientState();
            Harmony_Ingest.ResetTransientState();
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
                    // 内置劳工系统：兜底补齐囚犯缺失的 tracker / 工作设置
                    // （捕获时的 SetGuestStatus 补丁是主路径，这里覆盖老存档与例外路径）
                    PrisonerWorkSystem.EnsurePrisonerSetup(pawn);
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