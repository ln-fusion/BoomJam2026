namespace Game.Foundation
{
    /// <summary>
    /// 角色表情稳定 ID，与立绘表情差分资源中的标识对应。
    /// </summary>
    [System.Serializable]
    public sealed class ExpressionId : StrongId<ExpressionId>
    {
        /// <summary>创建角色表情稳定标识。</summary>
        /// <param name="value">稳定 ID（如 official.expression.hani.smile）。</param>
        public ExpressionId(string value)
            : base(value) { }
    }
}
