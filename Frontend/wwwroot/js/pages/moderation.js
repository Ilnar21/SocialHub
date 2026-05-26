import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formData, formatDate, shortId } from "../core/dom.js";
import { getSession, isPlatformModerator } from "../core/session.js";
import { toast } from "../core/toast.js";

const blockUserForm = document.querySelector('[data-form="block-user"]');
const userSearchInput = document.querySelector("[data-user-search]");
const userSearchResult = document.querySelector("[data-user-search-result]");
const blockedUsersList = document.querySelector("[data-blocked-users-list]");
const reportsList = document.querySelector("[data-reports-list]");
const auditList = document.querySelector("[data-audit-list]");

let selectedUser = null;

if (!isPlatformModerator()) {
  document.querySelector(".page-header p").textContent = "Этот раздел доступен только модераторам платформы.";
  document.querySelector(".split").innerHTML = `
    <section class="panel empty">
      <h2>Доступ запрещен</h2>
      <p>Для просмотра жалоб, блокировок и аудита нужна роль модератора платформы.</p>
      <a class="button secondary" href="/Feed">Вернуться в ленту</a>
    </section>`;
} else {
  document.querySelector("[data-load-moderation]")?.addEventListener("click", async (event) => {
    await runWithButton(event.currentTarget, "Обновляем...", loadModeration);
  });

  document.querySelector("[data-find-user]")?.addEventListener("click", async (event) => {
    await runWithButton(event.currentTarget, "Ищем...", findUserByUsername);
  });

  userSearchInput?.addEventListener("input", () => {
    selectedUser = null;
    userSearchResult.innerHTML = "";
  });

  blockUserForm?.addEventListener("submit", blockUser);

  await loadModeration();
}

async function loadModeration() {
  await Promise.all([loadReports(), loadAudit(), loadBlockedUsers()]);
}

async function findUserByUsername() {
  const username = normalizeUsername(userSearchInput.value);
  if (!username) {
    toast("Введите username пользователя", "error");
    return null;
  }

  try {
    selectedUser = await api(`/api/users/by-username/${encodeURIComponent(username)}`);
    userSearchResult.innerHTML = renderUserSearchResult(selectedUser);
    bindUserActionButtons(userSearchResult);
    return selectedUser;
  } catch (error) {
    selectedUser = null;
    userSearchResult.innerHTML = empty(error.message);
    return null;
  }
}

async function blockUser(event) {
  event.preventDefault();

  const form = event.currentTarget;
  const button = form.querySelector('button[type="submit"]');
  const data = formData(form);
  const currentUserId = getSession().user?.id;
  const searchedUsername = normalizeUsername(data.username);

  if (!searchedUsername) {
    toast("Введите username пользователя", "error");
    return;
  }

  if (!selectedUser || !selectedUser.username || selectedUser.username.toLowerCase() !== searchedUsername.toLowerCase()) {
    selectedUser = await findUserByUsername();
  }

  if (!selectedUser) return;

  if (selectedUser.id === currentUserId) {
    toast("Нельзя заблокировать собственный аккаунт модератора", "error");
    return;
  }

  if (isPlatformModeratorRole(selectedUser.role)) {
    toast("Нельзя заблокировать другого модератора платформы", "error");
    return;
  }

  if (isBlocked(selectedUser)) {
    toast("Пользователь уже заблокирован. Его можно досрочно разблокировать.", "error");
    return;
  }

  await runWithButton(button, "Блокируем...", async () => {
    await api(`/api/users/${selectedUser.id}/blocks`, toJson("POST", {
      durationDays: Number(data.durationDays),
      reason: data.reason
    }));
    toast("Пользователь заблокирован");
    selectedUser = null;
    form.reset();
    userSearchResult.innerHTML = "";
    await Promise.all([loadBlockedUsers(), loadAudit()]);
  });
}

