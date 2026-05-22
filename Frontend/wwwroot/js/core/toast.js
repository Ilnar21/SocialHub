export function toast(message, type = "ok") {
  const root = document.querySelector("[data-toast-root]");
  if (!root) return;

  const item = document.createElement("div");
  item.className = `toast ${type === "error" ? "error" : ""}`;
  item.textContent = message;
  root.appendChild(item);
  setTimeout(() => item.remove(), 4200);
}
