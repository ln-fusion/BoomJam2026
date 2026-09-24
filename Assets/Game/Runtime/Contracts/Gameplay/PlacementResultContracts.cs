using System;
using Game.Foundation;

namespace Game.Contracts.Gameplay
{
    /// <summary>
    /// 部署命令结果: 成功, 或携带 <see cref="PlacementError"/> 说明具体失败原因。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 部署命令不使用 <see cref="Result"/>。技术设计文档 §6.4 把失败原因定为一组固定的
    /// <see cref="PlacementError"/> 值, 而 <see cref="Result"/> 携带的 <see cref="ErrorCode"/>
    /// 没有对应词表; 若在调用侧手工翻译, 同一次失败会在两套词表之间来回映射, 且新增原因时
    /// 容易漏改一处。
    /// </para>
    /// <para>
    /// <see cref="StartSimulationResult"/> 已按同一原因集合定义, 因此部署命令沿用相同形状,
    /// 使整个 <see cref="IStageSession"/> 只有一套部署失败原因。
    /// </para>
    /// </remarks>
    public readonly struct PlacementResult
    {
        /// <summary>命令是否成功。</summary>
        public bool IsSuccess { get; }

        /// <summary>失败原因; 成功时为 <see cref="PlacementError.None"/>。</summary>
        public PlacementError Error { get; }

        /// <summary>人类可读错误消息; 仅用于日志, 不直接展示为玩家文案。</summary>
        public string Message { get; }

        /// <summary>创建部署结果。</summary>
        /// <param name="isSuccess">命令是否成功。</param>
        /// <param name="error">失败原因。</param>
        /// <param name="message">日志用消息。</param>
        private PlacementResult(bool isSuccess, PlacementError error, string message)
        {
            IsSuccess = isSuccess;
            Error = error;
            Message = message ?? string.Empty;
        }

        /// <summary>创建成功结果。</summary>
        /// <returns>不携带失败原因的部署结果。</returns>
        public static PlacementResult Success() => new PlacementResult(true, PlacementError.None, string.Empty);

        /// <summary>创建失败结果。</summary>
        /// <param name="error">失败原因; 不允许为 <see cref="PlacementError.None"/>。</param>
        /// <param name="message">日志用错误消息。</param>
        /// <returns>携带失败原因的部署结果。</returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="error"/> 为 <see cref="PlacementError.None"/> 时抛出: 失败结果必须能说明原因。
        /// </exception>
        public static PlacementResult Failure(PlacementError error, string message = null)
        {
            if (error == PlacementError.None)
                throw new ArgumentException("A failed placement result requires an error code.", nameof(error));
            return new PlacementResult(false, error, message);
        }
    }

    /// <summary>
    /// 携带成功返回值的部署命令结果; 失败语义与 <see cref="PlacementResult"/> 一致。
    /// </summary>
    /// <typeparam name="T">成功结果携带的值类型。</typeparam>
    public readonly struct PlacementResult<T>
    {
        /// <summary>命令是否成功。</summary>
        public bool IsSuccess { get; }

        /// <summary>成功结果携带的返回值。</summary>
        private readonly T _value;

        /// <summary>获取成功结果的值; 失败结果访问时抛出异常。</summary>
        /// <exception cref="InvalidOperationException">当结果为失败时抛出。</exception>
        public T Value =>
            IsSuccess ? _value : throw new InvalidOperationException("A failed placement result has no value.");

        /// <summary>失败原因; 成功时为 <see cref="PlacementError.None"/>。</summary>
        public PlacementError Error { get; }

        /// <summary>人类可读错误消息; 仅用于日志, 不直接展示为玩家文案。</summary>
        public string Message { get; }

        /// <summary>创建带值的部署结果。</summary>
        /// <param name="isSuccess">命令是否成功。</param>
        /// <param name="value">成功返回值。</param>
        /// <param name="error">失败原因。</param>
        /// <param name="message">日志用消息。</param>
        private PlacementResult(bool isSuccess, T value, PlacementError error, string message)
        {
            IsSuccess = isSuccess;
            _value = value;
            Error = error;
            Message = message ?? string.Empty;
        }

        /// <summary>创建成功结果。</summary>
        /// <param name="value">成功返回值。</param>
        /// <returns>携带返回值的部署结果。</returns>
        public static PlacementResult<T> Success(T value) =>
            new PlacementResult<T>(true, value, PlacementError.None, string.Empty);

        /// <summary>创建失败结果。</summary>
        /// <param name="error">失败原因; 不允许为 <see cref="PlacementError.None"/>。</param>
        /// <param name="message">日志用错误消息。</param>
        /// <returns>携带失败原因的部署结果。</returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="error"/> 为 <see cref="PlacementError.None"/> 时抛出: 失败结果必须能说明原因。
        /// </exception>
        public static PlacementResult<T> Failure(PlacementError error, string message = null)
        {
            if (error == PlacementError.None)
                throw new ArgumentException("A failed placement result requires an error code.", nameof(error));
            return new PlacementResult<T>(false, default!, error, message);
        }
    }
}
