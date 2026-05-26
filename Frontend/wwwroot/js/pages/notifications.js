import { api } from "../core/api.js";
import { empty, escapeHtml, formatDate } from "../core/dom.js";
import { toast } from "../core/toast.js";

const list = document.querySelector("[data-notifications-list]");
const typeSelect = document.querySelector("[data-notification-type]");
const periodSelect = document.querySelector("[data-notification-period]");
const statusLabel = document.querySelector("[data-notification-status]");

const typeGroups = {
  messages: ["MessageReceived"],
  joinRequests: ["JoinRequestCreated", "JoinRequestApproved", "JoinRequestRejected"],
  suggestedPosts: ["SuggestedPostCreated", "SuggestedPostApproved", "SuggestedPostRejected"],
  moderation: ["ReportResolved", "PublicationDecision", "UserBlocked", "UserUnblocked", "CommunityBlocked", "CommunityUnblocked"],
  account: ["UserRegistered"]
};

const typeLabels = {
  MessageReceived: "сообщение",
  SuggestedPostCreated: "предложенный пост",
  SuggestedPostApproved: "предложенный пост",
  SuggestedPostRejected: "предложенный пост",
  ReportResolved: "модерация",
  PublicationDecision: "модерация",
  UserBlocked: "блокировка",
  UserUnblocked: "блокировка",
  CommunityBlocked: "модерация",
  CommunityUnblocked: "модерация",
  UserRegistered: "аккаунт",
  JoinRequestCreated: "заявка",
  JoinRequestApproved: "заявка",
  JoinRequestRejected: "заявка"
};

const periodLabels = {
  all: "все сохранённые",
  day: "за день",
  week: "за неделю",
  month: "за месяц",
  retention: "за 90 дней"
};

let notifications = [];

document.querySelector("[data-load-notifications]")?.addEventListener("click", async (event) => {
  await runWithButton(event.currentTarget, "Обновляем...", loadNotifications);
});

document.querySelector("[data-notification-filters]")?.addEventListener("submit", (event) => {
  event.preventDefault();
});

for (const control of [typeSelect, periodSelect]) {
  control?.addEventListener("change", renderNotifications);
}

await loadNotifications();

async function loadNotifications() {
  list.innerHTML = empty("Загружаем уведомления...");
  try {
    const response = await api("/api/notifications");
    notifications = response.items ?? [];
    renderNotifications();
  } catch (error) {
    list.innerHTML = empty(error.message);
    updateStatus("Не удалось загрузить");
  }
}

function renderNotifications() {
  const items = applyFilters(notifications);
  list.innerHTML = items.length ? items.map(renderNotification).join("") : empty("По выбранным фильтрам уведомлений нет.");
  updateStatus(`${items.length} из ${notifications.length} · ${periodLabels[periodSelect.value] ?? periodLabels.all}`);
}

function applyFilters(items) {
  const selectedType = typeSelect.value;
  const selectedPeriod = periodSelect.value;
  const since = periodStart(selectedPeriod);

  return items
    .filter((notification) => {
      if (selectedType === "all") return true;
      return (typeGroups[selectedType] ?? []).includes(notification.type);
    })
    .filter((notification) => {
      if (!since) return true;
      return new Date(notification.createdAtUtc) >= since;
    })
    .sort((left, right) => new Date(right.createdAtUtc) - new Date(left.createdAtUtc));
}

function periodStart(period) {
  const now = new Date();
  if (period === "day") return new Date(now.getTime() - 24 * 60 * 60 * 1000);
  if (period === "week") return new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
  if (period === "month") return new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
  if (period === "retention") return new Date(now.getTime() - 90 * 24 * 60 * 60 * 1000);
  return null;
}

function renderNotification(notification) {
  return `
    <article class="card">
      <div class="row">
        <h2>${escapeHtml(notification.title)}</h2>
        <span class="badge">${escapeHtml(typeLabels[notification.type] ?? "уведомление")}</span>
        <span class="badge ${notification.isRead ? "" : "warn"}">${notification.isRead ? "прочитано" : "новое"}</span>
      </div>
      <p>${escapeHtml(notification.message)}</p>
      <div class="meta"><span>${formatDate(notification.createdAtUtc)}</span></div>
    </article>`;
}

function updateStatus(text) {
  if (statusLabel) {
    statusLabel.textContent = text;
  }
}

async function runWithButton(button, pendingText, action) {
  const originalText = button.textContent;
  button.dataset.busy = "true";
  button.disabled = true;
  button.textContent = pendingText;
  try {
    await action();
  } catch (error) {
    toast(error.message, "error");
  } finally {
    button.disabled = false;
    delete button.dataset.busy;
    button.textContent = originalText;
  }
}
