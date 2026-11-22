using LCAbnormalities.Settings;

namespace MeatLantern.Setting
{
    /// <summary>
    /// 保留旧静态访问门面以维持兼容性；统一设置入口由 LCAbnormalitiesMod 接管。
    /// </summary>
    public static class Setting_MeatLantern_Main
    {
        public static Setting_MeatLantern Settings => LCAbnormalitiesMod.Settings;
    }
}