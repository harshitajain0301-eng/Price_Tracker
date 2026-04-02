using System;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
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
            string dom = null;
            if (host.Contains("amazon."))
            {
                dom = doc.DocumentNode.SelectSingleNode("//*[@id='productTitle']")?.InnerText
                      ?? doc.DocumentNode.SelectSingleNode("//*[@id='productTitle']//span[contains(@class,'a-size-large')]")?.InnerText
                      ?? doc.DocumentNode.SelectSingleNode("//h1[contains(@class,'a-size-large')]")?.InnerText;
            }
            else if (host.Contains("walmart."))
            {
                dom = doc.DocumentNode.SelectSingleNode("//h1[@data-automation-id='product-title']")?.InnerText
                      ?? doc.DocumentNode.SelectSingleNode("//h1[@data-automation-id]")?.InnerText;
            }
            else if (host.Contains("ebay."))
            {
                dom = doc.DocumentNode.SelectSingleNode("//*[@id='itemTitle']")?.InnerText
                      ?? doc.DocumentNode.SelectSingleNode("//*[@class='x-item-title__mainTitle']")?.InnerText;
            }
            else
            {
                dom = doc.DocumentNode.SelectSingleNode("//*[@id='productTitle']")?.InnerText
                      ?? doc.DocumentNode.SelectSingleNode("//h1[@data-automation-id='product-title']")?.InnerText
                      ?? doc.DocumentNode.SelectSingleNode("//h1[@data-automation-id]")?.InnerText
                      ?? doc.DocumentNode.SelectSingleNode("//*[@id='itemTitle']")?.InnerText;
            }

            if (!string.IsNullOrWhiteSpace(dom))
                return dom;

            var meta = ExtractTitleFromMeta(doc);
            if (!string.IsNullOrWhiteSpace(meta))
                return meta;

            var jsonLd = ExtractProductNameFromJsonLd(doc);
            if (!string.IsNullOrWhiteSpace(jsonLd))
                return jsonLd;

            return SanitizeHtmlTitle(doc.DocumentNode.SelectSingleNode("//title")?.InnerText);
        }

        private static string ExtractTitleFromMeta(HtmlDocument doc)
        {
            var og = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']")?.GetAttributeValue("content", null);
            if (!string.IsNullOrWhiteSpace(og)) return og;
            var tw = doc.DocumentNode.SelectSingleNode("//meta[@name='twitter:title']")?.GetAttributeValue("content", null);
            if (!string.IsNullOrWhiteSpace(tw)) return tw;
            return doc.DocumentNode.SelectSingleNode("//meta[@name='title']")?.GetAttributeValue("content", null);
        }

        private static string ExtractProductNameFromJsonLd(HtmlDocument doc)
        {
            var scripts = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
            if (scripts == null) return null;
            foreach (var script in scripts)
            {
                var raw = script.InnerText;
                if (string.IsNullOrWhiteSpace(raw)) continue;
                try
                {
                    var token = JToken.Parse(raw);
                    var name = TryReadProductNameFromJsonLd(token);
                    if (!string.IsNullOrWhiteSpace(name)) return name;
                }
                catch
                {
                    // skip malformed JSON
                }
            }
            return null;
        }

        private static string TryReadProductNameFromJsonLd(JToken token)
        {
            if (token == null) return null;
            if (token.Type == JTokenType.Array)
            {
                foreach (var item in token)
                {
                    var n = TryReadProductNameFromJsonLd(item);
                    if (!string.IsNullOrWhiteSpace(n)) return n;
                }
                return null;
            }

            if (token.Type != JTokenType.Object) return null;
            var o = (JObject)token;

            if (o["@graph"] is JArray graph)
            {
                foreach (var item in graph)
                {
                    var n = TryReadProductNameFromJsonLd(item);
                    if (!string.IsNullOrWhiteSpace(n)) return n;
                }
            }

            if (IsJsonLdProductType(o["@type"]))
            {
                var name = o["name"]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }

            return null;
        }

        private static bool IsJsonLdProductType(JToken typeTok)
        {
            if (typeTok == null || typeTok.Type == JTokenType.Null) return false;
            if (typeTok.Type == JTokenType.String)
                return typeTok.Value<string>().IndexOf("Product", StringComparison.OrdinalIgnoreCase) >= 0;
            if (typeTok.Type == JTokenType.Array)
            {
                foreach (var x in typeTok)
                {
                    var s = x?.Value<string>();
                    if (!string.IsNullOrEmpty(s) && s.IndexOf("Product", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            return false;
        }

        private static string SanitizeHtmlTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;
            var t = WebUtilityTrim(title);
            t = Regex.Replace(t, @"\s*[\|\-:]\s*Amazon[^\|]*$", "", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"\s*[\|\-:]\s*eBay[^\|]*$", "", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"\s*[\|\-:]\s*Walmart[^\|]*$", "", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"\s*[\|\-:]\s*Flipkart[^\|]*$", "", RegexOptions.IgnoreCase);
            return string.IsNullOrWhiteSpace(t) ? null : t.Trim();
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
