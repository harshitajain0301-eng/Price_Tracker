import { useEffect, useRef } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { useUiStore } from "@/store/ui";

/**
 * Syncs the search bar with `q` and, when `auto=1` or `ext=1`, forwards to `/search` with `run=1`.
 * Uses `URLSearchParams.get("q")` as-is so the extension’s product name or URL appears unchanged in the bar.
 */
export function useSyncExtensionQuery() {
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const setSearchBarText = useUiStore((s) => s.setSearchBarText);
  const lastHandledAuto = useRef<string | null>(null);

  useEffect(() => {
    const fromUrl = params.get("q");
    if (fromUrl === null) return;

    setSearchBarText(fromUrl);

    const auto = params.get("auto") === "1" || params.get("ext") === "1";
    if (!auto) return;

    const sig = `${fromUrl}\0auto`;
    if (lastHandledAuto.current === sig) return;
    lastHandledAuto.current = sig;

    const next = new URLSearchParams(params);
    next.delete("auto");
    next.delete("ext");
    setParams(next, { replace: true });
    navigate(`/search?q=${encodeURIComponent(fromUrl)}&run=1`, { replace: true });
  }, [params, setParams, navigate, setSearchBarText]);
}
