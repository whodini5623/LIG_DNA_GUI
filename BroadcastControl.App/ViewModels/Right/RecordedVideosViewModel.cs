using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BroadcastControl.App.ViewModels.Right;

using BroadcastControl.App.Models;
using BroadcastControl.App.Services;

/// <summary>
/// View state and actions for the corresponding RecordedVideos.
/// </summary>
public sealed class RecordedVideosViewModel : INotifyPropertyChanged
{
    private readonly RecordedVideoCatalogService _catalogService;
    private IReadOnlyList<RecordedVideoItem> _videos = Array.Empty<RecordedVideoItem>();
    private IReadOnlyList<RecordedVideoItem> _currentItems = Array.Empty<RecordedVideoItem>();
    private string _statusText = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public RecordedVideosViewModel(RecordedVideoCatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public string? SelectedFolder { get; private set; }

    public IReadOnlyList<RecordedVideoItem> CurrentItems
    {
        get => _currentItems;
        private set
        {
            _currentItems = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public async Task LoadAsync()
    {
        StatusText = "Loading video list...";
        try
        {
            _videos = await _catalogService.LoadAsync();
        }
        catch
        {
            CurrentItems = Array.Empty<RecordedVideoItem>();
            StatusText = "Could not load the video list.";
            throw;
        }

        if (!string.IsNullOrWhiteSpace(SelectedFolder) &&
            _videos.Any(video => string.Equals(video.Folder, SelectedFolder, StringComparison.Ordinal)))
            ShowFolderContents(SelectedFolder);
        else
        {
            SelectedFolder = null;
            ShowFolderList();
        }
    }

    public void SetPlaybackStatus(string statusText) => StatusText = statusText;

    public bool Select(RecordedVideoItem item)
    {
        if (item.IsBack)
        {
            SelectedFolder = null;
            ShowFolderList();
            return true;
        }
        if (item.IsFolder)
        {
            SelectedFolder = item.Folder;
            ShowFolderContents(item.Folder);
            return true;
        }
        return false;
    }

    public Task<string> CacheAsync(RecordedVideoItem item) => _catalogService.CacheAsync(item);

    private void ShowFolderList()
    {
        var folders = _videos.GroupBy(video => video.Folder)
            .OrderByDescending(group => group.Key, StringComparer.Ordinal)
            .Select(group => new RecordedVideoItem
            {
                Folder = group.Key,
                DisplayName = $"[{group.Key}] ({group.Count()})",
                IsFolder = true
            }).ToList();
        CurrentItems = folders;
        StatusText = folders.Count == 0 ? "No recorded video folders." : $"{folders.Count} folders";
    }

    private void ShowFolderContents(string folder)
    {
        var videos = _videos.Where(video => string.Equals(video.Folder, folder, StringComparison.Ordinal))
            .OrderBy(video => video.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        var items = new List<RecordedVideoItem> { new() { DisplayName = "[..]", IsBack = true } };
        items.AddRange(videos);
        CurrentItems = items;
        StatusText = $"{folder} / {videos.Count} videos";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
