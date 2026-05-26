import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formData, formatDate } from "../core/dom.js";
import { preloadUsers, userDisplayName, userProfileHref } from "../core/identity.js";
import { bindReportButtons } from "../core/reports.js";
import { getSession } from "../core/session.js";
import { toast } from "../core/toast.js";

const root = document.querySelector("[data-community-page]");
const params = new URLSearchParams(location.search);
let communityId = params.get("communityId");
const communityUsername = params.get("username");

let community = null;
let posts = [];
let suggestedPosts = [];
let joinRequests = [];
let members = [];
let activeTab = "posts";
let editingDescription = false;

if (!communityId && !communityUsername) {
  root.innerHTML = empty("Сообщество не найдено.");
} else {
  await loadCommunityPage();
}

async function loadCommunityPage() {
  root.innerHTML = empty("Загружаем сообщество...");

  try {
    community = communityUsername
      ? await api(`/api/communities/by-username/${encodeURIComponent(communityUsername)}`)
      : await api(`/api/communities/${communityId}`);
    communityId = community.id;

    if (!communityUsername && community.username) {
      history.replaceState(null, "", `/CommunityDetails?username=${encodeURIComponent(community.username)}`);
    }

    posts = canViewCommunityPosts() ? await api(`/communities/${community.id}/posts`) : [];
    suggestedPosts = [];
    joinRequests = [];
    members = [];

    if (isOwner()) {
      [suggestedPosts, joinRequests, members] = await Promise.all([
        api(`/api/communities/${community.id}/suggested-posts?status=Pending`),
        api(`/api/communities/${community.id}/join-requests?status=Pending`),
        api(`/api/communities/${community.id}/members`)
      ]);
    }

    await preloadUsers([
      ...posts.map((post) => post.authorId),
      ...suggestedPosts.map((post) => post.authorUserId),
      ...joinRequests.map((request) => request.userId),
      ...members.map((member) => member.userId)
    ]);

    renderPage();
  } catch (error) {
    root.innerHTML = empty(error.message);
  }
}

function renderPage() {
  root.innerHTML = `
    <section class="community-hero panel">
      <div class="community-avatar">${communityInitial()}</div>
      <div>
        <h1>${escapeHtml(community.name)}</h1>
        <p class="muted">@${escapeHtml(community.username)}</p>
        <p>${escapeHtml(community.description ?? "")}</p>
        <div class="meta">
          <span>${canViewCommunityPosts() ? `${posts.length} постов` : "Посты скрыты"}</span>
          <span>${community.membersCount ?? 0} подписчиков</span>
          <span>${typeLabel(community.type)}</span>
        </div>
      </div>
      <div class="community-hero-action">
        ${renderCommunityAction()}
      </div>
    </section>

    ${renderEditDescriptionForm()}
    ${renderSuggestForm()}

    <section class="community-tabs">
      ${renderTab("posts", canViewCommunityPosts() ? `Посты ${posts.length}` : "Посты")}
      ${isOwner() ? renderTab("suggested", `Предложенные ${suggestedPosts.length}`) : ""}
      ${isOwner() ? renderTab("joinRequests", `Заявки ${joinRequests.length}`) : ""}
      ${isOwner() ? renderTab("members", `Участники ${members.length}`) : ""}
    </section>

    <section class="community-center" data-community-center>
      ${renderActiveTab()}
    </section>`;

  bindActions();
}

function renderCommunityAction() {
  if (isCommunityBlocked()) {
    return `<button class="button secondary" type="button" disabled>Сообщество заблокировано</button>`;
  }

  if (isOwner()) {
    return `
      <button class="button secondary" type="button" disabled>Вы владелец</button>
      <button class="button primary" type="button" data-edit-community>${editingDescription ? "Закрыть" : "Изменить"}</button>
      <button class="button danger" type="button" data-delete-community>Удалить сообщество</button>
      ${renderCommunityReportButton()}`;
  }

  if (isMember()) {
    return `
      <button class="button secondary" type="button" data-leave-community="${community.id}">Выйти</button>
      ${renderCommunityReportButton()}`;
  }

  if (community.type === "Closed") {
    if (community.currentUserJoinRequest?.status === "Pending") {
      return `
        <button class="button secondary" type="button" disabled>Заявка отправлена</button>
        ${renderCommunityReportButton()}`;
    }

    return `
      <button class="button primary" type="button" data-request-join="${community.id}">Подать заявку</button>
      ${renderCommunityReportButton()}`;
  }

  return `
    <button class="button primary" type="button" data-join-community="${community.id}">Вступить</button>
    ${renderCommunityReportButton()}`;
}

