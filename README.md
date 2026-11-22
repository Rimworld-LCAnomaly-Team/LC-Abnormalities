# LC Abnormalities

面向 RimWorld 1.7（.NET 10）的《脑叶公司》异想体合集包，仅依赖 `LC Anomaly Core`（`DarthCY.LC.AnomalyCore`），不依赖 Anomaly DLC。

## 当前收录异想体（共 20 项）

### 基线收录（10 项）

- **一罪与百善**（`OneSin`，ZAYIN，标准）— 白伤近战短杖 / 防具 / 饰品
- **七宗罪实体**（`SevenSinEntity_ZAYIN`，6 种，ZAYIN，衍生）— 四色近战罪孽实体
- **我们可以改变一切**（`WeCanChangeAnything`，ZAYIN，持续封闭工具）— 消耗生命产出 PE-Box
- **古老的信念与承诺**（`OldBeliefAndPromise`，ZAYIN，单次交互工具）— 武器永久增伤赐福
- **肉食提灯**（`MeatLantern`，TETH，标准）— 黑伤近战武器 / 防具 / 饰品
- **蕾蒂希娅**（`Laetitia`，HE，标准）— 黑伤远程步枪 / 防具 / 饰品
- **异界肖像**（`PortraitOfAnotherWorld`，HE，单次交互工具）— 替身伤害转嫁
- **穿刺乐园**（`BurrowingHeaven`，WAW，标准）— 红伤近战长矛 / 防具 / 饰品
- **血肉偶像**（`FleshIdol`，WAW，持续祈祷工具）— 全殖民地周期治疗与理智脉冲
- **一无所有**（`NothingThere`，ALEPH，标准）— 红伤吸血大剑「拟态」与三阶段变形战斗

### 第 1 期扩充（5 项）

- **妖精盛宴**（`FairyFestival`，`F-04-83`，ZAYIN，标准）— 「翅振」红伤近战吸血拳套 / 红黑抗甲 / 护手饰品；带有「妖精守护」工作其他异想体将触发啃噬处决
- **韦尔奇乐汽水**（`WellCheers`，`F-05-52`，ZAYIN，标准）— 「苏打」红伤手枪 / 蓝红汽水甲 / 海虾饰品；良/优工作恢复生命或理智，差工作低温昏迷并被拖去捕虾
- **惩戒鸟**（`O-02-56`，`PunishingBird`，TETH，标准）— 「喙」红伤速射手枪 / 高红抗甲 / 羽毛发饰；受击暴怒切换全属性 0.1 抗性并以巨口处决
- **虚无之梦**（`T-02-99`，`VoidDream`，TETH，标准）— 「沉沦之梦」白伤二连发远程枪 / 黑白抗甲 / 羊角饰品；出逃释放范围催眠嚎叫，需队友唤醒
- **渴望之心**（`T-09-77`，`HeartOfAspiration`，TETH，佩戴型工具）— 大幅强化近战/射速/移速/承伤；佩戴后需限时参战，否则狂暴并造成红白反噬

### 第 2 期扩充（5 项）

- **焦化少女**（`F-01-02`，`ScorchedGirl`，TETH，标准）— 「第四根火柴」红伤爆破榴弹炮 / 高火红抗甲 / 火柴口饰；出逃点燃 20 秒引信冲锋自爆，击杀则安抚化卵
- **魔弹射手**（`F-01-69`，`DerFreischutz`，HE，标准）— 「魔弹」（**WAW** 超远贯穿黑伤步枪）/ 全抗均衡甲 / 烟斗口饰；支持全图付费请求魔弹射击，第 7 发必定诅咒贯穿殖民者
- **亡蝶葬仪**（`T-01-68`，`FuneralOfDeadButterflies`，HE，标准）— 「圣宣」（**WAW** 白+黑双色双枪）/ 黑抗甲 / 蝶翼饰品；出逃以蝴蝶弹幕与开棺爆发作战
- **雪皇后**（`F-01-37`，`SnowQueen`，HE，标准）— 「霜之碎片」白伤减速长矛 / 极寒高白抗甲 / 冰之吻面饰；二次亲吻冰封目标，可派遣决斗营救并双人获赠饰品
- **狂徒笔记**（`T-09-78`，`NotesFromCrazedResearcher`，HE，佩戴型工具）— 显著提升研究/工作/外交能力；累计受伤 ≥100 或违规提前归还时触发剧烈爆炸

## 目录结构约定

- `Cont/`：共享贴图与音效资源，按异想体子目录或资源分类存放。
- `Source/Art/<Abnormality>/`：各异想体美术工程源文件。
- `1.7/Defs/`：按标准 RimWorld Def 类型分目录存放（其中 `1.7/Defs/AnimationDefs/` 保持与 `LC-Anomaly-AnimationStudio` 兼容）；存在同类文件时按 `<Type>_<Abnormality>.xml` 命名，禁止同路径冲突。
- `1.7/Languages/`：统一维护 `ChineseSimplified (简体中文)` 与 `English` 翻译，按 `<Type>_<Abnormality>.xml` 分文件存放。
- `Source/1.7/LCAbnormalities/<Abnormality>/`：每个异想体占独立子目录并保留原有命名空间，统一编译为单一程序集 `1.7/Assemblies/LCAbnormalities.dll`。
- `WeaponTweakData/`：近战武器挥砍/偏移兼容数据，使用合集 `TextureModID`（`darthcy.lc.abnormalities`）。

## 新增异想体步骤

1. 将贴图与音效放入 `Cont/`，美术源文件放入 `Source/Art/<Abnormality>/`。
2. 将 XML Def 放入 `1.7/Defs/` 对应分类，文件名使用 `<Type>_<Abnormality>.xml` 避免冲突。
3. 在 `1.7/Languages/` 补充中英文翻译。
4. 将 C# 代码放入 `Source/1.7/LCAbnormalities/<Abnormality>/`，并在 `LCAbnormalities.csproj` 中添加 `<Compile Include="..." />`。

## 构建与验证

```powershell
# 构建合集程序集
dotnet build './Source/1.7/LCAbnormalities/LCAbnormalities.csproj' -c Release

# 从 LC Anomaly Core 运行全系列构建与 1.7 冒烟验证
../LC Anomaly Core/Tools/1.7/BuildFamily.ps1
../LC Anomaly Core/Tools/1.7/RuntimeSmokeTest.ps1 -DlcProfile WithoutAnomaly -Language ChineseSimplified
```
