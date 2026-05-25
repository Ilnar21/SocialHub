import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formData, formatDate } from "../core/dom.js";
import { preloadUsers, userDisplayName, userProfileHref } from "../core/identity.js";
import { getSession } from "../core/session.js";
import { toast } from "../core/toast.js";

const root = document.querySelector("[data-community-page]");
const params = new URLSearchParams(location.search);
let communityId = params.get("communityId");
const communityUsername = params.get("username");

let community = null;
let posts = [];
let suggestedPosts = [];
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

    posts = await api(`/communities/${community.id}/posts`);
    suggestedPosts = [];
    members = [];

    if (isOwner()) {
      [suggestedPosts, members] = await Promise.all([
        api(`/api/communities/${community.id}/suggested-posts?status=Pending`),
        api(`/api/communities/${community.id}/members`)
      ]);
    }

    await preloadUsers([
      ...posts.map((post) => post.authorId),
      ...suggestedPosts.map((post) => post.authorUserId),
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
          <span>${posts.length} постов</span>
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
      ${renderTab("posts", `Посты ${posts.length}`)}
      ${isOwner() ? renderTab("suggested", `Предложенные ${suggestedPosts.length}`) : ""}
      ${isOwner() ? renderTab("members", `Участники ${members.length}`) : ""}
    </section>

    <section class="community-center" data-community-center>
      ${renderActiveTab()}
    </section>`;

  bindActions();
}

function renderCommunityAction() {
  if (isOwner()) {
    return `
      <button class="button secondary" type="button" disabled>Вы владелец</button>
      <button class="button primary" type="button" data-edit-community>${editingDescription ? "Закрыть" : "Изменить"}</button>`;
  }

  if (isMember()) {
    return `<button class="button secondary" type="button" data-leave-community="${community.id}">Выйти</button>`;
  }

  return `<button class="button primary" type="button" data-join-community="${community.id}">Вступить</button>`;
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
  if (activeTab === "members") return renderMembers();
  return renderPosts();
}

function renderPosts() {
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

async function removeMember(button) {
  await runWithButton(button, "Удаляем...", async () => {
    await api(`/api/communities/${community.id}/members/${button.dataset.removeMember}`, { method: "DELETE" });
    toast("Участник удален из сообщества.");
    activeTab = "members";
    await loadCommunityPage();
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
