# 囚犯付费吃饭2（Prisoners Pay To Eat 2）

[![RimWorld 1.6](https://img.shields.io/badge/RimWorld-1.6-blue)](https://store.steampowered.com/app/294100/RimWorld/)

囚犯再也不能白嫖殖民地的食物了！每一位囚犯进食都必须支付「饭票」，没有饭票就只能挨饿。

Prisoners can no longer freeload off your colony's food supply! Every meal a prisoner eats must be paid for with a meal ticket — no tickets, no food.

---

## ✨ 功能特性 / Features

### 🍚 饭票经济 / Meal Ticket Economy
- 囚犯进食必须支付饭票，价格由食物的**市场价值**决定（精致食物贵，简单食物便宜，营养膏按营养值折算）
- 支持小数饭票（如 0.01 张），价格与余额精确到百分位
- 支持**逐种食物单独定价**：设置页可对每种食物单独设置饭票价格
- Prisoners must pay meal tickets to eat. Price is based on the food's **market value**.
- Fractional tickets supported (e.g. 0.01); prices and balances work down to two decimals.
- Per-food pricing: override the ticket cost of any individual food item.

### ⛏️ Prison Labor 打工赚饭票 / Earn Tickets Through Prison Labor
- 集成 Avius 的 **Prison Labor** MOD：哪些工种囚犯能做、工作区域、动机系统完全由 Prison Labor 管理
- 按**工作种类**分别设置每小时饭票（如挖矿 2/小时、清洁 1/小时、种植 0.5/小时）
- 全局工资倍率 + 每个囚犯的**个人工资倍率**可叠加调整
- Integrates Avius's **Prison Labor** MOD: which work types a prisoner may do is fully decided by Prison Labor.
- Per-work-type hourly wages (e.g. mining 2/hr, cleaning 1/hr, growing 0.5/hr).
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

### 🎛️ 玩家管理 / Player Controls
- 选中囚犯：**发放饭票** / **扣除饭票** / **配置囚犯** 按钮 + 饭票余额卡片
- 每个囚犯单独配置：个人食物倍率、个人工资倍率、是否允许贩卖器官
- 手动发放/扣除支持小数
- Selected prisoner gizmos: give tickets / take tickets / configure / live balance card.
- Per-prisoner settings: food multiplier, wage multiplier, organ-sale permission.
- Manual give/take supports decimals.

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

> ⚠️ 本 MOD 目前**尚未发布到 Steam 创意工坊**，也未提供 Releases 下载包。以下为本地/手动安装方式。

**方式一：直接放入 Mods 目录**
1. 将整个 `PrisonersPayToEat2` 文件夹复制到 `RimWorld/Mods/` 下（注意不是把里面的文件散开，是整个文件夹）
2. 启动游戏 → 游戏启动器 → MOD → 勾选启用「囚犯付费吃饭2」

**方式二：从 GitHub 拉取**
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
