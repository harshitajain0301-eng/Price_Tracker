const US_SHOPPING_PARAMS = {
  engine: "google_shopping",
  gl: "us",
  hl: "en",
  location: "United States",
};

function normalizeHost(source) {
  if (!source) return null;
  const s = String(source).toLowerCase();
  if (s.includes("amazon")) return "Amazon";
  if (s.includes("walmart")) return "Walmart";
  if (s.includes("ebay")) return "eBay";
  return null;
}

function parsePriceToNumber(priceStr) {
  if (!priceStr || typeof priceStr !== "string") return null;
  const n = parseFloat(priceStr.replace(/[^0-9.]/g, ""));
  return Number.isFinite(n) ? n : null;
}

/**
 * Map SerpAPI shopping_results / inline_shopping_results to our product shape.
 * Filters to Amazon, Walmart, eBay when possible.
 */
export function mapShoppingResults(raw) {
  const shopping = raw.shopping_results || [];
  const inline = raw.inline_shopping_results || [];
  const combined = [...shopping, ...inline];

  const byKey = new Map();

  for (const item of combined) {
    const source = item.source || item.store || item.seller || "";
    const platform = normalizeHost(source);
    if (!platform) continue;

    const title = item.title || item.name || "Product";
    const priceRaw = item.price || item.extracted_price || "";
    const priceNum = item.extracted_price
      ? Number(item.extracted_price)
      : parsePriceToNumber(priceRaw);

    const key =
      item.product_id ||
      item.product_link ||
      `${platform}:${title}`.slice(0, 200);

    const entry = {
      id: String(key).replace(/\s+/g, "_"),
      productKey: String(key),
      title,
      link: item.product_link || item.link || item.url || "#",
      image: item.thumbnail || item.image || "",
      price: priceRaw || (priceNum != null ? `$${priceNum.toFixed(2)}` : "—"),
      priceUsd: priceNum,
      platform,
      rating: item.rating ?? item.reviews?.rating ?? null,
      reviewsCount: item.reviews ?? item.review_count ?? null,
      offer: item.tag || item.extensions?.join?.(" · ") || null,
    };

    const existing = byKey.get(entry.productKey);
    if (!existing || (entry.priceUsd != null && existing.priceUsd == null)) {
      byKey.set(entry.productKey, entry);
    }
  }

  return [...byKey.values()];
}

export async function fetchGoogleShopping(apiKey, query) {
  const url = new URL("https://serpapi.com/search.json");
  url.searchParams.set("api_key", apiKey);
  url.searchParams.set("q", query);
  for (const [k, v] of Object.entries(US_SHOPPING_PARAMS)) {
    url.searchParams.set(k, v);
  }

  const res = await fetch(url.toString());
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`SerpAPI error ${res.status}: ${text.slice(0, 200)}`);
  }
  const data = await res.json();
  if (data.error) {
    throw new Error(data.error);
  }
  return data;
}

export function recommendationForListings(listings) {
  const prices = listings.map((l) => l.priceUsd).filter((p) => p != null && p > 0);
  if (prices.length === 0) return { verdict: "wait", reason: "Insufficient price data." };
  const min = Math.min(...prices);
  const avg = prices.reduce((a, b) => a + b, 0) / prices.length;
  if (min <= avg * 0.92) {
    return {
      verdict: "buy_now",
      reason: "Current low is noticeably below the average across tracked offers.",
    };
  }
  if (min >= avg * 1.05) {
    return {
      verdict: "wait",
      reason: "Prices are clustered high versus the sample average; waiting may help.",
    };
  }
  return {
    verdict: "wait",
    reason: "Prices are near average; no strong dip signal yet.",
  };
}
