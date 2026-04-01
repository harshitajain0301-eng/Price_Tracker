import { getDeviceId } from "./deviceId";

const base = "";

export type Listing = {
  id: string;
  productKey: string;
  title: string;
  link: string;
  image: string;
  price: string;
  priceUsd: number | null;
  platform: string;
  rating: number | null;
  reviewsCount: number | null;
  offer: string | null;
};

export type SearchResponse = {
  query: string;
  listings: Listing[];
  recommendation: { verdict: "buy_now" | "wait"; reason: string };
};

async function json<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${base}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers || {}),
    },
  });
  const data = await res.json().catch(() => ({}));
  if (!res.ok) {
    throw new Error((data as { message?: string }).message || (data as { error?: string }).error || res.statusText);
  }
  return data as T;
}

export function searchProducts(q: string) {
  return json<SearchResponse>("/api/search", {
    method: "POST",
    body: JSON.stringify({ q, deviceId: getDeviceId() }),
  });
}

export function compareProduct(q: string) {
  return json<{
    query: string;
    table: Listing[];
    allListings: Listing[];
    recommendation: { verdict: string; reason: string };
  }>("/api/compare", {
    method: "POST",
    body: JSON.stringify({ q, deviceId: getDeviceId() }),
  });
}

export function getSearchHistory() {
  return json<{ query: string; createdAt: string }[]>("/api/history/search", {
    method: "POST",
    body: JSON.stringify({ deviceId: getDeviceId(), limit: 12 }),
  });
}

export function getViewedHistory() {
  return json<{ productKey: string; title?: string; image?: string; createdAt: string }[]>(
    "/api/history/viewed",
    {
      method: "POST",
      body: JSON.stringify({ deviceId: getDeviceId(), limit: 12 }),
    }
  );
}

export function recordView(productKey: string, title?: string, image?: string) {
  return json<{ ok: boolean }>("/api/product/view", {
    method: "POST",
    body: JSON.stringify({
      deviceId: getDeviceId(),
      productKey,
      title,
      image,
    }),
  });
}

export function getAlerts() {
  return json<
    {
      _id: string;
      productKey: string;
      title?: string;
      image?: string;
      thresholdUsd: number;
      lastKnownPriceUsd?: number;
    }[]
  >(`/api/alerts/${getDeviceId()}`);
}

export function saveAlert(body: {
  productKey: string;
  title?: string;
  image?: string;
  thresholdUsd: number;
  lastKnownPriceUsd?: number | null;
}) {
  return json<unknown>("/api/alerts", {
    method: "POST",
    body: JSON.stringify({ ...body, deviceId: getDeviceId() }),
  });
}

export function deleteAlert(id: string) {
  return json<{ ok: boolean }>(`/api/alerts/${id}`, { method: "DELETE" });
}

export function getNotifications() {
  return json<
    { _id: string; title: string; body: string; read: boolean; type: string; createdAt: string }[]
  >(`/api/notifications/${getDeviceId()}`);
}

export function markNotificationRead(id: string) {
  return json<{ ok: boolean }>(`/api/notifications/${id}/read`, { method: "PATCH" });
}