async function loadBlockedUsers() {
  blockedUsersList.innerHTML = empty("Загружаем блокировки...");
  try {
    const currentUserId = getSession().user?.id;
    const users = await api("/api/users/");
    const blockedUsers = users.filter((user) => user.id !== currentUserId && isBlocked(user));
    blockedUsersList.innerHTML = blockedUsers.length
      ? blockedUsers.map(renderBlockedUser).join("")
      : empty("Сейчас нет заблокированных пользователей.");
    bindUserActionButtons(blockedUsersList);
  } catch (error) {
    blockedUsersList.innerHTML = empty(error.message);
  }
}

async function unblockUser(button) {
  await runWithButton(button, "Разблокируем...", async () => {
    await api(`/api/users/${button.dataset.unblockUser}/blocks`, { method: "DELETE" });
    toast("Пользователь разблокирован");
    selectedUser = null;
    userSearchResult.innerHTML = "";
    await Promise.all([loadBlockedUsers(), loadAudit()]);
  });
}

function renderUserSearchResult(user) {
  const blocked = isBlocked(user);
  const isModerator = isPlatformModeratorRole(user.role);
  const isCurrentUser = user.id === getSession().user?.id;
  const warning = isModerator
    ? `<p class="muted">Модератора платформы нельзя заблокировать через панель модерации.</p>`
    : isCurrentUser
      ? `<p class="muted">Нельзя заблокировать собственный аккаунт.</p>`
      : "";

  return `
    <article class="card">
      <div class="row">
        <h2>${escapeHtml(displayName(user))}</h2>
        <span class="badge ${blocked ? "danger" : "success"}">${blocked ? "Заблокирован" : "Активен"}</span>
      </div>
      <p>${escapeHtml(user.blockReason ?? user.profile?.bio ?? "")}</p>
      <div class="meta">
        <span>@${escapeHtml(user.username)}</span>
        <span>${translateRole(user.role)}</span>
        ${user.blockedUntil ? `<span>До ${formatDate(user.blockedUntil)}</span>` : ""}
      </div>
      ${warning}
      ${blocked ? `
        <div class="actions">
          <button class="button secondary" type="button" data-unblock-user="${user.id}">Разблокировать досрочно</button>
        </div>` : ""}
    </article>`;
}

function renderBlockedUser(user) {
  return `
    <article class="card">
      <div class="row">
        <h2>${escapeHtml(displayName(user))}</h2>
        <span class="badge danger">Заблокирован</span>
      </div>
      <p>${escapeHtml(user.blockReason ?? "")}</p>
      <div class="meta">
        <span>@${escapeHtml(user.username)}</span>
        <span>${translateRole(user.role)}</span>
        ${user.blockedUntil ? `<span>До ${formatDate(user.blockedUntil)}</span>` : ""}
      </div>
      <div class="actions">
        <button class="button secondary" type="button" data-unblock-user="${user.id}">Разблокировать досрочно</button>
      </div>
    </article>`;
}

function bindUserActionButtons(root) {
  for (const button of root.querySelectorAll("[data-unblock-user]")) {
    if (button.dataset.bound === "true") continue;
    button.dataset.bound = "true";
    button.addEventListener("click", () => unblockUser(button));
  }
}

function isBlocked(user) {
  return user.status === "Blocked" || user.status === "BLOCKED";
}

async function loadReports() {
  reportsList.innerHTML = empty("Загружаем жалобы...");
  try {
    const reports = await api("/api/reports?status=NEW");
    reportsList.innerHTML = reports.length ? reports.map(renderReport).join("") : empty("Новых жалоб нет.");
    for (const button of reportsList.querySelectorAll("[data-delete-reported-post]")) {
      button.addEventListener("click", () => deleteReportedPost(button));
    }
    for (const button of reportsList.querySelectorAll("[data-resolve-report]")) {
      button.addEventListener("click", () => resolveReport(button));
    }
  } catch (error) {
    reportsList.innerHTML = empty(error.message);
  }
}

async function loadAudit() {
  auditList.innerHTML = empty("Загружаем аудит...");
  try {
    const entries = await api("/api/audit");
    auditList.innerHTML = entries.length ? entries.map(renderAudit).join("") : empty("Записей аудита пока нет.");
  } catch (error) {
    auditList.innerHTML = empty(error.message);
  }
}

