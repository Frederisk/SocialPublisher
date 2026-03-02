using Avalonia.Controls;
using Avalonia.Platform.Storage;

using PixivCS.Api;
using PixivCS.Models.Illust;

using SkiaSharp;

using SocialPublisher.Utils;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SocialPublisher.Services;

public interface IUrlAnalysisImagesService {
    //public Task<List<Byte[]>> AnalysisImagesAsync(String uri, String token);
    public IAsyncEnumerable<Byte[]> AnalysisImagesAsync(String url, String storageBookmark, IProgress<String>? progress = null, CancellationToken token = default);
}

public partial class UrlAnalysisImagesService : IUrlAnalysisImagesService {
    private readonly HttpClient _httpClient;
    private readonly PixivAppApi _pixivAppApi;
    private readonly ISettingService _settingsService;

    public UrlAnalysisImagesService(ISettingService settingsService) {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        _pixivAppApi = new PixivAppApi();
        _settingsService = settingsService;
    }

    //public async IAsyncEnumerable<Byte[]> AnalysisImagesAsync(String url, IProgress<String>? progress = null) {
    //    await foreach (var image in AnalysisImagesAsync(url, progress, CancellationToken.None)) {
    //        yield return image;
    //    }
    //}

    public async IAsyncEnumerable<Byte[]> AnalysisImagesAsync(String url, String storageBookmark, IProgress<String>? progress = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        String domain;
        String id;
        IAsyncEnumerable<Byte[]> images;

        if (url.Contains("pixiv.net", StringComparison.OrdinalIgnoreCase)) {
            id = GetPixivIllustId(url);
            if (String.IsNullOrEmpty(id)) {
                yield break;
            }
            domain = "pixiv.net";
            images = AnalysisPixivImagesAsync(id, progress, cancellationToken);
            //await foreach (var image in AnalysisPixivImagesAsync(illustId, progress, cancellationToken)) {
            //    yield return image;
            //}
        } else if (url.Contains("twitter.com", StringComparison.OrdinalIgnoreCase) || url.Contains("x.com", StringComparison.OrdinalIgnoreCase)) {
            id = GetTwitterTweetId(url);
            if (String.IsNullOrEmpty(id)) {
                yield break;
            }
            domain = "twitter.com";
            images = AnalysisTwitterImagesAsync(id, progress, cancellationToken);
            //await foreach (var image in AnalysisTwitterImagesAsync(tweetId, progress, cancellationToken)) {
            //    yield return image;
            //}
        } else {
            progress?.Report("Unsupported URL.");
            yield break;
        }

        //String targetDirectory = String.Empty;
        IStorageFolder? targetFolder = null;
        TopLevel? topLevel = TopLevelHelper.GetTopLevel();
        if (topLevel is not null && !String.IsNullOrEmpty(storageBookmark)) {
            var rootFolder = await topLevel.StorageProvider.OpenFolderBookmarkAsync(storageBookmark);
            if (rootFolder is not null) {
                var domainFolder = await rootFolder.GetOrCreateFolderAsync(domain);
                if (domainFolder is not null) {
                    targetFolder = await domainFolder.GetOrCreateFolderAsync(id);
                }
            }
        }

        Int32 index = 0;
        await foreach (var image in images.WithCancellation(cancellationToken)) {
            if (image is null) {
                continue;
            }
            if (targetFolder is not null) {
                using MemoryStream stream = new MemoryStream(image);
                using SKCodec codec = SKCodec.Create(stream);
                String extension = codec.EncodedFormat.ToString().ToLower();
                String fileName = $"{index:00}.{extension}";
                //String filePath = Path.Combine(targetDirectory, fileName);
                try {
                    var file = await targetFolder.GetOrCreateFileAsync(fileName);
                    if (file is not null) {
                        await using var outStream = await file.OpenWriteAsync();
                        await outStream.WriteAsync(image, cancellationToken);
                        await outStream.FlushAsync(cancellationToken);
                    }

                    //await File.WriteAllBytesAsync(filePath, image, cancellationToken);
                } catch (Exception ex) {
                    progress?.Report($"Save failed for {fileName}: {ex.Message}");
                }
            }

            yield return image;
            index++;
        }
    }

