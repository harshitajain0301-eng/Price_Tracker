# Smart Price Intelligence & Purchase Assistant (US)

Monorepo layout:

- `client` — React 18, TypeScript, Tailwind, TanStack Query, Zustand  
- `server` — Express, MongoDB (Mongoose), SerpAPI proxy, alert polling  
- `extension` — Chrome MV3, “Track Product” on Google Search  

## Extension → web app (search bar)

The extension opens:

`{webAppOrigin}/?q=<encodeURIComponent(exact)>&auto=1`

where `exact` is either the Google URL `q` parameter or the search box value, **unchanged**. The app uses `URLSearchParams.get("q")` and shows that string in the global search bar as-is (no substitution with product titles or trimmed URLs). `auto=1` redirects to `/search` and runs SerpAPI immediately.

Configure the web app origin in the extension popup (default `http://localhost:5173`).

## Setup

1. **MongoDB** running locally (or set `MONGODB_URI` in `server/.env`).

2. **SerpAPI** — copy `server/.env.example` to `server/.env` and set `SERPAPI_KEY`.

3. Install and run (Node 18+):

```bash
cd server && npm install && npm run dev
cd client && npm install && npm run dev
```

4. **Chrome extension** — `chrome://extensions` → Developer mode → Load unpacked → select `extension/`.

The existing **WPF** `PriceDropCatcher` WebSocket bridge (`product.track` on port 22345) is **not modified**; this stack is a separate URL-based flow for the web app.
