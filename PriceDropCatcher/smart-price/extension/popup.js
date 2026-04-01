const DEFAULT_ORIGIN = "http://localhost:5173";

const input = document.getElementById("origin");
const save = document.getElementById("save");

chrome.storage.sync.get(["webAppOrigin"], (r) => {
  input.value = r.webAppOrigin || DEFAULT_ORIGIN;
});

save.addEventListener("click", () => {
  let v = input.value.trim() || DEFAULT_ORIGIN;
  v = v.replace(/\/$/, "");
  chrome.storage.sync.set({ webAppOrigin: v }, () => {
    save.textContent = "Saved";
    setTimeout(() => {
      save.textContent = "Save";
    }, 1200);
  });
});
