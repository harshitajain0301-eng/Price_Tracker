import { Outlet, Link, useLocation } from "react-router-dom";
import { Bell, Moon, Sun, LineChart } from "lucide-react";
import { GlobalSearch } from "@/components/GlobalSearch";
import { Button } from "@/components/ui/button";
import { useSyncExtensionQuery } from "@/hooks/useSyncExtensionQuery";
import { useUiStore } from "@/store/ui";
import { useQuery } from "@tanstack/react-query";
import { getNotifications } from "@/lib/api";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

export function Layout() {
  useSyncExtensionQuery();
  const loc = useLocation();
  const dark = useUiStore((s) => s.dark);
  const toggleDark = useUiStore((s) => s.toggleDark);
  const { data: notes } = useQuery({
    queryKey: ["notifications"],
    queryFn: getNotifications,
    refetchInterval: 60_000,
  });
  const unread = notes?.filter((n) => !n.read).length ?? 0;

  return (
    <div className="min-h-screen bg-background">
      <header className="sticky top-0 z-40 border-b border-border/80 bg-card/80 backdrop-blur-md">
        <div className="mx-auto flex max-w-6xl flex-col gap-4 px-4 py-4 md:flex-row md:items-center md:gap-6">
          <Link to="/" className="flex shrink-0 items-center gap-2">
            <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary text-sm font-bold text-white">
              <LineChart className="h-5 w-5" />
            </div>
            <div className="font-display text-lg font-semibold tracking-tight">Smart Price</div>
          </Link>
          <GlobalSearch />
          <div className="flex shrink-0 items-center justify-end gap-2 md:ml-auto">
            <Button variant="ghost" size="icon" className="relative" asChild>
              <Link to="/notifications" aria-label="Notifications">
                <Bell className="h-5 w-5" />
                {unread > 0 && (
                  <span className="absolute right-1 top-1 flex h-4 min-w-[1rem] items-center justify-center rounded-full bg-[hsl(199,89%,48%)] px-1 text-[10px] font-semibold text-white">
                    {unread > 9 ? "9+" : unread}
                  </span>
                )}
              </Link>
            </Button>
            <Button variant="ghost" size="icon" onClick={toggleDark} aria-label="Toggle theme">
              {dark ? <Sun className="h-5 w-5" /> : <Moon className="h-5 w-5" />}
            </Button>
          </div>
        </div>
        <nav className="mx-auto flex max-w-6xl gap-1 border-t border-border/60 px-4 py-2 text-sm">
          {[
            ["/", "Dashboard"],
            ["/search", "Search"],
            ["/tracked", "Tracked"],
          ].map(([to, label]) => (
            <Link
              key={to}
              to={to}
              className={cn(
                "rounded-md px-3 py-1.5 font-medium transition-colors",
                loc.pathname === to ? "bg-muted text-foreground" : "text-muted-foreground hover:text-foreground"
              )}
            >
              {label}
            </Link>
          ))}
          <Badge className="ml-auto hidden sm:inline-flex">US market</Badge>
        </nav>
      </header>
      <main className="mx-auto max-w-6xl px-4 py-8">
        <Outlet />
      </main>
    </div>
  );
}
