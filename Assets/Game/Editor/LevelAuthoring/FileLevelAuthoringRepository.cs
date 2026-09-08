using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Foundation;
using UnityEngine;

namespace Game.Editor.Level
{
    /// <summary>
    /// 基于文件系统的关卡 Authoring 仓库; 文件名为 {LevelId}.level.authoring.json。
    /// </summary>
    /// <remarks>
    /// 仓库不调用 AssetDatabase.Refresh; 由调用方（如 C20 编辑器窗口）负责刷新。
    /// 损坏或格式不兼容的文件被跳过并记录日志, 不阻断其它关卡加载。
    /// </remarks>
    public sealed class FileLevelAuthoringRepository : ILevelAuthoringRepository
    {
        private readonly string _rootPath;

        /// <summary>创建文件仓库。</summary>
        /// <param name="rootPath">Authoring 文件根目录; 如 Assets/Game/Content/Authoring/Levels。</param>
        public FileLevelAuthoringRepository(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
                throw new ArgumentException("A repository root path is required.", nameof(rootPath));
            _rootPath = rootPath;
        }

        /// <summary>枚举全部关卡源数据, 按关卡稳定 ID 排序。</summary>
        /// <returns>只读关卡源数据列表。</returns>
        public IReadOnlyList<LevelAuthoringData> GetAllLevels()
        {
            var result = new List<LevelAuthoringData>();
            if (!Directory.Exists(_rootPath))
                return result;
            foreach (string file in Directory.EnumerateFiles(_rootPath, "*.level.authoring.json"))
            {
                if (TryLoadFile(file, out LevelAuthoringData data) && data?.Definition != null)
                    result.Add(data);
            }
            return result.OrderBy(data => data.Definition.LevelId, StringComparer.Ordinal).ToList();
        }

        /// <summary>尝试加载指定关卡源数据。</summary>
        /// <param name="levelId">关卡稳定标识。</param>
        /// <param name="data">加载出的关卡源数据; 不存在或损坏时为 null。</param>
        /// <returns>加载成功返回 true。</returns>
        public bool TryLoad(LevelId levelId, out LevelAuthoringData data)
        {
            if (levelId == null)
                throw new ArgumentNullException(nameof(levelId));
            string path = GetFilePath(levelId);
            if (!File.Exists(path))
            {
                data = null;
                return false;
            }
            return TryLoadFile(path, out data);
        }

        /// <summary>保存关卡源数据; 覆盖同 ID 的既有文件。</summary>
        /// <param name="levelId">关卡稳定标识。</param>
        /// <param name="data">待保存数据。</param>
        /// <returns>保存结果。</returns>
        public Result Save(LevelId levelId, LevelAuthoringData data)
        {
            if (levelId == null)
                throw new ArgumentNullException(nameof(levelId));
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (
                data.Definition == null
                || !string.Equals(levelId.Value, data.Definition.LevelId, StringComparison.Ordinal)
            )
            {
                return Result.Failure(
                    ErrorCode.InvalidArgument,
                    "LevelId must match the definition inside the authoring data."
                );
            }
            string path = GetFilePath(levelId);
            return SafeJsonWrite.Write(
                path,
                LevelAuthoringSerializer.Serialize(data),
                json => LevelAuthoringSerializer.TryDeserialize(json, out _)
            );
        }

        /// <summary>删除指定关卡源数据。</summary>
        /// <param name="levelId">关卡稳定标识。</param>
        /// <returns>删除结果。</returns>
        public Result Delete(LevelId levelId)
        {
            if (levelId == null)
                throw new ArgumentNullException(nameof(levelId));
            string path = GetFilePath(levelId);
            if (!File.Exists(path))
                return Result.Failure(ErrorCode.NotFound, "Level authoring file does not exist: " + path);
            try
            {
                File.Delete(path);
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(ErrorCode.SaveFailed, ex.Message);
            }
        }

        /// <summary>把关卡稳定标识转换为 Authoring 文件路径。</summary>
        /// <param name="levelId">关卡稳定标识。</param>
        /// <returns>完整文件路径。</returns>
        private string GetFilePath(LevelId levelId) => Path.Combine(_rootPath, levelId.Value + ".level.authoring.json");

        /// <summary>读取并解析单个文件; 非法内容记录日志并返回失败。</summary>
        /// <param name="path">文件路径。</param>
        /// <param name="data">解析出的数据; 失败时为 null。</param>
        /// <returns>解析成功返回 true。</returns>
        private static bool TryLoadFile(string path, out LevelAuthoringData data)
        {
            string json = File.ReadAllText(path);
            if (LevelAuthoringSerializer.TryDeserialize(json, out data) && data?.Definition != null)
                return true;
            Debug.LogWarning("[FileLevelAuthoringRepository] Skip invalid level authoring file: " + path);
            data = null;
            return false;
        }
    }
}
