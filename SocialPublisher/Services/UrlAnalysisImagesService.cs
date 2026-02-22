using PixivCS.Api;
using PixivCS.Models.Illust;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace SocialPublisher.Services;

public interface IUrlAnalysisImagesService {
    //public Task<List<Byte[]>> AnalysisImagesAsync(String uri, String token);
    public IAsyncEnumerable<Byte[]> AnalysisImagesAsync(String url, IProgress<String>? progress = null);
}

public partial class UrlAnalysisImagesService : IUrlAnalysisImagesService {
    private readonly HttpClient _httpClient;
    private readonly PixivAppApi _pixivAppApi;
    private readonly ISettingService _settingsService;

    //private const String PixivRefreshToken = "";

    public UrlAnalysisImagesService(ISettingService settingsService) {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        _pixivAppApi = new PixivAppApi();
        _settingsService = settingsService;
    }

    public async IAsyncEnumerable<Byte[]> AnalysisImagesAsync(String url, IProgress<String>? progress = null) {
        await foreach (var image in AnalysisImagesAsync(url, progress, CancellationToken.None)) {
            yield return image;
        }
    }

    public async IAsyncEnumerable<Byte[]> AnalysisImagesAsync(String url, IProgress<String>? progress = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        if (url.Contains("pixiv.net", StringComparison.OrdinalIgnoreCase)) {
            await foreach (var image in AnalysisPixivImagesAsync(url, progress, cancellationToken)) {
                yield return image;
            }
        } else if (url.Contains("twitter.com", StringComparison.OrdinalIgnoreCase) || url.Contains("x.com", StringComparison.OrdinalIgnoreCase)) {
            await foreach (var image in AnalysisTwitterImagesAsync(url, progress, cancellationToken)) {
                yield return image;
            }
        }
        progress?.Report("Unsupported URL.");
    }


    private async IAsyncEnumerable<Byte[]> AnalysisPixivImagesAsync(String url, IProgress<String>? progress = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        if (!_pixivAppApi.IsAuthenticated) {
            await _pixivAppApi.AuthAsync(_settingsService.Settings.PixivRefreshToken, cancellationToken);
        }
        String illustId = IllustIdRegex().Match(url).Groups[1].Value;
        IllustDetail illust = await _pixivAppApi.GetIllustDetailAsync(illustId, cancellationToken);
        if (illust.Illust?.PageCount > 1) {
            foreach (var page in illust.Illust.MetaPages) {
                String? imageUrl = page.ImageUrls?.Original ?? page.ImageUrls?.Large ?? page.ImageUrls?.Medium ?? page.ImageUrls?.SquareMedium;
                if (imageUrl is null) {
                    continue;
                }
                yield return await _pixivAppApi.DownloadImageAsync(imageUrl, cancellationToken);
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

    private async IAsyncEnumerable<Byte[]> AnalysisTwitterImagesAsync(String url, IProgress<String>? progress = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        var match = TweetIdRegex().Match(url);
        if (!match.Success) {
            yield break;
        }

        String tweetId = match.Groups[1].Value;
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
                        yield return await _httpClient.GetByteArrayAsync(imgUrl, cancellationToken);
                    }
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