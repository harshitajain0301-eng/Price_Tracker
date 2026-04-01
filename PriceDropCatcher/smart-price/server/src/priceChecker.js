import { PriceAlert, Notification } from "./models.js";
import { fetchGoogleShopping, mapShoppingResults } from "./serpapi.js";

export function startPriceChecker(serpApiKey) {
  if (!serpApiKey) {
    console.warn("[priceChecker] SERPAPI_KEY missing — alert checks disabled");
    return () => {};
  }

  const run = async () => {
    const alerts = await PriceAlert.find({ active: true }).lean();
    for (const a of alerts) {
      try {
        const q = a.title || a.productKey;
        const raw = await fetchGoogleShopping(serpApiKey, q);
        let listings = mapShoppingResults(raw);
        if (listings.length === 0) {
          listings = (raw.shopping_results || []).map((item) => ({
            title: item.title,
            priceUsd:
              item.extracted_price != null
                ? Number(item.extracted_price)
                : null,
            link: item.product_link,
          }));
        }
        const match = listings.find(
          (l) =>
            l.productKey === a.productKey ||
            (a.title && l.title && l.title.includes(a.title.slice(0, 40)))
        );
        const prices = listings
          .map((l) => l.priceUsd)
          .filter((p) => p != null && p > 0);
        const best = prices.length ? Math.min(...prices) : match?.priceUsd;

        if (best != null && best <= a.thresholdUsd) {
          await Notification.create({
            deviceId: a.deviceId,
            type: "price_drop",
            title: "Price target hit",
            body: `${a.title || "Product"} is at or below $${a.thresholdUsd} (seen ~$${best.toFixed(2)}).`,
            meta: { alertId: String(a._id), price: best },
          });
        } else if (
          a.lastKnownPriceUsd != null &&
          best != null &&
          best < a.lastKnownPriceUsd * 0.97
        ) {
          await Notification.create({
            deviceId: a.deviceId,
            type: "deal",
            title: "Price drop",
            body: `${a.title || "Tracked item"} dropped from ~$${a.lastKnownPriceUsd} to ~$${best.toFixed(2)}.`,
            meta: { alertId: String(a._id), price: best },
          });
        }

        if (best != null) {
          await PriceAlert.updateOne(
            { _id: a._id },
            { lastKnownPriceUsd: best, updatedAt: new Date() }
          );
        }
      } catch (e) {
        console.error("[priceChecker] alert", a._id, e.message);
      }
    }
  };

  const interval = setInterval(run, 15 * 60 * 1000);
  run();
  return () => clearInterval(interval);
}
