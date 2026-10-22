using System;
using System.Threading.Tasks;
using WinTaskbar.Models;

namespace WinTaskbar.Services
{
    public interface IMediaService
    {
        MediaTrackInfo CurrentTrack { get; }
        event EventHandler? TrackChanged;

        Task UpdateAsync();
        Task TogglePlayPauseAsync();
        Task NextAsync();
        Task PreviousAsync();
        void OpenSpotify();
    }
}
