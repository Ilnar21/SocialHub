import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formData, formatDate } from "../core/dom.js";
import { preloadUsers, userDisplayName, userProfileHref, userUsername } from "../core/identity.js";
import { getSession, isPlatformModerator } from "../core/session.js";
import { toast } from "../core/toast.js";

const blockUserForm = document.querySelector('[data-form="block-user"]');
const communityForm = document.querySelector('[data-form="community-moderation"]');
const userSearchInput = document.querySelector("[data-user-search]");
const userSearchResult = document.querySelector("[data-user-search-result]");
const communitySearchInput = document.querySelector("[data-community-search]");
const communitySearchResult = document.querySelector("[data-community-search-result]");
const communityBlockReasonInput = document.querySelector("[data-community-block-reason]");
const blockedUsersList = document.querySelector("[data-blocked-users-list]");
const blockedCommunitiesList = document.querySelector("[data-blocked-communities-list]");
const reportsList = document.querySelector("[data-reports-list]");
const auditList = document.querySelector("[data-audit-list]");

const usersById = new Map();
const communitiesById = new Map();
const postsById = new Map();

let selectedUser = null;
let selectedCommunity = null;

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

  document.querySelector("[data-find-community]")?.addEventListener("click", async (event) => {
    await runWithButton(event.currentTarget, "Ищем...", findCommunityByUsername);
  });

  userSearchInput?.addEventListener("input", () => {
    selectedUser = null;
    userSearchResult.innerHTML = "";
  });

  communitySearchInput?.addEventListener("input", () => {
    selectedCommunity = null;
    communitySearchResult.innerHTML = "";
  });

  blockUserForm?.addEventListener("submit", blockUser);
  communityForm?.addEventListener("submit", (event) => event.preventDefault());

  await loadModeration();
}

async function loadModeration() {
  await Promise.all([loadReports(), loadAudit(), loadBlockedUsers(), loadBlockedCommunities()]);
}

async function findUserByUsername() {
  const username = normalizeUsername(userSearchInput.value);
  if (!username) {
    toast("Введите username пользователя", "error");
    return null;
  }

  try {
    selectedUser = await api(`/api/users/by-username/${encodeURIComponent(username)}`);
    cacheUser(selectedUser);
    userSearchResult.innerHTML = renderUserSearchResult(selectedUser);
    bindUserActionButtons(userSearchResult);
    return selectedUser;
  } catch (error) {
    selectedUser = null;
    userSearchResult.innerHTML = empty(error.message);
    return null;
  }
}

