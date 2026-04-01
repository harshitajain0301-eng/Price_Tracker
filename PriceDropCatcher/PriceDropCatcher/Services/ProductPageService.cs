using System;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using PriceDropCatcher.Models;

namespace PriceDropCatcher.Services
{
    public class ProductPageService
    {
        private static readonly HttpClient Http = CreateHttp();

        private static HttpClient CreateHttp()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            c.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
            return c;
        }

        public async Task<Product> LoadProductAsync(string productUrl, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(productUrl))
                throw new ArgumentException("URL required.", nameof(productUrl));

            var html = await RetryHelper.RunAsync(
                async () =>
                {
                    var resp = await Http.GetAsync(productUrl, ct).ConfigureAwait(false);
                    resp.EnsureSuccessStatusCode();
                    return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                },
                3,
                ct).ConfigureAwait(false);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var host = new Uri(productUrl).Host.ToLowerInvariant();
            var retailer = GuessRetailer(host);

            var name = ExtractTitle(doc, host) ?? "Product";
            var (priceDisplay, priceUsd) = ExtractPrice(doc, host);

            return new Product
            {
                SourceUrl = productUrl,
                Name = WebUtilityTrim(name),
                PagePriceDisplay = priceDisplay,
                PagePriceUsd = priceUsd,
                RetailerHint = retailer,
                LoadedAt = DateTime.UtcNow,
                ImageUrl = ExtractImage(doc, host)
            };
        }

        private static string GuessRetailer(string host)
        {
            if (host.Contains("amazon.")) return "Amazon";
            if (host.Contains("walmart.")) return "Walmart";
            if (host.Contains("ebay.")) return "eBay";
            return null;
        }

        private static string ExtractTitle(HtmlDocument doc, string host)
        {
            if (host.Contains("amazon."))
                return doc.DocumentNode.SelectSingleNode("//*[@id='productTitle']")?.InnerText;
            if (host.Contains("walmart."))
                return doc.DocumentNode.SelectSingleNode("//h1[@data-automation-id]")?.InnerText;
            if (host.Contains("ebay."))
                return doc.DocumentNode.SelectSingleNode("//*[@id='itemTitle']")?.InnerText;

            return doc.DocumentNode.SelectSingleNode("//*[@id='productTitle']")?.InnerText
                   ?? doc.DocumentNode.SelectSingleNode("//h1[@data-automation-id]")?.InnerText
                   ?? doc.DocumentNode.SelectSingleNode("//*[@id='itemTitle']")?.InnerText;
        }

        private static string ExtractImage(HtmlDocument doc, string host)
        {
            if (host.Contains("amazon."))
                return doc.DocumentNode.SelectSingleNode("//*[@id='landingImage']")?.GetAttributeValue("src", null)
                       ?? doc.DocumentNode.SelectSingleNode("//img[@id='imgBlkFront']")?.GetAttributeValue("src", null);
            if (host.Contains("walmart."))
                return doc.DocumentNode.SelectSingleNode("//img[contains(@class,'prod-hero-image')]")?.GetAttributeValue("src", null);
            if (host.Contains("ebay."))
                return doc.DocumentNode.SelectSingleNode("//*[@id='icImg']")?.GetAttributeValue("src", null);
            return null;
        }

        private static Tuple<string, decimal?> ExtractPrice(HtmlDocument doc, string host)
        {
            string raw = null;
            if (host.Contains("amazon."))
            {
                raw = doc.DocumentNode.SelectSingleNode("//span[contains(@class,'a-price')]//span[contains(@class,'a-offscreen')]")?.InnerText
                      ?? doc.DocumentNode.SelectSingleNode("//*[@id='priceblock_ourprice']")?.InnerText
                      ?? doc.DocumentNode.SelectSingleNode("//*[@id='corePrice_feature_div']//span[contains(@class,'a-offscreen')]")?.InnerText;
            }
            else if (host.Contains("walmart."))
            {
                raw = doc.DocumentNode.SelectSingleNode("//*[@itemprop='price']")?.GetAttributeValue("content", null)
                      ?? doc.DocumentNode.SelectSingleNode("//span[@itemprop='price']")?.InnerText;
            }
            else if (host.Contains("ebay."))
            {
                raw = doc.DocumentNode.SelectSingleNode("//*[@id='prcIsum']")?.InnerText
                      ?? doc.DocumentNode.SelectSingleNode("//*[@itemprop='price']")?.GetAttributeValue("content", null);
            }

            if (string.IsNullOrWhiteSpace(raw)) return Tuple.Create<string, decimal?>(null, null);
            var n = ParseMoney(raw);
            return Tuple.Create(raw.Trim(), n);
        }

        private static decimal? ParseMoney(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var cleaned = Regex.Replace(s, @"[^0-9.]", "");
            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return d;
            return null;
        }

        private static string WebUtilityTrim(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return System.Net.WebUtility.HtmlDecode(s).Trim();
        }
    }
}
