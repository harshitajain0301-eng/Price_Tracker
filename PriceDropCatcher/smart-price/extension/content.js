/**
 * Reads the current Google search text (URL `q` param or search box value) and passes it through
 * to the web app unchanged — that exact string is what appears in the app search bar.
 */
const DEFAULT_ORIGIN = "http://localhost:5173";

function getWebAppOrigin(callback) {
  chrome.storage.sync.get(["webAppOrigin"], (r) => {
    const o = (r.webAppOrigin || DEFAULT_ORIGIN).replace(/\/$/, "");
    callback(o);
  });
}

function getExactQueryText() {
  try {
    const fromUrl = new URLSearchParams(window.location.search).get("q");
    if (fromUrl) return fromUrl;
  } catch (_) {
    /* ignore */
  }
  const el =
    document.querySelector('textarea[name="q"]') || document.querySelector('input[name="q"]');
  if (el && typeof el.value === "string" && el.value.length > 0) return el.value;
  return "";
}

function injectTrackButton() {
  if (document.getElementById("spi-track-product-btn")) return;

  const btn = document.createElement("button");
  btn.id = "spi-track-product-btn";
  btn.type = "button";
  btn.textContent = "Track Product";
  btn.setAttribute("aria-label", "Track product in Smart Price Intelligence");
  Object.assign(btn.style, {
    marginLeft: "12px",
    padding: "8px 14px",
    borderRadius: "999px",
    border: "none",
    cursor: "pointer",
    fontSize: "13px",
    fontWeight: "600",
    fontFamily: "system-ui, sans-serif",
    background: "linear-gradient(135deg, #0f172a, #0369a1)",
    color: "#fff",
    boxShadow: "0 2px 8px rgba(15,23,42,0.25)",
  });

  btn.addEventListener("mouseenter", () => {
    btn.style.opacity = "0.92";
  });
  btn.addEventListener("mouseleave", () => {
    btn.style.opacity = "1";
  });

  btn.addEventListener("click", () => {
    const exact = getExactQueryText();
    getWebAppOrigin((origin) => {
      const url = `${origin}/?q=${encodeURIComponent(exact)}&auto=1`;
      chrome.runtime.sendMessage({ type: "OPEN_WEB_APP", url });
    });
  });

  const tryAttach = () => {
    const form = document.querySelector('form[action="/search"]');
    const bar = document.querySelector('div[role="navigation"]') || form?.parentElement;
    if (form && form.parentElement) {
      const wrap = form.parentElement;
      if (!wrap.querySelector("#spi-track-product-btn")) {
        wrap.appendChild(btn);
        return true;
      }
    }
    const searchDiv = document.querySelector(".RNNXgb")?.parentElement;
    if (searchDiv && !searchDiv.querySelector("#spi-track-product-btn")) {
      searchDiv.appendChild(btn);
      return true;
    }
    return false;
  };

  if (!tryAttach()) {
    const t = setInterval(() => {
      if (tryAttach()) clearInterval(t);
    }, 500);
    setTimeout(() => clearInterval(t), 15000);
  }
}

if (document.readyState === "loading") {
  document.addEventListener("DOMContentLoaded", injectTrackButton);
} else {
  injectTrackButton();
}
