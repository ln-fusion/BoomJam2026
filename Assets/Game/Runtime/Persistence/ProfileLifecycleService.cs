using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Persistence;
using Game.Foundation;

namespace Game.Persistence
{
    public sealed class ProfileLifecycleService : IProfileLifecycleService
    {
        private static readonly ErrorCode InvalidNickname =
            new ErrorCode(ErrorCategory.Validation, "profile.nickname_invalid");
        private static readonly ErrorCode SaveFailed =
            new ErrorCode(ErrorCategory.SaveIo, "profile.create_failed");
        private static readonly char[] InvalidNicknameChars = { '\r', '\n', '\t' };

        private readonly ISaveRepository _repository;
        private readonly IClock _clock;

        public ProfileLifecycleService(ISaveRepository repository, IClock clock = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _clock = clock ?? new SystemClock();
        }

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
