import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { getAlerts, deleteAlert } from "@/lib/api";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";

export function TrackedPage() {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ["alerts"], queryFn: getAlerts });
  const del = useMutation({
    mutationFn: deleteAlert,
    onSuccess: () => qc.invalidateQueries({ queryKey: ["alerts"] }),
  });

  return (
    <div className="space-y-6">
      <h1 className="font-display text-2xl font-bold">Tracked products</h1>
      <Card>
        <CardHeader>
          <CardTitle>Active alerts</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {q.isLoading && <Skeleton className="h-24 w-full" />}
          {q.data?.length === 0 && <p className="text-sm text-muted-foreground">No tracked products.</p>}
          {q.data?.map((a) => (
            <div
              key={a._id}
              className="flex flex-wrap items-center justify-between gap-4 rounded-xl border border-border/60 p-4"
            >
              <Link
                to={`/product/${encodeURIComponent(a.productKey)}`}
                className="flex min-w-0 flex-1 items-center gap-3"
              >
                {a.image ? (
                  <img src={a.image} alt="" className="h-14 w-14 rounded-lg object-cover" />
                ) : (
                  <div className="h-14 w-14 rounded-lg bg-muted" />
                )}
                <div className="min-w-0">
                  <p className="truncate font-medium">{a.title || a.productKey}</p>
                  <p className="text-sm text-muted-foreground">Target ≤ ${a.thresholdUsd}</p>
                </div>
              </Link>
              <Button variant="outline" size="sm" onClick={() => del.mutate(a._id)} disabled={del.isPending}>
                Remove
              </Button>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
