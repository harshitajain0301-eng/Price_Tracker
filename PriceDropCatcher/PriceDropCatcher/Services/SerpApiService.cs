using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PriceDropCatcher.Models;

namespace PriceDropCatcher.Services
{
    public class SerpApiService
    {
        private string _apiKey;
        private static readonly HttpClient Http = CreateHttp();

        public SerpApiService(string apiKey)
        {
            _apiKey = apiKey ?? "";
        }

        /// <summary>Update key at runtime (e.g. after user saves settings).</summary>
        public void SetApiKey(string apiKey)
        {
            _apiKey = (apiKey ?? "").Trim();
        }

        private static HttpClient CreateHttp()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("PriceDropCatcher/1.0 (.NET 4.8)");
            return c;
        }

        public bool HasApiKey => !string.IsNullOrWhiteSpace(_apiKey);

        public async Task<IReadOnlyList<PriceResult>> SearchGoogleShoppingAsync(string query, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException("SerpAPI key is missing. Set SerpApiKey in App.config.");

            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Query required.", nameof(query));

            var url =
                "https://serpapi.com/search.json?engine=google_shopping" +
                "&q=" + Uri.EscapeDataString(query.Trim()) +
                "&api_key=" + Uri.EscapeDataString(_apiKey) +
                "&gl=us&hl=en";

            var json = await RetryHelper.RunAsync(
                async () =>
                {
                    var resp = await Http.GetAsync(url, ct).ConfigureAwait(false);
                    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode)
                        throw new HttpRequestException("SerpAPI HTTP " + (int)resp.StatusCode + ": " + body.Substring(0, Math.Min(200, body.Length)));
                    return body;
                },
                3,
                ct).ConfigureAwait(false);

            var root = JObject.Parse(json);
            var errTok = root["error"];
            if (errTok != null && errTok.Type != JTokenType.Null)
            {
                var err = errTok.Type == JTokenType.String ? errTok.Value<string>() : errTok.ToString();
                if (!string.IsNullOrWhiteSpace(err))
                    throw new InvalidOperationException(err);
            }

            var mapped = MapShoppingResults(root);
            return mapped;
        }

        /// <summary>Collect product rows from all known Google Shopping JSON shapes.</summary>
        private static IEnumerable<JToken> EnumerateShoppingItems(JObject raw)
        {
            if (raw == null) yield break;

            var shopping = raw["shopping_results"] as JArray;
            if (shopping != null)
            {
                foreach (var x in shopping) yield return x;
            }

            var inline = raw["inline_shopping_results"] as JArray;
            if (inline != null)
            {
                foreach (var x in inline) yield return x;
            }

            var categorized = raw["categorized_shopping_results"] as JArray;
            if (categorized != null)
            {
                foreach (var cat in categorized)
                {
                    var inner = cat["shopping_results"] as JArray;
                    if (inner == null) continue;
                    foreach (var x in inner) yield return x;
                }
            }

            var filters = raw["filters"] as JArray;
            if (filters != null)
            {
                foreach (var f in filters)
                {
                    var inner = f["shopping_results"] as JArray;
                    if (inner == null) continue;
                    foreach (var x in inner) yield return x;
                }
            }
        }

        private static IReadOnlyList<PriceResult> MapShoppingResults(JObject raw)
        {
            var list = new List<JToken>();
            foreach (var item in EnumerateShoppingItems(raw))
                list.Add(item);

            var byKey = new Dictionary<string, PriceResult>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in list)
            {
                var source = (item["source"] ?? item["store"] ?? item["seller"])?.Value<string>() ?? "";
                var platform = NormalizeRetailer(source);
                if (platform == null) continue;

                var title = (item["title"] ?? item["name"])?.Value<string>() ?? "Product";
                var priceRaw = item["price"]?.Value<string>();
                var extracted = item["extracted_price"];
                decimal? priceNum = null;
                if (extracted != null && extracted.Type != JTokenType.Null)
                {
                    if (extracted.Type == JTokenType.Float || extracted.Type == JTokenType.Integer)
                        priceNum = extracted.Value<decimal>();
                    else if (decimal.TryParse(extracted.Value<string>(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                        priceNum = d;
                }
                if (priceNum == null)
                    priceNum = ParsePriceToNumber(priceRaw);

                var key = (item["product_id"] ?? item["product_link"] ?? item["link"])?.Value<string>()
                          ?? platform + ":" + title;
                key = key.Replace(" ", "_");

                var row = new PriceResult
                {
                    Id = key,
                    Title = title,
                    Platform = platform,
                    PriceDisplay = !string.IsNullOrEmpty(priceRaw)
                        ? priceRaw
                        : (priceNum.HasValue ? "$" + priceNum.Value.ToString("0.00", CultureInfo.InvariantCulture) : "—"),
                    PriceUsd = priceNum,
                    Rating = ReadRating(item),
                    Offer = item["tag"]?.Value<string>()
                            ?? JoinExtensions(item["extensions"] as JArray),
                    Link = item["product_link"]?.Value<string>()
                           ?? item["link"]?.Value<string>()
                           ?? item["url"]?.Value<string>()
                           ?? "#",
                    Thumbnail = item["thumbnail"]?.Value<string>() ?? item["image"]?.Value<string>()
                };

                if (!byKey.TryGetValue(key, out var existing)
                    || (row.PriceUsd != null && existing.PriceUsd == null))
                    byKey[key] = row;
            }

            return new List<PriceResult>(byKey.Values);
        }

        private static double? ReadRating(JToken item)
        {
            var r = item["rating"];
            if (r != null && r.Type != JTokenType.Null)
            {
                if (r.Type == JTokenType.Float || r.Type == JTokenType.Integer) return r.Value<double>();
                if (double.TryParse(r.Value<string>(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    return d;
            }
            var rev = item["reviews"]?["rating"];
            if (rev != null && rev.Type != JTokenType.Null)
            {
                if (rev.Type == JTokenType.Float || rev.Type == JTokenType.Integer) return rev.Value<double>();
            }
            return null;
        }

        private static string JoinExtensions(JArray ext)
        {
            if (ext == null) return null;
            var parts = new List<string>();
            foreach (var x in ext)
            {
                var s = x?.Value<string>();
                if (!string.IsNullOrWhiteSpace(s)) parts.Add(s);
            }
            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }

        private static string NormalizeRetailer(string source)
        {
            if (string.IsNullOrEmpty(source)) return null;
            var s = source.ToLowerInvariant();
            if (s.Contains("amazon")) return "Amazon";
            if (s.Contains("walmart")) return "Walmart";
            if (s.Contains("ebay") || s.Contains("e-bay")) return "eBay";
            return null;
        }

        private static decimal? ParsePriceToNumber(string priceStr)
        {
            if (string.IsNullOrWhiteSpace(priceStr)) return null;
            var cleaned = Regex.Replace(priceStr, @"[^0-9.]", "");
            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
                return n;
            return null;
        }
    }
}
