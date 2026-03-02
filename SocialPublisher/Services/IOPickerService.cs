using Avalonia.Controls;
using Avalonia.Platform.Storage;

using SocialPublisher.Utils;

using System;
using System.Linq;
using System.Threading.Tasks;

namespace SocialPublisher.Services;

public interface IIOPickerService {
    public Task<String> PickFolderAsync(String oldBookmark);
}

public class IOPickerService : IIOPickerService {
    public async Task<String> PickFolderAsync(String oldBookmark) {
        TopLevel? topLevel = TopLevelHelper.GetTopLevel();
        if (topLevel is null) {
            return oldBookmark;
        }
        using var oldFolder = await topLevel.StorageProvider.OpenFolderBookmarkAsync(oldBookmark);
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
            Title = "Select a folder to save your posts",
            SuggestedStartLocation = oldFolder,
            AllowMultiple = false
        });
        IStorageFolder? selected = folders.FirstOrDefault();
        if (selected is null) {
            return oldBookmark;
        }
        return await selected.SaveBookmarkAsync() ?? oldBookmark;
    }
}