function renderEditDescriptionForm() {
  if (!isOwner() || !editingDescription) return "";

  return `
    <form class="panel form-grid community-edit-form" data-form="edit-community">
      <h2>Описание сообщества</h2>
      <label>Описание
        <textarea name="description" maxlength="500">${escapeHtml(community.description ?? "")}</textarea>
      </label>
      <div class="actions">
        <button class="button primary" type="submit">Сохранить</button>
        <button class="button secondary" type="button" data-cancel-community-edit>Отмена</button>
      </div>
    </form>`;
}

function renderSuggestForm() {
  if (!isMember() || isOwner()) return "";

  return `
    <form class="panel form-grid community-suggest-form" data-form="suggest-post">
      <h2>Предложить пост</h2>
      <label>Заголовок
        <input name="title" maxlength="120" required />
      </label>
      <label>Текст
        <textarea name="text" maxlength="10000" required></textarea>
      </label>
      <button class="button primary" type="submit">Отправить владельцу</button>
    </form>`;
}

function renderTab(tab, label) {
  return `<button class="button ${activeTab === tab ? "primary" : "secondary"}" type="button" data-tab="${tab}">${escapeHtml(label)}</button>`;
}

function renderActiveTab() {
  if (activeTab === "suggested") return renderSuggestedPosts();
  if (activeTab === "joinRequests") return renderJoinRequests();
  if (activeTab === "members") return renderMembers();
  return renderPosts();
}

function renderPosts() {
  if (!canViewCommunityPosts()) {
    return empty("Посты закрытого сообщества видны только участникам после одобрения заявки владельцем.");
  }

  return posts.length
    ? posts.map(renderPost).join("")
    : empty("В этом сообществе пока нет постов.");
}

function renderPost(post) {
  return `
    <article class="card community-post-card">
      <div class="post-card-meta">
        <span class="community-mark">${communityInitial()}</span>
        <strong>${escapeHtml(community.name)}</strong>
        ${renderAuthorLink(post.authorId)}
        <span>${formatDate(post.createdAt)}</span>
      </div>
      <a class="community-post-title" href="/PostDetails?postId=${post.id}">${escapeHtml(post.title)}</a>
      <p>${escapeHtml(post.text ?? "")}</p>
      ${renderMedia(post.id, post.media)}
      <div class="post-card-footer">
        ${renderVoteControls(post)}
        <a class="comment-pill" href="/PostDetails?postId=${post.id}#comments">${getComments(post.id).length} комментариев</a>
        <button class="button secondary" type="button"
                data-report-target-type="POST"
                data-report-target-id="${post.id}"
                data-report-target-label="Пост: ${escapeHtml(post.title)}">
          Пожаловаться
        </button>
      </div>
    </article>`;
}

function renderSuggestedPosts() {
  return suggestedPosts.length
    ? suggestedPosts.map(renderSuggestedPost).join("")
    : empty("Новых предложенных постов нет.");
}

function renderSuggestedPost(post) {
  return `
    <article class="card suggested-review-card">
      <div class="post-card-meta">
        <span class="community-mark">${communityInitial()}</span>
        <strong>${escapeHtml(community.name)}</strong>
        ${renderAuthorLink(post.authorUserId)}
        <span>${formatDate(post.createdAtUtc)}</span>
      </div>
      <h2>${escapeHtml(post.title)}</h2>
      <p>${escapeHtml(post.text)}</p>
      <div class="actions">
        <button class="button primary" type="button" data-approve-suggested="${post.id}">Опубликовать</button>
        <button class="button danger" type="button" data-reject-suggested="${post.id}">Удалить</button>
      </div>
    </article>`;
}

function renderMembers() {
  return members.length
    ? members.map(renderMember).join("")
    : empty("Участников пока нет.");
}

function renderJoinRequests() {
  return joinRequests.length
    ? joinRequests.map(renderJoinRequest).join("")
    : empty("Новых заявок на вступление нет.");
}

function renderJoinRequest(request) {
  return `
    <article class="card suggested-review-card">
      <div class="post-card-meta">
        <span class="community-logo">${userInitial(request.userId)}</span>
        ${renderAuthorLink(request.userId)}
        <span>${formatDate(request.createdAtUtc)}</span>
      </div>
      <p>Пользователь хочет вступить в закрытое сообщество.</p>
      <div class="actions">
        <button class="button primary" type="button" data-approve-join-request="${request.id}">Принять</button>
        <button class="button danger" type="button" data-reject-join-request="${request.id}">Отклонить</button>
      </div>
    </article>`;
}

function renderMember(member) {
  const currentUserId = getSession().user?.id;
  const canRemove = member.userId !== currentUserId && member.role !== "Owner";

  return `
    <article class="member-row-card">
      <a href="${escapeHtml(userProfileHref(member.userId) || "#")}">
        <span class="community-logo">${userInitial(member.userId)}</span>
        <strong>${escapeHtml(userDisplayName(member.userId))}</strong>
      </a>
      <span class="badge">${roleLabel(member.role)}</span>
      ${canRemove ? `<button class="button danger" type="button" data-remove-member="${member.userId}">Удалить</button>` : ""}
    </article>`;
}

