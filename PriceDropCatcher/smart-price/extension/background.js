chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message?.type === "OPEN_WEB_APP" && typeof message.url === "string") {
    chrome.tabs.create({ url: message.url });
    sendResponse({ ok: true });
    return true;
  }
  return false;
});
