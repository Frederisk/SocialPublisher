using PixivCS.Api;
using PixivCS.Models.Illust;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SocialPublisher.Services;

public partial class UrlAnalysisImagesService : IUrlAnalysisImagesService {
    private readonly HttpClient _httpClient;
    private readonly PixivAppApi _pixivAppApi;
    //private const String PixivRefreshToken = "";

    public UrlAnalysisImagesService() {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        _pixivAppApi = new PixivAppApi();
    }

    public async Task<List<Byte[]>> AnalysisImagesAsync(String url, String token) {
        if (url.Contains("pixiv.net", StringComparison.OrdinalIgnoreCase)) {
            return await AnalysisPixivImagesAsync(url, token);
        } else if (url.Contains("twitter.com", StringComparison.OrdinalIgnoreCase) || url.Contains("x.com", StringComparison.OrdinalIgnoreCase)) {
            return await AnalysisTwitterImagesAsync(url);
        }
        return [];
    }

    private async Task<List<Byte[]>> AnalysisPixivImagesAsync(String url, String token) {
        List<Byte[]> results = [];
        try {
            if (!_pixivAppApi.IsAuthenticated) {
                await _pixivAppApi.AuthAsync(token);
            }
            String illustId = IllustIdRegex().Match(url).Groups[1].Value;
            IllustDetail illust = await _pixivAppApi.GetIllustDetailAsync(illustId);
            if (illust.Illust?.PageCount > 1) {
                foreach (var page in illust.Illust.MetaPages) {
                    String? imageUrl = page.ImageUrls?.Original ?? page.ImageUrls?.Large ?? page.ImageUrls?.Medium ?? page.ImageUrls?.SquareMedium;
                    if (imageUrl is null) {
                        continue;
                    }
                    results.Add(await _pixivAppApi.DownloadImageAsync(imageUrl));
                }
            }
        } catch {
            // ignore
        }
        return results;
    }

    private async Task<List<Byte[]>> AnalysisTwitterImagesAsync(String url) {

        var results = new List<Byte[]>();

        // 1. 從網址提取 Tweet ID (例如 https://x.com/user/status/123456789)
        var match = Regex.Match(url, @"status/(\d+)");
        if (!match.Success) return results;
        String tweetId = match.Groups[1].Value;

        // 2. 呼叫 JSON API
        String apiUrl = $"https://api.fxtwitter.com/status/{tweetId}";

        try {
            var json = await _httpClient.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 檢查 code 是否為 200
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

                            results.Add(await _httpClient.GetByteArrayAsync(imgUrl));
                        }
                    }
                }
            }
        } catch {
            // ignore
        }

        return results;
    }

    [GeneratedRegex(@"artworks/(\d+)")]
    private static partial Regex IllustIdRegex();

    //[GeneratedRegex(@"name=\w+")]
    //private static partial Regex TwitterImageQualityRegex();

}