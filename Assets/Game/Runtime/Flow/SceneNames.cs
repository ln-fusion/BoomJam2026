namespace Game.Flow
{
    /// <summary>
    /// 功能场景名常量：与 Build Settings 中的场景文件路径保持一致。
    /// </summary>
    public static class SceneNames
    {
        /// <summary>引导场景（组合根常驻）</summary>
        public const string Bootstrap = "00_Bootstrap";

        /// <summary>开始菜单场景</summary>
        public const string StartMenu = "01_StartMenu";

        /// <summary>主界面（驾驶舱）场景</summary>
        public const string MetaHub = "02_MetaHub";

        /// <summary>剧情场景</summary>
        public const string Story = "03_Story";

        /// <summary>关卡玩法场景</summary>
        public const string Gameplay = "04_Gameplay";
    }
}
