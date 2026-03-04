#if ANDROID
using Android.App;
using Android.Provider;
using AndroidUri = Android.Net.Uri;
#endif

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
        // FIXME: On Android, subfolders cannot be created correctly, this is an upstream bug of Avalonia.
        // See: https://github.com/AvaloniaUI/Avalonia/issues/20578
#if ANDROID
        try {
            var context = Application.Context;
            var resolver = context.ContentResolver;
            if (resolver is not null) {
                var androidUri = AndroidUri.Parse(parentFolder.Path.ToString());
                // If it's the root directory, we need to get the tree document id instead of the document id
                String? documentId = DocumentsContract.IsDocumentUri(context, androidUri)
                    ? DocumentsContract.GetDocumentId(androidUri)
                    : DocumentsContract.GetTreeDocumentId(androidUri);
                // Get the parent document URI using the tree URI and document ID
                var parentDocumentUri = DocumentsContract.BuildDocumentUriUsingTree(androidUri, documentId) ?? throw new Exception();
                // Create the new folder
                var newDocUri = DocumentsContract.CreateDocument(resolver, parentDocumentUri, DocumentsContract.Document.MimeTypeDir, folderName)?.ToString();

                if (newDocUri is not null) {
                    await foreach (var item in parentFolder.GetItemsAsync()) {
                        if (item is IStorageFolder newFolder && newFolder.Name == folderName) {
                            return newFolder;
                        }
                    }

                    var topLevel = TopLevelHelper.GetTopLevel();
                    if (topLevel is not null) {
                        // Convert the uri to IStorageFolder and return it
                        return await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(newDocUri));
                    }
                }
            }
        } catch {
            // ignore
        }
#endif
        return await parentFolder.CreateFolderAsync(folderName);
    }

    public static async Task<IStorageFile?> GetOrCreateFileAsync(this IStorageFolder parentFolder, String fileName) {
        await foreach (var item in parentFolder.GetItemsAsync()) {
            if (item is IStorageFile file && file.Name == fileName) {
                // In Android, when attempting to overwrite an existing file,
                // the file is not truncated beforehand,
                // and you also cannot truncate it by setting the length to 0,
                // which will result in file data remnants.
                // So we need to delete the file and create a new one to achieve the overwrite effect.
                // return file;
                await file.DeleteAsync();
                break;
            }
        }

#if ANDROID
        try {
            var context = Application.Context;
            var resolver = context.ContentResolver;
            if (resolver is not null) {
                var androidUri = AndroidUri.Parse(parentFolder.Path.ToString());

                String? documentId = DocumentsContract.IsDocumentUri(context, androidUri)
                    ? DocumentsContract.GetDocumentId(androidUri)
                    : DocumentsContract.GetTreeDocumentId(androidUri);

                var parentDocumentUri = DocumentsContract.BuildDocumentUriUsingTree(androidUri, documentId) ?? throw new Exception();
                // Assign the file mime type to avoid unnecessary conversions.
                var mimeType = "application/octet-stream";
                // Or you can guess the mime type based on the file extension using MimeTypeMap.GetMimeTypeFromExtension() if you have the file extension.
                /*
                var ext = System.IO.Path.GetExtension(fileName).TrimStart('.').ToLower();
                if (!String.IsNullOrEmpty(ext) {
                    var map = MimeTypeMap.Singleton;
                    mimeType = map?.GetMimeTypeFromExtension(ext.ToLowerInvariant()) ?? mimeType;
                }
                */

                var newDocUri = DocumentsContract.CreateDocument(resolver, parentDocumentUri, mimeType, fileName)?.ToString();

                if (newDocUri is not null) {
                    var topLevel = TopLevelHelper.GetTopLevel();
                    if (topLevel is not null) {
                        return await topLevel.StorageProvider.TryGetFileFromPathAsync(new Uri(newDocUri));
                    }
                }
            }
        } catch {
            // ignore
        }
#endif
        return await parentFolder.CreateFileAsync(fileName);
    }
}
