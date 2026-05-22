import { api } from "../core/api.js";
import { empty, escapeHtml, formatDate } from "../core/dom.js";
import { toast } from "../core/toast.js";

const list = document.querySelector("[data-notifications-list]");
document.querySelector("[data-load-notifications]")?.addEventListener("click", async (event) => {
  await runWithButton(event.currentTarget, "Обновляем...", loadNotifications);
});

loadNotifications();

async function loadNotifications() {
  list.innerHTML = empty("Загружаем уведомления...");
  try {
    const response = await api("/api/notifications");
    const items = response.items ?? [];
    list.innerHTML = items.length ? items.map(renderNotification).join("") : empty("Новых уведомлений нет.");
  } catch (error) {
    list.innerHTML = empty(error.message);
  }
}

function renderNotification(notification) {
  return `
    <article class="card">
      <div class="row">
        <h2>${escapeHtml(notification.title)}</h2>
        <span class="badge ${notification.isRead ? "" : "warn"}">${notification.isRead ? "прочитано" : "новое"}</span>
      </div>
      <p>${escapeHtml(notification.message)}</p>
      <div class="meta"><span>${formatDate(notification.createdAtUtc)}</span></div>
    </article>`;
}

async function runWithButton(button, pendingText, action) {
  const originalText = button.textContent;
  button.disabled = true;
  button.textContent = pendingText;
  try {
    await action();
  } catch (error) {
    toast(error.message, "error");
  } finally {
    button.disabled = false;
    button.textContent = originalText;
  }
}