async function findCommunityByUsername() {
  const username = normalizeUsername(communitySearchInput.value);
  if (!username) {
    toast("Введите username сообщества", "error");
    return null;
  }

  try {
    selectedCommunity = await api(`/api/communities/by-username/${encodeURIComponent(username)}`);
    cacheCommunity(selectedCommunity);
    communitySearchResult.innerHTML = renderCommunitySearchResult(selectedCommunity);
    bindCommunityActionButtons(communitySearchResult);
    return selectedCommunity;
  } catch (error) {
    selectedCommunity = null;
    communitySearchResult.innerHTML = empty(error.message);
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

async function blockCommunity(button, community = selectedCommunity, reason = communityBlockReasonInput?.value) {
  if (!community?.id) {
    toast("Сначала найдите сообщество", "error");
    return;
  }

  const blockReason = String(reason || "").trim();
  if (!blockReason) {
    toast("Укажите причину блокировки сообщества", "error");
    return;
  }

  if (isCommunityBlocked(community)) {
    toast("Сообщество уже заблокировано", "error");
    return;
  }

  await runWithButton(button, "Блокируем...", async () => {
    await api(`/api/communities/${community.id}/blocks`, toJson("POST", { reason: blockReason }));
    toast("Сообщество заблокировано");
    await refreshCommunitySearchResult(community.username);
    await Promise.all([loadReports(), loadAudit(), loadBlockedCommunities()]);
  });
}

async function unblockCommunity(button, community = selectedCommunity) {
  if (!community?.id) {
    toast("Сначала найдите сообщество", "error");
    return;
  }

  await runWithButton(button, "Разблокируем...", async () => {
    await api(`/api/communities/${community.id}/blocks`, { method: "DELETE" });
    toast("Сообщество разблокировано");
    await refreshCommunitySearchResult(community.username);
    await Promise.all([loadReports(), loadAudit(), loadBlockedCommunities()]);
  });
}

async function refreshCommunitySearchResult(username) {
  if (!username) return;
  try {
    selectedCommunity = await api(`/api/communities/by-username/${encodeURIComponent(username)}`);
    cacheCommunity(selectedCommunity);
    communitySearchResult.innerHTML = renderCommunitySearchResult(selectedCommunity);
    bindCommunityActionButtons(communitySearchResult);
  } catch {
    selectedCommunity = null;
    communitySearchResult.innerHTML = "";
  }
}

async function loadBlockedUsers() {
  blockedUsersList.innerHTML = empty("Загружаем блокировки...");
  try {
    const currentUserId = getSession().user?.id;
    const users = await api("/api/users/");
    users.forEach(cacheUser);
    const blockedUsers = users.filter((user) => user.id !== currentUserId && isBlocked(user));
    blockedUsersList.innerHTML = blockedUsers.length
      ? blockedUsers.map(renderBlockedUser).join("")
      : empty("Сейчас нет заблокированных пользователей.");
    bindUserActionButtons(blockedUsersList);
  } catch (error) {
    blockedUsersList.innerHTML = empty(error.message);
  }
}

async function loadBlockedCommunities() {
  blockedCommunitiesList.innerHTML = empty("Загружаем заблокированные сообщества...");
  try {
    const communities = await api("/api/communities/blocked");
    communities.forEach(cacheCommunity);
    blockedCommunitiesList.innerHTML = communities.length
      ? communities.map(renderBlockedCommunity).join("")
      : empty("Сейчас нет заблокированных сообществ.");
    bindCommunityActionButtons(blockedCommunitiesList);
  } catch (error) {
    blockedCommunitiesList.innerHTML = empty(error.message);
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
        <h2>${renderUserProfileLink(user)}</h2>
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
        <h2>${renderUserProfileLink(user)}</h2>
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

function renderCommunitySearchResult(community) {
  const blocked = isCommunityBlocked(community);
  const actions = blocked
    ? `<button class="button secondary" type="button" data-unblock-community="${community.id}">Разблокировать сообщество</button>`
    : `<button class="button danger" type="button" data-block-community="${community.id}">Заблокировать сообщество</button>`;

  return `
    <article class="card">
      <div class="row">
        <h2>${renderCommunityLink(community)}</h2>
        <span class="badge ${blocked ? "danger" : "success"}">${blocked ? "Заблокировано" : "Активно"}</span>
      </div>
      <p>${escapeHtml(community.description || "Описание пока не заполнено.")}</p>
      <div class="meta">
        <span>@${escapeHtml(community.username)}</span>
        <span>${translateCommunityType(community.type)}</span>
        <span>${community.membersCount ?? 0} подписчиков</span>
      </div>
      ${blocked && community.blockReason ? `<p class="muted">${escapeHtml(community.blockReason)}</p>` : ""}
      <div class="actions">
        ${actions}
      </div>
    </article>`;
}

function renderBlockedCommunity(community) {
  return `
    <article class="card">
      <div class="row">
        <h2>${renderCommunityLink(community)}</h2>
        <span class="badge danger">Заблокировано</span>
      </div>
      <p>${escapeHtml(community.blockReason || "Причина не указана.")}</p>
      <div class="meta">
        <span>@${escapeHtml(community.username)}</span>
        <span>${translateCommunityType(community.type)}</span>
        <span>${community.membersCount ?? 0} подписчиков</span>
        ${community.blockedAtUtc ? `<span>С ${formatDate(community.blockedAtUtc)}</span>` : ""}
      </div>
      <div class="actions">
        <button class="button secondary" type="button" data-unblock-community="${community.id}">Разблокировать сообщество</button>
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

function bindCommunityActionButtons(root) {
  for (const button of root.querySelectorAll("[data-block-community]")) {
    if (button.dataset.bound === "true") continue;
    button.dataset.bound = "true";
    button.addEventListener("click", () => {
      const community = communitiesById.get(button.dataset.blockCommunity) ?? selectedCommunity;
      blockCommunity(button, community);
    });
  }

  for (const button of root.querySelectorAll("[data-unblock-community]")) {
    if (button.dataset.bound === "true") continue;
    button.dataset.bound = "true";
    button.addEventListener("click", () => {
      const community = communitiesById.get(button.dataset.unblockCommunity) ?? selectedCommunity;
      unblockCommunity(button, community);
    });
  }
}

async function loadReports() {
  reportsList.innerHTML = empty("Загружаем жалобы...");
  try {
    const reports = await api("/api/reports?status=NEW");
    await hydrateReports(reports);
    reportsList.innerHTML = reports.length ? reports.map(renderReport).join("") : empty("Новых жалоб нет.");
    bindReportActionButtons();
  } catch (error) {
    reportsList.innerHTML = empty(error.message);
  }
}

async function hydrateReports(reports) {
  await preloadUsers(reports.map((report) => report.reporterUserId));
  await Promise.all(reports.map(async (report) => {
    const targetType = normalizeTargetType(report.targetType);
    if (targetType === "POST") {
      await hydratePostReport(report);
    } else if (targetType === "COMMUNITY") {
      await hydrateCommunity(report.targetId);
    }
  }));
}

async function hydratePostReport(report) {
  try {
    const post = await api(`/posts/${report.targetId}`);
    postsById.set(report.targetId, post);
    await preloadUsers([post.authorId]);
    await hydrateCommunity(post.communityId);
  } catch {
    postsById.set(report.targetId, null);
  }
}

async function hydrateCommunity(communityId) {
  if (!communityId || communitiesById.has(communityId)) return communitiesById.get(communityId);

  try {
    const community = await api(`/api/communities/${communityId}`);
    cacheCommunity(community);
    return community;
  } catch {
    communitiesById.set(communityId, null);
    return null;
  }
}

function bindReportActionButtons() {
  for (const button of reportsList.querySelectorAll("[data-delete-reported-post]")) {
    button.addEventListener("click", () => deleteReportedPost(button));
  }

  for (const button of reportsList.querySelectorAll("[data-resolve-report]")) {
    button.addEventListener("click", () => resolveReport(button));
  }

  for (const button of reportsList.querySelectorAll("[data-block-reported-community]")) {
    button.addEventListener("click", () => {
      const community = communitiesById.get(button.dataset.communityId);
      blockReportedCommunity(button, community, button.dataset.reportId);
    });
  }

  for (const button of reportsList.querySelectorAll("[data-unblock-reported-community]")) {
    button.addEventListener("click", () => {
      const community = communitiesById.get(button.dataset.communityId);
      unblockCommunity(button, community);
    });
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

async function blockReportedCommunity(button, community, reportId) {
  if (!community) {
    toast("Сообщество из жалобы недоступно", "error");
    return;
  }

  await blockCommunity(button, community, `Жалоба: ${button.dataset.reportReason || "нарушение правил"}`);
  await api(`/api/reports/${reportId}/resolve`, toJson("POST", {
    comment: "Сообщество заблокировано по жалобе"
  }));
  await loadModeration();
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

  return `
    <article class="card">
      <div class="row">
        <h2>${escapeHtml(report.reason)}</h2>
        <span class="badge warn">${translateReportStatus(report.status)}</span>
      </div>
      <p>${escapeHtml(report.comment ?? "")}</p>
      <div class="meta">
        <span>Отправил: ${renderUserByIdLink(report.reporterUserId)}</span>
        <span>${formatDate(report.createdAtUtc)}</span>
      </div>
      ${renderReportTarget(report, targetType)}
      <div class="actions">
        ${renderReportActions(report, targetType)}
      </div>
    </article>`;
}

function renderReportTarget(report, targetType) {
  if (targetType === "POST") {
    const post = postsById.get(report.targetId);
    if (!post) {
      return `<div class="panel empty"><p>Пост недоступен или уже удален.</p></div>`;
    }

    const community = communitiesById.get(post.communityId);
    return `
      <div class="report-target">
          <h3><a href="/PostDetails?postId=${encodeURIComponent(post.id)}">${escapeHtml(post.title)}</a></h3>
          <p>${escapeHtml(post.text ?? "")}</p>
          <div class="meta">
            <span>Сообщество: ${renderCommunityLink(community)}</span>
            <span>Автор: ${renderUserByIdLink(post.authorId)}</span>
            <span>${formatDate(post.createdAt)}</span>
          </div>
      </div>`;
  }

  if (targetType === "COMMUNITY") {
    const community = communitiesById.get(report.targetId);
    if (!community) {
      return `<div class="panel empty"><p>Сообщество недоступно или уже скрыто.</p></div>`;
    }

    return `
      <div class="report-target">
          <div class="row">
            <h3>${renderCommunityLink(community)}</h3>
            <span class="badge ${isCommunityBlocked(community) ? "danger" : "success"}">
              ${isCommunityBlocked(community) ? "Заблокировано" : "Активно"}
            </span>
          </div>
          <p>${escapeHtml(community.description || "Описание пока не заполнено.")}</p>
          <div class="meta">
            <span>@${escapeHtml(community.username)}</span>
            <span>${translateCommunityType(community.type)}</span>
            <span>${community.membersCount ?? 0} подписчиков</span>
          </div>
      </div>`;
  }

  return `<div class="panel empty"><p>${translateTargetType(targetType)} недоступен для просмотра.</p></div>`;
}

function renderReportActions(report, targetType) {
  if (targetType === "POST") {
    return `
      <button class="button danger" data-delete-reported-post="${report.id}">Удалить пост</button>
      <button class="button secondary" data-resolve-report="${report.id}" data-report-target-type="${targetType}">Закрыть без удаления</button>`;
  }

  if (targetType === "COMMUNITY") {
    const community = communitiesById.get(report.targetId);
    const moderationAction = community && isCommunityBlocked(community)
      ? `<button class="button secondary" data-unblock-reported-community="${report.id}" data-community-id="${community.id}">Разблокировать сообщество</button>`
      : community
        ? `<button class="button danger" data-block-reported-community="${report.id}" data-report-id="${report.id}" data-report-reason="${escapeHtml(report.reason)}" data-community-id="${community.id}">Заблокировать сообщество</button>`
        : "";

    return `
      ${moderationAction}
      <button class="button secondary" data-resolve-report="${report.id}" data-report-target-type="${targetType}">Закрыть жалобу</button>`;
  }

  return `<button class="button secondary" data-resolve-report="${report.id}" data-report-target-type="${targetType}">Закрыть жалобу</button>`;
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
        <span>${translateTargetType(entry.targetType)}</span>
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

function cacheUser(user) {
  if (user?.id) {
    usersById.set(user.id, user);
  }
}

function cacheCommunity(community) {
  if (community?.id) {
    communitiesById.set(community.id, community);
  }
}

function normalizeUsername(value) {
  return String(value || "").trim().replace(/^@+/, "");
}

function displayName(user) {
  return user?.profile?.displayName || user?.username || "Пользователь";
}

function renderUserProfileLink(user) {
  if (!user?.username) return escapeHtml(displayName(user));
  return `<a href="/UserProfile?username=${encodeURIComponent(user.username)}">${escapeHtml(displayName(user))}</a>`;
}

function renderUserByIdLink(userId) {
  const username = userUsername(userId) || usersById.get(userId)?.username;
  const label = userDisplayName(userId) || displayName(usersById.get(userId));
  const href = userProfileHref(userId) || (username ? `/UserProfile?username=${encodeURIComponent(username)}` : "");
  return href ? `<a href="${escapeHtml(href)}">${escapeHtml(label)}</a>` : `<span>${escapeHtml(label)}</span>`;
}

function renderCommunityLink(community) {
  if (!community?.username) {
    return `<span>${escapeHtml(community?.name || "Сообщество")}</span>`;
  }

  return `<a href="/CommunityDetails?username=${encodeURIComponent(community.username)}">${escapeHtml(community.name)}</a>`;
}

function normalizeTargetType(value) {
  return String(value || "").trim().toUpperCase();
}

function isPlatformModeratorRole(role) {
  const value = String(role || "").toLowerCase();
  return value === "platformmoderator" || value === "platform_moderator" || value === "moderator";
}

function isBlocked(user) {
  return user.status === "Blocked" || user.status === "BLOCKED";
}

function isCommunityBlocked(community) {
  return String(community?.status || "").toLowerCase() === "blocked";
}

function translateRole(role) {
  const value = String(role || "");
  if (isPlatformModeratorRole(value)) return "Модератор платформы";
  if (value === "CommunityAdmin") return "Администратор сообщества";
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

function translateCommunityType(type) {
  return type === "Closed" ? "Закрытое" : "Открытое";
}

function translateAuditAction(action) {
  const value = String(action || "").toUpperCase();
  if (value === "POST_DELETED") return "Пост удален";
  if (value === "USER_BLOCKED") return "Пользователь заблокирован";
  if (value === "USER_UNBLOCKED") return "Пользователь разблокирован";
  if (value === "COMMUNITY_BLOCKED") return "Сообщество заблокировано";
  if (value === "COMMUNITY_UNBLOCKED") return "Сообщество разблокировано";
  if (value === "COMMUNITY_REPORT_RESOLVED") return "Жалоба на сообщество закрыта";
  if (value === "POST_REPORT_RESOLVED") return "Жалоба на пост закрыта";
  return action;
}
