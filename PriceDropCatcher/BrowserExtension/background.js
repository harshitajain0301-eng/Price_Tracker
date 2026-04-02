/**
 * Opens a short-lived WebSocket to PriceDropCatcher (Fleck) and sends TRACK_PRODUCT.
 * Runs in the service worker so Origin is chrome-extension:// (allowed by the desktop app).
 */
function sendToDesktop(productUrl, productName) {
  return new Promise((resolve) => {
    if (!productUrl) {
      resolve({ ok: false, error: "No URL" });
      return;
    }
    let settled = false;
    const done = (ok, error) => {
      if (settled) return;
      settled = true;
      resolve({ ok, error });
    };

    let ws;
    try {
      ws = new WebSocket("ws://127.0.0.1:22345");
    } catch (e) {
      done(false, String(e));
      return;
    }

    const t = setTimeout(() => {
      try {
        ws.close();
      } catch (_) {}
      done(false, "Timeout — is PriceDropCatcher running?");
    }, 5000);

    ws.onopen = () => {
      try {
        ws.send(
          JSON.stringify({
            type: "TRACK_PRODUCT",
            payload: {
              productUrl: productUrl,
              productName: productName || "",
            },
          })
        );
        clearTimeout(t);
        setTimeout(() => {
          try {
            ws.close();
          } catch (_) {}
          done(true);
        }, 350);
      } catch (e) {
        clearTimeout(t);
        done(false, String(e));
      }
    };

    ws.onerror = () => {
      clearTimeout(t);
      done(false, "WebSocket error — open PriceDropCatcher first.");
    };
  });
}

chrome.runtime.onMessage.addListener((msg, _sender, sendResponse) => {
  if (msg?.type === "PC_TRACK" && msg.productUrl) {
    sendToDesktop(msg.productUrl, msg.productName).then(sendResponse);
    return true;
  }
  return false;
});

chrome.action.onClicked.addListener(async (tab) => {
  if (!tab?.id || !tab.url) return;
  const u = tab.url;
  if (!isSupportedProductUrl(u)) {
    return;
  }
  let productName = "";
  try {
    const [{ result } = {}] = await chrome.scripting.executeScript({
      target: { tabId: tab.id },
      func: () => {
        const t =
          document.querySelector("#productTitle")?.innerText?.trim() ||
          document.querySelector("h1[data-automation-id='product-title']")?.innerText?.trim() ||
          document.querySelector("h1[data-automation-id]")?.innerText?.trim() ||
          document.querySelector("#itemTitle")?.innerText?.trim() ||
          document.querySelector("h1")?.innerText?.trim() ||
          "";
        return t || document.title || "";
      },
    });
    productName = result || "";
  } catch (_) {
    productName = tab.title || "";
  }
  await sendToDesktop(u, productName);
});

function isSupportedProductUrl(url) {
  try {
    const u = new URL(url);
    const h = u.hostname.replace(/^www\./, "");
    const p = u.pathname;
    if (h.includes("amazon."))
      return /\/(dp|gp\/product|d)\//.test(p) || u.searchParams.has("asin");
    if (h === "walmart.com" || h.endsWith(".walmart.com")) return p.includes("/ip/");
    if (h.includes("ebay.")) return p.includes("/itm/") || p.includes("/p/");
  } catch (_) {}
  return false;
}
