import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { getNotifications, markNotificationRead } from "@/lib/api";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";

export function NotificationsPage() {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ["notifications"], queryFn: getNotifications });

  const markRead = useMutation({
    mutationFn: markNotificationRead,
    onSuccess: () => qc.invalidateQueries({ queryKey: ["notifications"] }),
  });

  return (
    <div className="space-y-6">
      <h1 className="font-display text-2xl font-bold">Notifications</h1>
      <Card>
        <CardHeader>
          <CardTitle>In-app</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {q.isLoading && <Skeleton className="h-20 w-full" />}
          {q.data?.length === 0 && <p className="text-sm text-muted-foreground">No notifications yet.</p>}
          {q.data?.map((n) => (
            <div
              key={n._id}
              className={`flex flex-wrap items-start justify-between gap-3 rounded-xl border p-4 ${
                n.read ? "border-border/40 bg-muted/20" : "border-[hsl(199,89%,48%)]/30 bg-[hsl(199,89%,48%)]/5"
              }`}
            >
              <div>
                <p className="font-medium">{n.title}</p>
                <p className="text-sm text-muted-foreground">{n.body}</p>
                <p className="mt-1 text-xs text-muted-foreground">{new Date(n.createdAt).toLocaleString()}</p>
              </div>
              {!n.read && (
                <Button size="sm" variant="outline" onClick={() => markRead.mutate(n._id)}>
                  Mark read
                </Button>
              )}
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
