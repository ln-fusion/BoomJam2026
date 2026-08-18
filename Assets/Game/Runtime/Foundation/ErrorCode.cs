using System;
using System.Collections.Generic;

namespace Game.Foundation
{
    /// <summary>
    /// 统一错误类别：错误所属的模块边界，用于日志聚合与平台上报.
    /// </summary>
    public enum ErrorCategory
    {
        /// <summary>未归类错误</summary>
        Unknown = 0,

        /// <summary>参数错误</summary>
        InvalidArgument,

        /// <summary>依赖资源缺失</summary>
        MissingAsset,

        /// <summary>IO/网络/平台错误</summary>
        Infrastructure,

        /// <summary>场景流转错误（Additive 加载/卸载/激活失败）</summary>
        SceneTransition,
    }

    /// <summary>
    /// 错误码：标记具体失败原因；新增时保持数值向后兼容，不重排既有枚举值.
    /// </summary>
    public enum ErrorCode
    {
        None = 0,
        Unknown = 1,
        InvalidArgument = 2,
        NotFound = 3,
        AlreadyExists = 4,
        OperationNotAllowed = 5,
        SaveFailed = 6,
        LoadFailed = 7,

        /// <summary>场景加载失败（Build Settings 缺失或加载中断）</summary>
        SceneLoadFailed = 8,

        /// <summary>场景卸载失败</summary>
        SceneUnloadFailed = 9,

        /// <summary>异步操作被取消</summary>
        OperationCancelled = 10,
    }
}