async function deleteReportedPost(button) {
  await runWithButton(button, "Удаляем...", async () => {
    await api(`/api/reports/${button.dataset.deleteReportedPost}/resolve/delete-post`, toJson("POST", {
      comment: "Удалено через панель модерации"
    }));
    toast("Пост удален, жалоба обработана");
    await loadModeration();
  });
}

async function resolveReport(button) {
  const targetType = button.dataset.reportTargetType || "REPORT";
  const comment = targetType === "COMMUNITY"
    ? "Жалоба на сообщество рассмотрена модератором"
    : "Жалоба рассмотрена модератором";

  await runWithButton(button, "Закрываем...", async () => {
    await api(`/api/reports/${button.dataset.resolveReport}/resolve`, toJson("POST", { comment }));
    toast("Жалоба закрыта");
    await loadModeration();
  });
}

function renderReport(report) {
  const targetType = normalizeTargetType(report.targetType);
  const actions = targetType === "POST"
    ? `
      <button class="button danger" data-delete-reported-post="${report.id}">Удалить пост</button>
      <button class="button secondary" data-resolve-report="${report.id}" data-report-target-type="${targetType}">Закрыть без удаления</button>`
    : `<button class="button secondary" data-resolve-report="${report.id}" data-report-target-type="${targetType}">Закрыть жалобу</button>`;

  return `
    <article class="card">
      <div class="row">
        <h2>${escapeHtml(report.reason)}</h2>
        <span class="badge warn">${translateReportStatus(report.status)}</span>
      </div>
      <p>${escapeHtml(report.comment ?? "")}</p>
      <div class="meta">
        <span>${translateTargetType(targetType)} ${shortId(report.targetId)}</span>
        <span>${formatDate(report.createdAtUtc)}</span>
      </div>
      <div class="actions">
        ${actions}
      </div>
    </article>`;
}

function renderAudit(entry) {
  return `
    <article class="card">
      <div class="row">
        <h2>${translateAuditAction(entry.action)}</h2>
        <span class="badge">${translateRole(entry.actorRole ?? "PlatformModerator")}</span>
      </div>
      <p>${escapeHtml(entry.reason ?? "")}</p>
      <div class="meta">
        <span>${translateTargetType(entry.targetType)} ${shortId(entry.targetId)}</span>
        <span>${formatDate(entry.createdAtUtc)}</span>
      </div>
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

function normalizeUsername(value) {
  return String(value || "").trim().replace(/^@+/, "");
}

function displayName(user) {
  return user.profile?.displayName || user.username;
}

function normalizeTargetType(value) {
  return String(value || "").trim().toUpperCase();
}

function isPlatformModeratorRole(role) {
  const value = String(role || "").toLowerCase();
  return value === "platformmoderator" || value === "platform_moderator" || value === "moderator";
}

function translateRole(role) {
  const value = String(role || "");
  if (isPlatformModeratorRole(value)) return "Модератор платформы";
  if (value === "CommunityAdmin") return "Администратор сообщества";
  if (value === "PLATFORM_MODERATOR") return "Модератор платформы";
  return "Пользователь";
}

function translateReportStatus(status) {
  const value = String(status || "").toUpperCase();
  if (value === "NEW") return "Новая";
  if (value === "RESOLVED") return "Обработана";
  return status;
}

function translateTargetType(type) {
  const value = normalizeTargetType(type);
  if (value === "POST") return "Пост";
  if (value === "COMMUNITY") return "Сообщество";
  if (value === "USER") return "Пользователь";
  return value || "Объект";
}

function translateAuditAction(action) {
  const value = String(action || "").toUpperCase();
  if (value === "POST_DELETED") return "Пост удален";
  if (value === "USER_BLOCKED") return "Пользователь заблокирован";
  if (value === "USER_UNBLOCKED") return "Пользователь разблокирован";
  if (value === "COMMUNITY_REPORT_RESOLVED") return "Жалоба на сообщество закрыта";
  if (value === "POST_REPORT_RESOLVED") return "Жалоба на пост закрыта";
  return action;
}
