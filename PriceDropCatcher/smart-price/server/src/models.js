import mongoose from "mongoose";

const searchHistorySchema = new mongoose.Schema(
  {
    deviceId: { type: String, required: true, index: true },
    query: { type: String, required: true },
    createdAt: { type: Date, default: Date.now },
  },
  { collection: "search_history" }
);

const viewedProductSchema = new mongoose.Schema(
  {
    deviceId: { type: String, required: true, index: true },
    productKey: { type: String, required: true },
    title: String,
    image: String,
    createdAt: { type: Date, default: Date.now },
  },
  { collection: "viewed_products" }
);

const priceAlertSchema = new mongoose.Schema(
  {
    deviceId: { type: String, required: true, index: true },
    productKey: { type: String, required: true },
    title: String,
    image: String,
    thresholdUsd: { type: Number, required: true },
    lastKnownPriceUsd: Number,
    active: { type: Boolean, default: true },
    createdAt: { type: Date, default: Date.now },
    updatedAt: { type: Date, default: Date.now },
  },
  { collection: "price_alerts" }
);

const notificationSchema = new mongoose.Schema(
  {
    deviceId: { type: String, required: true, index: true },
    type: { type: String, enum: ["price_drop", "deal", "info"], default: "price_drop" },
    title: String,
    body: String,
    read: { type: Boolean, default: false },
    meta: mongoose.Schema.Types.Mixed,
    createdAt: { type: Date, default: Date.now },
  },
  { collection: "notifications" }
);

export const SearchHistory = mongoose.model("SearchHistory", searchHistorySchema);
export const ViewedProduct = mongoose.model("ViewedProduct", viewedProductSchema);
export const PriceAlert = mongoose.model("PriceAlert", priceAlertSchema);
export const Notification = mongoose.model("Notification", notificationSchema);