    private static String GetPixivIllustId(String url) => IllustIdRegex().Match(url).Groups[1].Value;

    private static String GetTwitterTweetId(String url) => TweetIdRegex().Match(url).Groups[1].Value;

    private async IAsyncEnumerable<Byte[]> AnalysisPixivImagesAsync(String illustId, IProgress<String>? progress = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        if (!_pixivAppApi.IsAuthenticated) {
            await _pixivAppApi.AuthAsync(_settingsService.Settings.PixivRefreshToken, cancellationToken);
        }
        //String illustId = IllustIdRegex().Match(url).Groups[1].Value;
        IllustDetail illust = await _pixivAppApi.GetIllustDetailAsync(illustId, cancellationToken);
        if (illust.Illust?.PageCount > 1) {
            using SemaphoreSlim throttler = new(4, 4);
            List<Task<Byte[]>> downloadTask = [];

            foreach (var page in illust.Illust.MetaPages) {
                String? imageUrl = page.ImageUrls?.Original ?? page.ImageUrls?.Large ?? page.ImageUrls?.Medium ?? page.ImageUrls?.SquareMedium;
                if (imageUrl is null) {
                    continue;
                }
                downloadTask.Add(Task.Run(async () => {
                    await throttler.WaitAsync(cancellationToken);
                    try {
                        return await _pixivAppApi.DownloadImageAsync(imageUrl, cancellationToken);
                    } finally {
                        throttler.Release();
                    }
                }, cancellationToken));
                //yield return await _pixivAppApi.DownloadImageAsync(imageUrl, cancellationToken);
            }
            foreach (var task in downloadTask) {
                yield return await task;
            }
        } else if (illust.Illust?.PageCount == 1) {
            String? imageUrl = illust.Illust.MetaSinglePage?.OriginalImageUrl;
            if (imageUrl is null) {
                //return results;
                yield break;
            }
            yield return await _pixivAppApi.DownloadImageAsync(imageUrl, cancellationToken);
        } else {
            progress?.Report("No images found in the provided Pixiv URL.");
        }
    }

    private async IAsyncEnumerable<Byte[]> AnalysisTwitterImagesAsync(String tweetId, IProgress<String>? progress = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        //var tweetId = TweetIdRegex().Match(url).Groups[1].Value;
        String apiUrl = $"https://api.fxtwitter.com/status/{tweetId}";

        //try {
        var json = await _httpClient.GetStringAsync(apiUrl, cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("code", out var code) && code.GetInt32() == (Int32)HttpStatusCode.OK) {
            if (root.TryGetProperty("tweet", out var tweet)
                && tweet.TryGetProperty("media", out var media)
                // && media.TryGetProperty("all", out var allMedia)
                && media.TryGetProperty("photos", out var allMedia)
                ) {
                //SemaphoreSlim throttler = new(4);
                List<Task<Byte[]>> downloadTasks = [];
                foreach (var item in allMedia.EnumerateArray()) {
                    if (item.TryGetProperty("url", out var urlElement)) {
                        String? imgUrl = urlElement.GetString();
                        if (imgUrl is null) {
                            continue;
                        }
                        //if (imgUrl.Contains("name="))
                        //    imgUrl = TwitterImageQualityRegex().Replace(imgUrl, "name=orig");
                        //else {
                        //    imgUrl += ":orig";
                        //}

                        //results.Add(await _httpClient.GetByteArrayAsync(imgUrl));
                        //yield return await _httpClient.GetByteArrayAsync(imgUrl, cancellationToken);
                        downloadTasks.Add(_httpClient.GetByteArrayAsync(imgUrl, cancellationToken));
                    }
                }
                foreach (var task in downloadTasks) {
                    yield return await task;
                }
            }
        }
    }

    [GeneratedRegex(@"artworks/(\d+)")]
    private static partial Regex IllustIdRegex();
    [GeneratedRegex(@"status/(\d+)")]
    private static partial Regex TweetIdRegex();

    //[GeneratedRegex(@"name=\w+")]
    //private static partial Regex TwitterImageQualityRegex();

}