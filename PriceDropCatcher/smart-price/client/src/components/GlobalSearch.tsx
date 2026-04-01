import { FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { Search } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { useUiStore } from "@/store/ui";

export function GlobalSearch() {
  const navigate = useNavigate();
  const searchBarText = useUiStore((s) => s.searchBarText);
  const setSearchBarText = useUiStore((s) => s.setSearchBarText);

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    const q = searchBarText;
    if (!q.trim()) return;
    navigate(`/search?q=${encodeURIComponent(q)}&run=1`);
  };

  return (
    <form onSubmit={onSubmit} className="relative flex w-full max-w-2xl flex-1 gap-2">
      <div className="relative flex-1">
        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground/70" />
        <Input
          value={searchBarText}
          onChange={(e) => setSearchBarText(e.target.value)}
          placeholder="Search products (US) — Amazon, Walmart, eBay…"
          className="h-11 pl-10 font-normal shadow-sm"
          name="q"
          autoComplete="off"
        />
      </div>
      <Button type="submit" size="lg" className="shrink-0" variant="accent">
        Search
      </Button>
    </form>
  );
}
