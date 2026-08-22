using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Persistence;
using Game.Foundation;

namespace Game.Persistence
{
    /// <summary>
    /// 玩家档案生命周期服务，负责启动决策和新档案创建。
    /// </summary>
    public sealed class ProfileLifecycleService : IProfileLifecycleService
    {
        private static readonly ErrorCode InvalidNickname =
            new ErrorCode(ErrorCategory.Validation, "profile.nickname_invalid");
        private static readonly ErrorCode SaveFailed =
            new ErrorCode(ErrorCategory.SaveIo, "profile.create_failed");
        private static readonly char[] InvalidNicknameChars = { '\r', '\n', '\t' };

        private readonly ISaveRepository _repository;
        private readonly IClock _clock;

        /// <summary>创建玩家档案生命周期服务。</summary>
        /// <param name="repository">本地存档仓储。</param>
        /// <param name="clock">创建档案时间使用的时钟。</param>
        public ProfileLifecycleService(ISaveRepository repository, IClock clock = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _clock = clock ?? new SystemClock();
        }

        /// <summary>加载档案并决定进入创建新档案还是继续已有档案流程。</summary>
        /// <param name="cancellationToken">取消标记。</param>
        /// <returns>启动决策结果。</returns>
        public async Task<Result<ProfileStartupDecision>> LoadOrDecideAsync(
            CancellationToken cancellationToken)
        {
            LoadResult<ProfileSave> result = await _repository.LoadProfileAsync(cancellationToken);
            if (result.Source == LoadSource.NotFound || result.Data == null)
            {
                return Result<ProfileStartupDecision>.Success(
                    new ProfileStartupDecision(ProfileStartupMode.CreateNew, null,
                        result.HasRecoveryWarning));
            }

            return Result<ProfileStartupDecision>.Success(
                new ProfileStartupDecision(ProfileStartupMode.Continue, result.Data,
                    result.HasRecoveryWarning));
        }

        /// <summary>校验昵称、创建单一玩家档案并保存。</summary>
        /// <param name="nickname">玩家输入的昵称。</param>
        /// <param name="cancellationToken">取消标记。</param>
        /// <returns>创建出的档案或失败结果。</returns>
        public async Task<Result<ProfileSave>> CreateProfileAsync(string nickname,
            CancellationToken cancellationToken)
        {
            string normalizedNickname = nickname == null ? string.Empty : nickname.Trim();
            if (normalizedNickname.Length < 1 || normalizedNickname.Length > 32 ||
                normalizedNickname.IndexOfAny(InvalidNicknameChars) >= 0)
                return Result<ProfileSave>.Failure(InvalidNickname,
                    "Nickname must contain 1 to 32 visible characters.");

            string now = _clock.UtcNow.ToString("O");
            var profile = new ProfileSave
            {
                ProfileId = Guid.NewGuid().ToString("N"),
                PlayerNickname = normalizedNickname,
                CreatedAtUtc = now,
                LastModifiedAtUtc = now
            };

            SaveResult saved = await _repository.SaveProfileAsync(profile,
                SaveReason.ProfileCreated, cancellationToken);
            return saved.IsSuccess
                ? Result<ProfileSave>.Success(profile)
                : Result<ProfileSave>.Failure(SaveFailed, saved.Message);
        }
    }
}