function bindActions() {
  bindReportButtons(root);

  for (const button of root.querySelectorAll("[data-tab]")) {
    button.addEventListener("click", () => {
      activeTab = button.dataset.tab;
      renderPage();
    });
  }

  for (const button of root.querySelectorAll("[data-vote-post]")) {
    button.addEventListener("click", () => votePost(button));
  }

  root.querySelector("[data-join-community]")?.addEventListener("click", (event) =>
    runCommunityAction(event.currentTarget, "POST"));

  root.querySelector("[data-request-join]")?.addEventListener("click", (event) =>
    requestJoinCommunity(event.currentTarget));

  root.querySelector("[data-leave-community]")?.addEventListener("click", (event) =>
    runCommunityAction(event.currentTarget, "DELETE"));

  root.querySelector("[data-edit-community]")?.addEventListener("click", () => {
    editingDescription = !editingDescription;
    renderPage();
  });

  root.querySelector("[data-cancel-community-edit]")?.addEventListener("click", () => {
    editingDescription = false;
    renderPage();
  });

  root.querySelector("[data-delete-community]")?.addEventListener("click", (event) =>
    deleteCommunity(event.currentTarget));

  root.querySelector('[data-form="edit-community"]')?.addEventListener("submit", async (event) => {
    event.preventDefault();
    const form = event.currentTarget;
    const button = form.querySelector('button[type="submit"]');
    await runWithButton(button, "Сохраняем...", async () => {
      community = await api(`/api/communities/${community.id}`, toJson("PUT", formData(form)));
      editingDescription = false;
      toast("Описание сообщества обновлено.");
      renderPage();
    });
  });

  root.querySelector('[data-form="suggest-post"]')?.addEventListener("submit", async (event) => {
    event.preventDefault();
    const form = event.currentTarget;
    const button = form.querySelector('button[type="submit"]');
    await runWithButton(button, "Отправляем...", async () => {
      await api(`/api/communities/${community.id}/suggested-posts`, toJson("POST", formData(form)));
      form.reset();
      toast("Пост отправлен владельцу сообщества.");
    });
  });

  for (const button of root.querySelectorAll("[data-approve-suggested]")) {
    button.addEventListener("click", () => reviewSuggestedPost(button, button.dataset.approveSuggested, "approve"));
  }

  for (const button of root.querySelectorAll("[data-reject-suggested]")) {
    button.addEventListener("click", () => reviewSuggestedPost(button, button.dataset.rejectSuggested, "reject"));
  }

  for (const button of root.querySelectorAll("[data-approve-join-request]")) {
    button.addEventListener("click", () => reviewJoinRequest(button, button.dataset.approveJoinRequest, "approve"));
  }

  for (const button of root.querySelectorAll("[data-reject-join-request]")) {
    button.addEventListener("click", () => reviewJoinRequest(button, button.dataset.rejectJoinRequest, "reject"));
  }

  for (const button of root.querySelectorAll("[data-remove-member]")) {
    button.addEventListener("click", () => removeMember(button));
  }
}

async function runCommunityAction(button, method) {
  await runWithButton(button, method === "POST" ? "Вступаем..." : "Выходим...", async () => {
    const path = method === "POST" ? `/api/communities/${community.id}/join` : `/api/communities/${community.id}/membership`;
    await api(path, { method });
    toast(method === "POST" ? "Вы вступили в сообщество." : "Вы вышли из сообщества.");
    await loadCommunityPage();
  });
}

async function requestJoinCommunity(button) {
  await runWithButton(button, "Отправляем...", async () => {
    await api(`/api/communities/${community.id}/join-requests`, { method: "POST" });
    toast("Заявка отправлена владельцу сообщества.");
    await loadCommunityPage();
  });
}

async function reviewSuggestedPost(button, suggestedPostId, action) {
  const request = action === "reject"
    ? toJson("POST", { comment: "Удалено владельцем сообщества" })
    : { method: "POST" };

  await runWithButton(button, action === "approve" ? "Публикуем..." : "Удаляем...", async () => {
    await api(`/api/communities/${community.id}/suggested-posts/${suggestedPostId}/${action}`, request);
    toast(action === "approve" ? "Предложенный пост опубликован." : "Предложенный пост удален.");
    activeTab = "suggested";
    await loadCommunityPage();
  });
}

async function reviewJoinRequest(button, requestId, action) {
  const request = action === "reject"
    ? toJson("POST", { comment: "Отклонено владельцем сообщества" })
    : { method: "POST" };

  await runWithButton(button, action === "approve" ? "Принимаем..." : "Отклоняем...", async () => {
    await api(`/api/communities/${community.id}/join-requests/${requestId}/${action}`, request);
    toast(action === "approve" ? "Пользователь принят в сообщество." : "Заявка отклонена.");
    activeTab = "joinRequests";
    await loadCommunityPage();
  });
}

