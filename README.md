# 囚犯付费吃饭2（Prisoners Pay To Eat 2）

[![RimWorld 1.6](https://img.shields.io/badge/RimWorld-1.6-blue)](https://store.steampowered.com/app/294100/RimWorld/)
[![Steam Workshop](https://img.shields.io/badge/Steam-Workshop-1b2838?logo=steam)](https://steamcommunity.com/sharedfiles/filedetails/?id=3780939234)

> ⚠️ 实验性版本（Exp）· Experimental build

囚犯再也不能白嫖殖民地的食物了！每一位囚犯进食都必须支付「饭票」，没有饭票就只能挨饿。

Prisoners can no longer freeload off your colony's food supply! Every meal a prisoner eats must be paid for with a meal ticket — no tickets, no food.

---

## ✨ 功能特性 / Features

### 🍚 饭票经济 / Meal Ticket Economy
- 囚犯进食必须支付饭票，价格由食物的**市场价值**决定（精致食物贵，简单食物便宜，营养膏按营养值折算）
- 支持小数饭票（如 0.01 张），价格与余额精确到百分位
- 支持**逐种食物单独定价**：设置页可对每种食物单独设置饭票价格
- **赊账机制**（默认开启）：余额不足时囚犯仍可吃饭，余额扣成负数（欠款），之后工资等收入优先还债；健康页会以红字显示欠款。关闭赊账后，余额不足的囚犯将吃不到饭（食物保留不浪费）
- Prisoners must pay meal tickets to eat. Price is based on the food's **market value**.
- Fractional tickets supported (e.g. 0.01); prices and balances work down to two decimals.
- Per-food pricing: override the ticket cost of any individual food item.
- **Meal credit** (on by default): broke prisoners may still eat and the balance goes negative (debt); wages and other income repay the debt first. Debt shows in red in the health tab. With credit off, a prisoner who can't afford a meal simply cannot eat it (the food is kept, not wasted).

### ⛏️ Prison Labor 打工赚饭票 / Earn Tickets Through Prison Labor
- 集成 Avius 的 **Prison Labor** MOD：哪些工种囚犯能做、工作区域、动机系统完全由 Prison Labor 管理
- 按**工作种类**分别设置每小时饭票（如挖矿 2/小时、清洁 1/小时、种植 0.5/小时）
- **按时 / 按量双计费**：每个工种可单独切换按量计费，按产出发饭票——生产台按批次、采矿按矿脉、建造按建筑、种植/砍伐/播种按株、搬运按次、清洁按块、研究按科技（多人按贡献比例分）
- **未知工种通用兜底**：其他 MOD 新增的工种自动支持按量（按完成一次工作计费），无需额外配置
- 每个囚犯可单独覆盖计费方式（跟随全局 / 按时 / 按量）
- 全局工资倍率 + 每个囚犯的**个人工资倍率**可叠加调整
- Integrates Avius's **Prison Labor** MOD: which work types a prisoner may do is fully decided by Prison Labor.
- Per-work-type hourly wages (e.g. mining 2/hr, cleaning 1/hr, growing 0.5/hr).
- **Hourly or piece-rate billing per work type**: piece rate pays per completed output — production per recipe batch, mining per vein, construction per building, growing/cutting per plant, hauling per haul, cleaning per filth, research per tech (split among contributors by work share).
- **Generic fallback for unknown work types**: work types added by other MODs support piece rate automatically (paid per completed job).
- Per-prisoner billing override (follow global / hourly / piece-rate).
- Global wage multiplier + per-prisoner wage multiplier stack.

### 🏥 贩卖器官换饭票 / Sell Organs for Tickets
- 新增两种医疗手术：**摘取肾脏** / **摘取肺**，成功后囚犯获得饭票
- 非致命保障：仅当囚犯仍有两颗肾/两叶肺时才可摘取其一
- 摘下的器官作为物品正常生成；每个器官只能卖一次
- 可在设置页一键关闭，或对单个囚犯单独覆盖
- Two new surgeries: **remove kidney** / **remove lung**, paying tickets on success.
- Non-lethal safety: an organ can only be harvested while a matching one remains.
- Harvested organs spawn as items; each organ sells only once.
- Can be disabled globally in settings, or overridden per prisoner.

### 🔓 囚犯赎身 / Prisoner Ransom
- 囚犯攒够一定数量的饭票（默认 **1000**，可配置）并满足最短囚禁时间后，有一定概率提出**赎身请求**
- 请求需要**玩家同意**：同意后自动扣除对应饭票并**自动释放**囚犯；拒绝后需等待一段时间才能再次请求
- 全局设置：赎身所需饭票数、最短囚禁天数（自成为囚犯起算）
- 单个囚犯可单独覆盖所需饭票数与最短囚禁天数（配置囚犯窗口，0 = 跟随全局）
- Once a prisoner has saved up enough tickets (default **1000**, configurable) and served the minimum imprisonment time, they may occasionally **ask to buy their freedom**
- The request needs **your approval**: approving automatically deducts the tickets and **releases the prisoner**; rejecting starts a cooldown before they may ask again
- Global settings: required ticket count and minimum imprisonment time (since capture)
- Per-prisoner override for both values (prisoner config window, 0 = follow global)

### 🎛️ 玩家管理 / Player Controls
- 选中囚犯：一个**「囚犯饭票」按钮**整合全部操作——饭票余额、发放/扣除饭票、配置囚犯、赎身批准
- 囚犯的**健康标签页**会以「健康状态」形式直接显示饭票余额，颜色随余额变化（绿/黄/橙/红），悬停可见具体数量；欠款显示为红字
- 每个囚犯单独配置：个人食物倍率、个人工资倍率、计费方式（按时/按量）、是否允许贩卖器官、赎身所需饭票与最短囚禁天数、**借款/抢劫/乞讨各自允许/禁止**
- 手动发放/扣除支持小数
- One **"Prisoner tickets" button** on a selected prisoner gathers every control — balance, give/take tickets, configure, ransom approval.
- The prisoner's **health tab** shows the ticket balance as a "health status" row, colored by how much is left (green/yellow/orange/red); debt shows in red; hover for details.
- Per-prisoner settings: food multiplier, wage multiplier, billing mode (hourly/piece-rate), organ-sale permission, ransom ticket count & minimum imprisonment time, and **per-feature allow/deny for loans, robbery and begging**.
- Manual give/take supports decimals.

### 🗣️ 囚犯社会行为 / Prisoner Social Behaviors
- 吃不起饭的囚犯会自己想办法：**借款 / 乞讨 / 抢劫**（AI 自动触发，可全局开关或单囚犯允许/禁止）
- **借款**：向其他囚犯借饭票，**利率随好感度浮动**（好感越低利息越高）；借方收入自动优先还贷；借方被释放/死亡则坏账，贷方心情受损；有未还借款的囚犯无法赎身
- **乞讨**：成功率取决于对方对自己的好感度，成功讨到少量饭票，失败则丢脸（心情减益）
- **抢劫**：真实近战！谁先倒地谁输（可能致死），赢家拿走目标余额的一部分；输家挨打还丢脸
- 可开启「向玩家借款」：囚犯会弹出请求窗口，由玩家逐笔批准（默认关闭）
- Broke prisoners take matters into their own hands: **borrowing, begging and robbery** (AI-driven; global toggles plus per-prisoner allow/deny).
- **Loans**: borrow tickets from other prisoners — **interest scales with opinion** (the worse they like you, the higher the interest). Income automatically repays loans first; if the borrower is released or dies it's bad debt and the lender's mood suffers. Prisoners with outstanding loans can't ransom themselves.
- **Begging**: success depends on how much the target likes the beggar; small handouts on success, embarrassment on failure.
- **Robbery**: a real melee fight! Whoever falls first loses (can be lethal); the winner takes a share of the victim's balance. The loser gets beaten up and humiliated.
- Optional "borrow from the player": prisoners pop a request window you approve one by one (off by default).

### 👶 儿童父母代付 / Parents Pay for Children
- 儿童囚犯（Biotech，成人年龄线以下）吃饭时，费用可由**在押的父母**代付——生父母/养父母都算，父母被释放/招募/越狱/死亡后代付自动失效
- 三种扣费模式（全局设置）：**先自己**（孩子余额优先，父母补差）/ **先父母**（父母按余额比例分摊，孩子补差）/ **合并钱包**（家庭视为一个账户，按各自余额比例共同分摊，健康页显示合计余额）
- 多位在押父母按**余额比例**平摊（负余额的父母不参与）
- 父母有钱的儿童不会去乞讨/借钱/抢劫；抢劫目标仍只看被抢者自己的实际余额，抢不走父母的饭票
- **赎身同样支持父母代付**：儿童可用余额（含父母）满足赎身要求即可提出请求，批准后从家庭余额中扣除
- 可全局开关（默认开），也可在「配置囚犯」窗口对单个儿童允许/禁止
- Child prisoners (Biotech, below the adult age) can have their meals paid from their **imprisoned parents'** tickets — biological and adopted parents both count; support ends automatically when a parent is released, recruited, escapes or dies.
- Three payment modes (global setting): **Own first** (child's balance pays first, parents cover the shortfall) / **Parents first** (parents split by balance share, child covers the shortfall) / **Shared wallet** (the family is one account; costs are split by each member's balance share, and the health tab shows the combined balance).
- Multiple imprisoned parents **split costs proportionally to their balances** (parents with negative balances don't contribute).
- A child backed by well-off parents won't beg, borrow or rob; robbery targets still count only the victim's own balance — nobody can steal the parents' tickets by attacking the child.
- **Ransom also supports parent pay**: a child can request freedom once the combined available balance (including parents) meets the ransom cost; approving deducts from the family balance.
- Global toggle (on by default) plus a per-prisoner allow/deny in the prisoner config window.

### ⚙️ 其他 / Extras
- **自定义饭票名称**（如改成"代币""劳动券"）
- 设置页三个标签页：通用设置 / 按工种饭票 / 食物价格（带搜索）
- 越狱 / 精神崩溃期间跳过饭票检查（可关闭）
- 中英双语
- Custom ticket name (e.g. "token", "labor voucher").
- Settings tabs: General / Work-type wages / Food prices (searchable).
- Ticket check skipped during prison breaks / mental breaks (toggleable).
- Bilingual UI (简体中文 / English).

---

## 📦 依赖 / Requirements

| 依赖 | 必需 | 说明 |
|------|------|------|
| [Harmony](https://github.com/pardeike/HarmonyRimWorld/releases/latest) | ✅ 必须 | 补丁框架 |
| [Prison Labor](https://steamcommunity.com/sharedfiles/filedetails/?id=1899474310) | ✅ 强烈建议 | 囚犯劳动系统（未安装时仅器官贩卖可赚饭票） |

RimWorld **1.6** required.

---

## 🚀 安装 / Installation

**方式一：Steam 创意工坊（推荐）**
1. 在 [Steam 创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3780939234) 订阅「囚犯付费吃饭2（Prisoners Pay To Eat 2）」
2. 游戏启动器 → MOD → 启用「囚犯付费吃饭2」

> ⚠️ 当前为**实验性（Exp）版本**，功能仍在完善中。

**方式二：直接放入 Mods 目录**
1. 将整个 `PrisonersPayToEat2` 文件夹复制到 `RimWorld/Mods/` 下（注意不是把里面的文件散开，是整个文件夹）
2. 启动游戏 → 游戏启动器 → MOD → 勾选启用「囚犯付费吃饭2」

**方式三：从 GitHub 拉取**
```bash
git clone https://github.com/ikun2522-art/PPTE2.git RimWorld/Mods/PrisonersPayToEat2
```

> ⚠️ 加载顺序：确保本 MOD 在 **Prison Labor** 之后加载（About.xml 已声明 loadAfter）。

---

## 🔨 从源码构建 / Building from Source

需要 .NET Framework 4.8（或支持其的 SDK）+ 本机 RimWorld 安装。

1. 复制 `Source/LocalPaths.props.example` 为 `Source/Directory.Build.props`（该文件已被 git 忽略）
2. 在 `Directory.Build.props` 中填写你自己的路径：
   ```xml
   <RimWorldDir>你的RimWorld安装目录</RimWorldDir>
   <HarmonyDir>包含 0Harmony.dll 的目录</HarmonyDir>
   ```
3. 编译：
   ```bash
   cd Source
   dotnet build PrisonersPayToEat2.csproj -c Release
   ```
4. DLL 与 PDB 自动复制到 `1.6/Assemblies/`

---

## 🗂️ 目录结构 / Structure

```
PrisonersPayToEat2/
├── About/About.xml            # MOD 元数据
├── Defs/RecipeDefs/           # 器官摘取手术配方
├── Languages/                 # 中英双语翻译
├── Source/                    # C# 源码
└── 1.6/Assemblies/            # 编译产物 (dll + pdb)
```

---

## 📄 许可 / License

本项目基于 [MIT License](LICENSE) 发布。

---

*作者：AAA不玩抽象 · Author: AAA不玩抽象 — Made with ❤️ for the RimWorld community*
