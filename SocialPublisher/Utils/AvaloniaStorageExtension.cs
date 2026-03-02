using Avalonia.Platform.Storage;

using System;
using System.Threading.Tasks;

namespace SocialPublisher.Utils;

public static class AvaloniaStorageExtension {
    public static async Task<IStorageFolder?> GetOrCreateFolderAsync(this IStorageFolder parentFolder, String folderName) {
        await foreach (var item in parentFolder.GetItemsAsync()) {
            if (item is IStorageFolder folder && folder.Name == folderName) {
                return folder;
            }
        }
        return await parentFolder.CreateFolderAsync(folderName);
    }

    public static async Task<IStorageFile?> GetOrCreateFileAsync(this IStorageFolder parentFolder, String fileName) {
        await foreach (var item in parentFolder.GetItemsAsync()) {
            if (item is IStorageFile file && file.Name == fileName) {
                return file;
            }
        }
        return await parentFolder.CreateFileAsync(fileName);
    }
}
