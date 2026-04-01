import { useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams, Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { searchProducts, type Listing } from "@/lib/api";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useUiStore } from "@/store/ui";
import { cn } from "@/lib/utils";
import { AlertCircle, ChevronRight } from "lucide-react";

const PLATFORMS = ["Amazon", "Walmart", "eBay"] as const;

export function SearchPage() {
  const [params, setParams] = useSearchParams();
  const q = params.get("q") ?? "";
  const run = params.get("run") === "1";
  const setSearchBarText = useUiStore((s) => s.setSearchBarText);
  const searchLatched = useRef(false);
  if (run && q) searchLatched.current = true;

  const [minP, setMinP] = useState("");
  const [maxP, setMaxP] = useState("");
  const [platform, setPlatform] = useState<string>("all");
  const [minRating, setMinRating] = useState("");
  const [offersOnly, setOffersOnly] = useState(false);

  useEffect(() => {
    if (q) setSearchBarText(q);
  }, [q, setSearchBarText]);

  useEffect(() => {
    if (run && q) {
      const next = new URLSearchParams(params);
      next.delete("run");
      setParams(next, { replace: true });
    }
  }, [run, q, params, setParams]);

  const query = useQuery({
    queryKey: ["search", q],
    queryFn: () => searchProducts(q),
    enabled: q.length > 0 && searchLatched.current,
    retry: 1,
  });

  const filtered = useMemo(() => {
    let list = query.data?.listings ?? [];
    if (platform !== "all") {
      list = list.filter((l) => l.platform === platform);
    }
    const minN = parseFloat(minP);
    const maxN = parseFloat(maxP);
    if (Number.isFinite(minN)) {
      list = list.filter((l) => l.priceUsd != null && l.priceUsd >= minN);
    }
    if (Number.isFinite(maxN)) {
      list = list.filter((l) => l.priceUsd != null && l.priceUsd <= maxN);
    }
    const rMin = parseFloat(minRating);
    if (Number.isFinite(rMin)) {
      list = list.filter((l) => l.rating != null && l.rating >= rMin);
    }
    if (offersOnly) {
      list = list.filter((l) => l.offer && l.offer.length > 0);
    }
    return list;
  }, [query.data?.listings, platform, minP, maxP, minRating, offersOnly]);

  const rec = query.data?.recommendation;

  return (
    <div className="space-y-8">
      <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="font-display text-2xl font-bold">Search results</h1>
          {q && <p className="mt-1 text-sm text-muted-foreground">Query: {q}</p>}
        </div>
        {rec && (
          <div
            className={cn(
              "rounded-xl border px-4 py-3 text-sm",
              rec.verdict === "buy_now"
                ? "border-emerald-500/40 bg-emerald-500/10"
                : "border-amber-500/40 bg-amber-500/10"
            )}
          >
            <span className="font-semibold">{rec.verdict === "buy_now" ? "Buy now" : "Wait"}</span>
            <span className="text-muted-foreground"> — {rec.reason}</span>
          </div>
        )}
      </div>

      <Card>
        <CardContent className="space-y-4 p-5">
          <p className="text-sm font-medium">Filters</p>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <div>
              <label className="mb-1 block text-xs text-muted-foreground">Min price</label>
              <Input type="number" placeholder="0" value={minP} onChange={(e) => setMinP(e.target.value)} />
            </div>
            <div>
              <label className="mb-1 block text-xs text-muted-foreground">Max price</label>
              <Input type="number" placeholder="9999" value={maxP} onChange={(e) => setMaxP(e.target.value)} />
            </div>
            <div>
              <label className="mb-1 block text-xs text-muted-foreground">Platform</label>
              <select
                className="flex h-10 w-full rounded-md border border-border bg-card px-3 text-sm"
                value={platform}
                onChange={(e) => setPlatform(e.target.value)}
              >
                <option value="all">All (Amz / Wmt / eBay)</option>
                {PLATFORMS.map((p) => (
                  <option key={p} value={p}>
                    {p}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="mb-1 block text-xs text-muted-foreground">Min rating</label>
              <Input
                type="number"
                step="0.1"
                max={5}
                placeholder="e.g. 4"
                value={minRating}
                onChange={(e) => setMinRating(e.target.value)}
              />
            </div>
          </div>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={offersOnly} onChange={(e) => setOffersOnly(e.target.checked)} />
            Offers / discounts only
          </label>
        </CardContent>
      </Card>

      {!q && (
        <div className="rounded-xl border border-dashed border-border py-16 text-center text-muted-foreground">
          Enter a search in the bar above or open a product from the extension.
        </div>
      )}

      {q && !searchLatched.current && (
        <div className="flex flex-col items-center gap-3 rounded-xl border border-border py-12 text-center">
          <p className="text-sm text-muted-foreground">Ready to search for: {q}</p>
          <Button asChild variant="accent">
            <Link to={`/search?q=${encodeURIComponent(q)}&run=1`}>Load results</Link>
          </Button>
        </div>
      )}

      {q && searchLatched.current && query.isLoading && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-64 rounded-xl" />
          ))}
        </div>
      )}

      {query.isError && (
        <div className="flex items-start gap-3 rounded-xl border border-red-500/40 bg-red-500/10 p-4 text-sm">
          <AlertCircle className="mt-0.5 h-5 w-5 shrink-0 text-red-600" />
          <div>
            <p className="font-medium text-red-800 dark:text-red-200">Search unavailable</p>
            <p className="text-red-700/90 dark:text-red-300/90">{(query.error as Error).message}</p>
            <Button variant="outline" size="sm" className="mt-3" onClick={() => query.refetch()}>
              Retry
            </Button>
          </div>
        </div>
      )}

      {query.isSuccess && filtered.length === 0 && (
        <div className="rounded-xl border border-border py-12 text-center text-muted-foreground">
          No listings match filters. Try widening price or platform.
        </div>
      )}

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {filtered.map((item: Listing) => (
          <ResultCard key={item.id} item={item} />
        ))}
      </div>
    </div>
  );
}

function ResultCard({ item }: { item: Listing }) {
  return (
    <Link
      to={`/product/${encodeURIComponent(item.productKey)}`}
      state={{ listing: item }}
      className="group flex flex-col overflow-hidden rounded-xl border border-border bg-card shadow-sm transition hover:border-[hsl(199,89%,48%)]/50 hover:shadow-md"
    >
      <div className="aspect-square bg-muted/40 p-4">
        {item.image ? (
          <img src={item.image} alt="" className="h-full w-full object-contain" />
        ) : (
          <div className="flex h-full items-center justify-center text-xs text-muted-foreground">No image</div>
        )}
      </div>
      <div className="flex flex-1 flex-col gap-2 p-4">
        <Badge className="w-fit">{item.platform}</Badge>
        <h2 className="line-clamp-2 text-sm font-semibold leading-snug">{item.title}</h2>
        <p className="text-lg font-bold">{item.price}</p>
        <div className="mt-auto flex items-center justify-between text-xs text-muted-foreground">
          <span>{item.rating != null ? `★ ${item.rating}` : "—"}</span>
          {item.offer && <span className="truncate text-[hsl(199,89%,48%)]">{item.offer}</span>}
        </div>
        <span className="inline-flex items-center gap-1 text-xs font-medium text-[hsl(199,89%,48%)]">
          Compare prices <ChevronRight className="h-3 w-3" />
        </span>
      </div>
    </Link>
  );
}
