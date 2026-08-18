using System.Threading;
using System.Threading.Tasks;

namespace Game.Contracts.Persistence
{
    public interface ISaveRepository
    {
        Task<LoadResult<SettingsSave>> LoadSettingsAsync(CancellationToken cancellationToken);
        Task<LoadResult<ProfileSave>> LoadProfileAsync(CancellationToken cancellationToken);
        Task<SaveResult> SaveSettingsAsync(SettingsSave data,
            CancellationToken cancellationToken);
        Task<SaveResult> SaveProfileAsync(ProfileSave data, SaveReason reason,
            CancellationToken cancellationToken);
    }
}
