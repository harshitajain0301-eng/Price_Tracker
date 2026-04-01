import express from "express";
import {
  SearchHistory,
  ViewedProduct,
  PriceAlert,
  Notification,
} from "./models.js";
import {
  fetchGoogleShopping,
  mapShoppingResults,
  recommendationForListings,
} from "./serpapi.js";

export function createRouter({ serpApiKey }) {
  const r = express.Router();

  r.post("/search", async (req, res) => {
    try {
      const { q, deviceId } = req.body || {};
      if (!q || typeof q !== "string") {
        return res.status(400).json({ error: "Missing query `q`" });
      }
      if (!serpApiKey) {
        return res.status(503).json({
          error: "SerpAPI not configured",
          message: "Set SERPAPI_KEY in server environment.",
        });
      }

      const raw = await fetchGoogleShopping(serpApiKey, q.trim());
      let listings = mapShoppingResults(raw);

      if (listings.length === 0) {
        const fallback = (raw.shopping_results || []).map((item) => ({
          id: String(item.product_id || item.link || Math.random()),
          productKey: String(item.product_id || item.link || item.title),
          title: item.title || "Product",
          link: item.product_link || item.link || "#",
          image: item.thumbnail || "",
          price: item.price || "—",
          priceUsd:
            item.extracted_price != null
              ? Number(item.extracted_price)
              : null,
          platform: item.source || "Store",
          rating: item.rating ?? null,
          reviewsCount: item.reviews ?? null,
          offer: item.tag || null,
        }));
        listings = fallback;
      }

      const recommendation = recommendationForListings(listings);

      if (deviceId) {
        await SearchHistory.create({ deviceId, query: q }).catch(() => {});
      }

      res.json({
        query: q,
        listings,
        recommendation,
        serpMeta: { search_information: raw.search_information },
      });
    } catch (e) {
      console.error(e);
      res.status(502).json({
        error: "Search failed",
        message: e.message || String(e),
      });
    }
  });

  r.post("/history/search", async (req, res) => {
    const { deviceId, limit = 20 } = req.body || {};
    if (!deviceId) return res.status(400).json({ error: "deviceId required" });
    const rows = await SearchHistory.find({ deviceId })
      .sort({ createdAt: -1 })
      .limit(Number(limit))
      .lean();
    res.json(rows);
  });

  r.post("/history/viewed", async (req, res) => {
    const { deviceId, limit = 20 } = req.body || {};
    if (!deviceId) return res.status(400).json({ error: "deviceId required" });
    const rows = await ViewedProduct.find({ deviceId })
      .sort({ createdAt: -1 })
      .limit(Number(limit))
      .lean();
    res.json(rows);
  });

  r.post("/product/view", async (req, res) => {
    const { deviceId, productKey, title, image } = req.body || {};
    if (!deviceId || !productKey) {
      return res.status(400).json({ error: "deviceId and productKey required" });
    }
    await ViewedProduct.findOneAndUpdate(
      { deviceId, productKey },
      { title, image, createdAt: new Date() },
      { upsert: true, new: true }
    );
    res.json({ ok: true });
  });

  r.post("/compare", async (req, res) => {
    try {
      const { q, deviceId } = req.body || {};
      if (!q || typeof q !== "string") {
        return res.status(400).json({ error: "Missing query `q`" });
      }
      if (!serpApiKey) {
        return res.status(503).json({ error: "SerpAPI not configured" });
      }
      const raw = await fetchGoogleShopping(serpApiKey, q.trim());
      let listings = mapShoppingResults(raw);
      if (listings.length === 0) {
        listings = (raw.shopping_results || []).slice(0, 12).map((item) => ({
          id: String(item.product_id || item.link),
          productKey: String(item.product_id || item.link || item.title),
          title: item.title,
          link: item.product_link || item.link || "#",
          image: item.thumbnail || "",
          price: item.price || "—",
          priceUsd:
            item.extracted_price != null
              ? Number(item.extracted_price)
              : null,
          platform: item.source || "Store",
          rating: item.rating ?? null,
          reviewsCount: item.reviews ?? null,
          offer: item.tag || null,
        }));
      }
      const byPlatform = {};
      for (const L of listings) {
        const p = L.platform;
        if (!byPlatform[p] || (L.priceUsd && byPlatform[p].priceUsd > L.priceUsd)) {
          byPlatform[p] = L;
        }
      }
      const table = Object.values(byPlatform);
      const recommendation = recommendationForListings(listings);
      if (deviceId) {
        await ViewedProduct.findOneAndUpdate(
          { deviceId, productKey: q },
          { title: q, createdAt: new Date() },
          { upsert: true }
        ).catch(() => {});
      }
      res.json({ query: q, table, allListings: listings, recommendation });
    } catch (e) {
      console.error(e);
      res.status(502).json({ error: e.message || String(e) });
    }
  });

  r.get("/alerts/:deviceId", async (req, res) => {
    const alerts = await PriceAlert.find({
      deviceId: req.params.deviceId,
      active: true,
    })
      .sort({ updatedAt: -1 })
      .lean();
    res.json(alerts);
  });

  r.post("/alerts", async (req, res) => {
    const {
      deviceId,
      productKey,
      title,
      image,
      thresholdUsd,
      lastKnownPriceUsd,
    } = req.body || {};
    if (!deviceId || !productKey || thresholdUsd == null) {
      return res.status(400).json({ error: "deviceId, productKey, thresholdUsd required" });
    }
    const doc = await PriceAlert.findOneAndUpdate(
      { deviceId, productKey },
      {
        title,
        image,
        thresholdUsd: Number(thresholdUsd),
        lastKnownPriceUsd:
          lastKnownPriceUsd != null ? Number(lastKnownPriceUsd) : undefined,
        active: true,
        updatedAt: new Date(),
      },
      { upsert: true, new: true }
    );
    res.json(doc);
  });

  r.delete("/alerts/:id", async (req, res) => {
    await PriceAlert.findByIdAndUpdate(req.params.id, { active: false });
    res.json({ ok: true });
  });

  r.get("/notifications/:deviceId", async (req, res) => {
    const items = await Notification.find({ deviceId: req.params.deviceId })
      .sort({ createdAt: -1 })
      .limit(50)
      .lean();
    res.json(items);
  });

  r.patch("/notifications/:id/read", async (req, res) => {
    await Notification.findByIdAndUpdate(req.params.id, { read: true });
    res.json({ ok: true });
  });

  return r;
}
