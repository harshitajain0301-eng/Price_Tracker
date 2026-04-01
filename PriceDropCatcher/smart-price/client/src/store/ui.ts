import { create } from "zustand";

type UiState = {
  searchBarText: string;
  setSearchBarText: (v: string) => void;
  dark: boolean;
  toggleDark: () => void;
};

export const useUiStore = create<UiState>((set) => ({
  searchBarText: "",
  setSearchBarText: (v) => set({ searchBarText: v }),
  dark: false,
  toggleDark: () =>
    set((s) => {
      const next = !s.dark;
      document.documentElement.classList.toggle("dark", next);
      try {
        localStorage.setItem("spi_theme", next ? "dark" : "light");
      } catch {
        /* ignore */
      }
      return { dark: next };
    }),
}));

export function hydrateTheme() {
  try {
    const t = localStorage.getItem("spi_theme");
    const dark = t === "dark";
    document.documentElement.classList.toggle("dark", dark);
    useUiStore.setState({ dark });
  } catch {
    /* ignore */
  }
}
