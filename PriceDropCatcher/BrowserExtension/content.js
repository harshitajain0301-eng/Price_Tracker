/**
 * Floating "Send to PriceDropCatcher" on supported product pages (backup to toolbar action).
 */
(function () {
  if (window.__pcBridgeInjected) return;
  window.__pcBridgeInjected = true;

  function getProductName() {
    return (
      document.querySelector("#productTitle")?.innerText?.trim() ||
      document.querySelector("h1[data-automation-id='product-title']")?.innerText?.trim() ||
      document.querySelector("h1[data-automation-id]")?.innerText?.trim() ||
      document.querySelector("#itemTitle")?.innerText?.trim() ||
      document.querySelector("h1")?.innerText?.trim() ||
      document.title ||
      ""
    );
  }

  function inject() {
    if (document.getElementById("pc-bridge-btn")) return;

    const btn = document.createElement("button");
    btn.id = "pc-bridge-btn";
    btn.type = "button";
    btn.textContent = "Send to PriceDropCatcher";
    btn.title = "Send this page URL to the desktop app";
    Object.assign(btn.style, {
      position: "fixed",
      bottom: "24px",
      right: "24px",
      zIndex: "2147483646",
      padding: "10px 16px",
      borderRadius: "10px",
      border: "none",
      cursor: "pointer",
      fontSize: "13px",
      fontWeight: "600",
      fontFamily: "system-ui, sans-serif",
      background: "linear-gradient(135deg, #4f46e5, #6366f1)",
      color: "#fff",
      boxShadow: "0 4px 14px rgba(79,70,229,0.45)",
    });

    btn.addEventListener("click", () => {
      btn.disabled = true;
      btn.textContent = "Sending…";
      chrome.runtime.sendMessage(
        {
          type: "PC_TRACK",
          productUrl: location.href,
          productName: getProductName(),
        },
        (res) => {
          btn.disabled = false;
          btn.textContent = res?.ok ? "Sent ✓" : "Failed — open desktop app";
          setTimeout(() => {
            btn.textContent = "Send to PriceDropCatcher";
          }, 2500);
        }
      );
    });

    document.documentElement.appendChild(btn);
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", inject);
  } else {
    inject();
  }
})();
