# LC Abnormalities

面向 RimWorld 1.7（.NET 10）的《脑叶公司》异想体合集包，仅依赖 `LC Anomaly Core`（`DarthCY.LC.AnomalyCore`），不依赖 Anomaly DLC。

## 当前收录异想体

- **一罪与百善**（`OneSin`，ZAYIN）
- **肉食提灯**（`MeatLantern`，TETH）
- **蕾蒂希娅**（`Laetitia`，HE）
- **穿刺乐园**（`BurrowingHeaven`，WAW）

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
