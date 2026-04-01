import { useEffect, useState } from "react";
import { useParams, Link, useLocation } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { compareProduct, recordView, saveAlert, type Listing } from "@/lib/api";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import { AlertCircle, ExternalLink } from "lucide-react";

export function ProductPage() {
  const { id } = useParams();
  const loc = useLocation();
  const listing = (loc.state as { listing?: Listing } | null)?.listing;
  let productKey = "";
  try {
    productKey = id ? decodeURIComponent(id) : "";
  } catch {
    productKey = id || "";
  }
  const searchQuery = listing?.title || productKey;

  const [threshold, setThreshold] = useState(
    listing?.priceUsd != null ? String(Math.floor(listing.priceUsd * 0.95)) : ""
  );

  const qc = useQueryClient();

  useEffect(() => {
    if (productKey) {
      recordView(productKey, listing?.title, listing?.image).catch(() => {});
    }
  }, [productKey, listing?.title, listing?.image]);

  const q = useQuery({
    queryKey: ["compare", searchQuery],
    queryFn: () => compareProduct(searchQuery),
    enabled: searchQuery.length > 0,
    retry: 1,
  });

  const alertMut = useMutation({
    mutationFn: () =>
      saveAlert({
        productKey,
        title: listing?.title || searchQuery,
        image: listing?.image,
        thresholdUsd: Number(threshold),
        lastKnownPriceUsd: listing?.priceUsd ?? null,
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["alerts"] }),
  });

  const table = q.data?.table ?? [];
  const rec = q.data?.recommendation;

  if (!productKey && !listing) {
    return (
      <p className="text-muted-foreground">
        Missing product. <Link to="/search" className="text-[hsl(199,89%,48%)]">Back to search</Link>
      </p>
    );
  }

  return (
    <div className="space-y-10">
      <div className="grid gap-8 lg:grid-cols-2">
        <div className="space-y-4">
          <div className="overflow-hidden rounded-2xl border border-border bg-muted/30 p-6">
            {listing?.image || q.data?.allListings?.[0]?.image ? (
              <img
                src={listing?.image || q.data?.allListings?.[0]?.image}
                alt=""
                className="mx-auto max-h-80 object-contain"
              />
            ) : q.isLoading ? (
              <Skeleton className="mx-auto h-80 max-w-full" />
            ) : (
              <div className="flex h-64 items-center justify-center text-muted-foreground">No image</div>
            )}
          </div>
        </div>
        <div className="space-y-4">
          <Badge>{listing?.platform || "Multi-store"}</Badge>
          <h1 className="font-display text-2xl font-bold leading-tight md:text-3xl">
            {listing?.title || searchQuery}
          </h1>
          <p className="text-sm text-muted-foreground">
            {listing?.rating != null ? `Rating ${listing.rating}` : "Rating varies by seller"} · US offers via Google
            Shopping
          </p>
          {rec && (
            <div
              className={`rounded-xl border px-4 py-3 text-sm ${
                rec.verdict === "buy_now"
                  ? "border-emerald-500/40 bg-emerald-500/10"
                  : "border-amber-500/40 bg-amber-500/10"
              }`}
            >
              <span className="font-semibold">{rec.verdict === "buy_now" ? "Buy now" : "Wait"}</span>
              <span className="text-muted-foreground"> — {rec.reason}</span>
            </div>
          )}
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Price alert</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <p className="text-xs text-muted-foreground">Notify in-app when the best seen price is at or below your threshold.</p>
              <div className="flex flex-wrap gap-2">
                <Input
                  type="number"
                  placeholder="Target price USD"
                  value={threshold}
                  onChange={(e) => setThreshold(e.target.value)}
                  className="max-w-[200px]"
                />
                <Button
                  variant="accent"
                  disabled={!threshold || alertMut.isPending}
                  onClick={() => alertMut.mutate()}
                >
                  Track product
                </Button>
              </div>
              {alertMut.isError && <p className="text-xs text-red-600">Could not save alert.</p>}
              {alertMut.isSuccess && <p className="text-xs text-emerald-600">Alert saved.</p>}
            </CardContent>
          </Card>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Price comparison</CardTitle>
        </CardHeader>
        <CardContent>
          {q.isLoading && (
            <div className="space-y-2">
              <Skeleton className="h-12 w-full" />
              <Skeleton className="h-12 w-full" />
            </div>
          )}
          {q.isError && (
            <div className="flex gap-2 rounded-lg border border-red-500/30 bg-red-500/10 p-4 text-sm">
              <AlertCircle className="h-5 w-5 shrink-0" />
              <span>{(q.error as Error).message}</span>
            </div>
          )}
          {q.isSuccess && table.length === 0 && (
            <p className="text-sm text-muted-foreground">No Amazon / Walmart / eBay rows for this query.</p>
          )}
          {table.length > 0 && (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <thead>
                  <tr className="border-b border-border text-muted-foreground">
                    <th className="pb-3 pr-4 font-medium">Platform</th>
                    <th className="pb-3 pr-4 font-medium">Price</th>
                    <th className="pb-3 pr-4 font-medium">Offers</th>
                    <th className="pb-3 pr-4 font-medium">Rating</th>
                    <th className="pb-3 font-medium">Buy</th>
                  </tr>
                </thead>
                <tbody>
                  {table.map((row) => (
                    <tr key={row.id} className="border-b border-border/60">
                      <td className="py-3 pr-4 font-medium">{row.platform}</td>
                      <td className="py-3 pr-4">{row.price}</td>
                      <td className="py-3 pr-4 text-muted-foreground">{row.offer || "—"}</td>
                      <td className="py-3 pr-4">{row.rating != null ? row.rating : "—"}</td>
                      <td className="py-3">
                        <a
                          href={row.link}
                          target="_blank"
                          rel="noreferrer"
                          className="inline-flex items-center gap-1 font-medium text-[hsl(199,89%,48%)] hover:underline"
                        >
                          Open <ExternalLink className="h-3 w-3" />
                        </a>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      <Link to="/search" className="text-sm text-muted-foreground hover:text-foreground">
        ← Back to search
      </Link>
    </div>
  );
}
