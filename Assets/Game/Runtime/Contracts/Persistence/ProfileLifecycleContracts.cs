using System.Threading;
using System.Threading.Tasks;
using Game.Foundation;

namespace Game.Contracts.Persistence
{
    public enum ProfileStartupMode
    {
        CreateNew,
        Continue
    }

    public sealed class ProfileStartupDecision
    {
        public ProfileStartupMode Mode { get; }
        public ProfileSave Profile { get; }
        public bool HasRecoveryWarning { get; }

        public ProfileStartupDecision(ProfileStartupMode mode, ProfileSave profile,
            bool hasRecoveryWarning = false)
        {
            Mode = mode;
            Profile = profile;
            HasRecoveryWarning = hasRecoveryWarning;
        }
    }

    public interface IProfileLifecycleService
    {
        Task<Result<ProfileStartupDecision>> LoadOrDecideAsync(CancellationToken cancellationToken);
        Task<Result<ProfileSave>> CreateProfileAsync(string nickname,
            CancellationToken cancellationToken);
    }
}