async function removeMember(button) {
  await runWithButton(button, "Удаляем...", async () => {
    await api(`/api/communities/${community.id}/members/${button.dataset.removeMember}`, { method: "DELETE" });
    toast("Участник удален из сообщества.");
    activeTab = "members";
    await loadCommunityPage();
  });
}

async function deleteCommunity(button) {
  const firstConfirmation = confirm(
    `Удалить сообщество «${community.name}»? Все посты и жалобы на это сообщество будут удалены.`
  );
  if (!firstConfirmation) {
    return;
  }

  const typedUsername = prompt(`Для подтверждения введите username сообщества: ${community.username}`);
  if ((typedUsername || "").trim().toLowerCase() !== community.username.toLowerCase()) {
    toast("Удаление отменено: username сообщества введен неверно.", "error");
    return;
  }

  await runWithButton(button, "Удаляем...", async () => {
    await api(`/api/communities/${community.id}`, { method: "DELETE" });
    toast("Сообщество удалено.");
    location.href = "/Communities";
  });
}

async function votePost(button) {
  await runWithButton(button, "...", async () => {
    await api(`/posts/${button.dataset.votePost}/vote`, toJson("POST", {
      value: Number(button.dataset.voteValue)
    }));
    posts = await api(`/communities/${community.id}/posts`);
    renderPage();
  });
}

function renderVoteControls(post) {
  const upActive = post.viewerVote === 1 ? " active" : "";
  const downActive = post.viewerVote === -1 ? " active" : "";
  const upValue = post.viewerVote === 1 ? 0 : 1;
  const downValue = post.viewerVote === -1 ? 0 : -1;

  return `
    <div class="vote-bar" aria-label="Оценка поста">
      <button class="vote-button${upActive}" type="button" data-vote-post="${post.id}" data-vote-value="${upValue}" title="Поднять пост">▲</button>
      <strong>${post.score ?? 0}</strong>
      <button class="vote-button${downActive}" type="button" data-vote-post="${post.id}" data-vote-value="${downValue}" title="Опустить пост">▼</button>
    </div>`;
}

function renderMedia(postId, media = []) {
  if (!media.length) return "";

  return `
    <div class="feed-media-grid">
      ${media.map((item) => renderMediaItem(postId, item)).join("")}
    </div>`;
}

function renderMediaItem(postId, item) {
  const mediaUrl = `/posts/${postId}/media/${item.id}`;
  if ((item.contentType || "").startsWith("image/")) {
    return `<figure class="feed-media-item"><img src="${mediaUrl}" alt="${escapeHtml(item.fileName)}" loading="lazy" /></figure>`;
  }

  return `<span class="media-file">${escapeHtml(item.fileName)} · ${Math.ceil((item.size ?? 0) / 1024)} KB</span>`;
}

function getComments(postId) {
  return readJson(`socialhub.comments.${postId}`, []);
}

function readJson(key, fallback) {
  try {
    return JSON.parse(localStorage.getItem(key) || JSON.stringify(fallback));
  } catch {
    return fallback;
  }
}

function isMember() {
  return Boolean(community?.currentUserMembership);
}

function isOwner() {
  return community?.currentUserMembership?.role === "Owner";
}

function canViewCommunityPosts() {
  if (isCommunityBlocked()) return false;
  return community?.type !== "Closed" || isMember();
}

function isCommunityBlocked() {
  return String(community?.status || "").toLowerCase() === "blocked";
}

function communityInitial() {
  return String(community?.name ?? "C").trim().slice(0, 1).toUpperCase() || "C";
}

function userInitial(userId) {
  return userDisplayName(userId).trim().slice(0, 1).toUpperCase() || "U";
}

function renderAuthorLink(userId) {
  const href = userProfileHref(userId);
  const label = `Автор: ${userDisplayName(userId)}`;
  return href
    ? `<a href="${escapeHtml(href)}">${escapeHtml(label)}</a>`
    : `<span>${escapeHtml(label)}</span>`;
}

function renderCommunityReportButton() {
  return `
    <button class="button secondary" type="button"
            data-report-target-type="COMMUNITY"
            data-report-target-id="${community.id}"
            data-report-target-label="Сообщество: ${escapeHtml(community.name)}">
      Пожаловаться
    </button>`;
}

function typeLabel(type) {
  return type === "Closed" ? "Закрытое" : "Открытое";
}

function roleLabel(role) {
  return {
    Owner: "Владелец",
    Admin: "Администратор",
    Member: "Участник"
  }[role] ?? "Участник";
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
