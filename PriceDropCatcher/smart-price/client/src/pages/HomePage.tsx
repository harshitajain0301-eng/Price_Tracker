import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import { getSearchHistory, getViewedHistory, getAlerts, getNotifications } from "@/lib/api";
import { TrendingDown, History, Target, Sparkles } from "lucide-react";

export function HomePage() {
  const qh = useQuery({ queryKey: ["hist", "search"], queryFn: getSearchHistory });
  const qv = useQuery({ queryKey: ["hist", "viewed"], queryFn: getViewedHistory });
  const qa = useQuery({ queryKey: ["alerts"], queryFn: getAlerts });
  const qn = useQuery({ queryKey: ["notifications"], queryFn: getNotifications });

  const recentRec =
    qh.data?.[0] && qv.data?.[0]
      ? { verdict: "wait" as const, reason: "Search again for a fresh Buy / Wait signal." }
      : null;

  return (
    <div className="space-y-10">
      <div>
        <h1 className="font-display text-3xl font-bold tracking-tight md:text-4xl">Price intelligence</h1>
        <p className="mt-2 max-w-2xl text-muted-foreground">
          Compare Amazon, Walmart, and eBay in one place. Track thresholds and get in-app alerts when prices move.
        </p>
      </div>

      <div className="grid gap-6 md:grid-cols-2">
        <Card>
          <CardHeader className="flex flex-row items-center gap-2">
            <History className="h-5 w-5 text-[hsl(199,89%,48%)]" />
            <CardTitle>Recently searched</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {qh.isLoading && (
              <>
                <Skeleton className="h-9 w-full" />
                <Skeleton className="h-9 w-full" />
              </>
            )}
            {qh.isError && <p className="text-sm text-red-600 dark:text-red-400">Could not load history.</p>}
            {qh.data?.length === 0 && <p className="text-sm text-muted-foreground">No searches yet.</p>}
            {qh.data?.map((h) => (
              <Link
                key={h.createdAt + h.query}
                to={`/search?q=${encodeURIComponent(h.query)}&run=1`}
                className="block truncate rounded-lg border border-border/60 bg-muted/30 px-3 py-2 text-sm hover:bg-muted/60"
              >
                {h.query}
              </Link>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center gap-2">
            <Sparkles className="h-5 w-5 text-amber-500" />
            <CardTitle>Recommendations</CardTitle>
          </CardHeader>
          <CardContent>
            {recentRec ? (
              <div className="rounded-lg border border-dashed border-border bg-muted/20 p-4">
                <Badge className="mb-2">Insight</Badge>
                <p className="text-sm text-muted-foreground">{recentRec.reason}</p>
                <Link to="/search" className="mt-3 inline-block text-sm font-medium text-[hsl(199,89%,48%)]">
                  Run a search →
                </Link>
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">Search for a product to see Buy now / Wait guidance on results.</p>
            )}
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6 md:grid-cols-2">
        <Card>
          <CardHeader className="flex flex-row items-center gap-2">
            <Target className="h-5 w-5 text-emerald-500" />
            <CardTitle>Tracked products</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {qa.isLoading && <Skeleton className="h-20 w-full" />}
            {qa.data?.length === 0 && <p className="text-sm text-muted-foreground">No active alerts. Add one from a product page.</p>}
            {qa.data?.map((a) => (
              <Link
                key={a._id}
                to={`/product/${encodeURIComponent(a.productKey)}`}
                className="flex items-center gap-3 rounded-lg border border-border/60 p-3 hover:bg-muted/40"
              >
                {a.image ? (
                  <img src={a.image} alt="" className="h-12 w-12 rounded-md object-cover" />
                ) : (
                  <div className="h-12 w-12 rounded-md bg-muted" />
                )}
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium">{a.title || a.productKey}</p>
                  <p className="text-xs text-muted-foreground">Alert ≤ ${a.thresholdUsd}</p>
                </div>
              </Link>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center gap-2">
            <TrendingDown className="h-5 w-5 text-[hsl(199,89%,48%)]" />
            <CardTitle>Price drop alerts</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {qn.isLoading && <Skeleton className="h-16 w-full" />}
            {qn.data?.filter((n) => n.type === "price_drop" || n.type === "deal").length === 0 && (
              <p className="text-sm text-muted-foreground">You’re all caught up.</p>
            )}
            {qn.data
              ?.filter((n) => n.type === "price_drop" || n.type === "deal")
              .slice(0, 5)
              .map((n) => (
                <div key={n._id} className="rounded-lg border border-border/60 bg-muted/20 p-3 text-sm">
                  <p className="font-medium">{n.title}</p>
                  <p className="text-muted-foreground">{n.body}</p>
                </div>
              ))}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Recently viewed</CardTitle>
        </CardHeader>
        <CardContent>
          {qv.isLoading && <Skeleton className="h-24 w-full" />}
          <div className="flex flex-wrap gap-3">
            {qv.data?.map((v) => (
              <Link
                key={v.productKey + v.createdAt}
                to={`/product/${encodeURIComponent(v.productKey)}`}
                className="flex max-w-[200px] flex-col gap-2 rounded-lg border border-border/60 p-2 hover:bg-muted/40"
              >
                {v.image ? (
                  <img src={v.image} alt="" className="h-24 w-full rounded-md object-cover" />
                ) : (
                  <div className="h-24 w-full rounded-md bg-muted" />
                )}
                <span className="line-clamp-2 text-xs">{v.title || v.productKey}</span>
              </Link>
            ))}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
