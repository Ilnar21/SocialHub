import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formData, formatDate } from "../core/dom.js";
import { preloadUsers, userDisplayName, userProfileHref } from "../core/identity.js";
import { getSession } from "../core/session.js";
import { toast } from "../core/toast.js";

const postRoot = document.querySelector("[data-post-page]");
const communityRoot = document.querySelector("[data-community-sidebar]");
const postId = new URLSearchParams(location.search).get("postId");

let post = null;
let community = null;
let communityPosts = [];

if (!postId) {
  postRoot.innerHTML = empty("Пост не найден.");
} else {
  await loadPage();
}

async function loadPage() {
  postRoot.innerHTML = empty("Загружаем пост...");
  communityRoot.innerHTML = empty("Загружаем сообщество...");

  try {
    post = await api(`/posts/${postId}`);
    await preloadUsers([post.authorId, ...getComments(post.id).map((comment) => comment.authorId)]);
    community = await api(`/api/communities/${post.communityId}`);
    communityPosts = canViewCommunityPosts()
      ? await api(`/communities/${post.communityId}/posts`)
      : [];

    renderPost();
    renderCommunity();
    bindPostActions();
    bindCommunityActions();
  } catch (error) {
    postRoot.innerHTML = empty(error.message);
    communityRoot.innerHTML = "";
  }
}

function renderPost() {
  const comments = getComments(post.id);

  postRoot.innerHTML = `
    <article class="post-page-card">
      <div class="post-card-meta">
        <span class="community-mark">${communityInitial()}</span>
        ${renderCommunityLink()}
        ${renderAuthorLink(post.authorId)}
        <span>${formatDate(post.createdAt)}</span>
      </div>

      <h1>${escapeHtml(post.title)}</h1>
      <p class="post-detail-text">${escapeHtml(post.text ?? "")}</p>
      ${renderMedia(post.id, post.media)}

      <div class="post-card-footer">
        ${renderVoteControls(post)}
        <a class="comment-pill" href="#comments">${comments.length} комментариев</a>
      </div>
    </article>

    <section class="panel comments-panel" id="comments">
      <div class="row comments-title">
        <h2>Комментарии</h2>
        <span class="badge">${comments.length}</span>
      </div>
      <div class="comment-list">
        ${comments.length ? comments.map(renderComment).join("") : '<p class="muted">Комментариев пока нет.</p>'}
      </div>
      <form class="comment-form" data-form="post-comment" data-post-id="${post.id}">
        <input name="text" maxlength="1000" placeholder="Добавить комментарий" required />
        <button class="button secondary" type="submit">Отправить</button>
      </form>
    </section>`;
}

function renderCommunity() {
  const membership = community?.currentUserMembership;
  const isMember = Boolean(membership);
  const isOwner = membership?.role === "Owner";

  communityRoot.innerHTML = `
    <section class="panel community-about">
      <div class="community-avatar">${communityInitial()}</div>
      <h2>${renderCommunityLink()}</h2>
      <p>${escapeHtml(community?.description || "Описание пока не заполнено.")}</p>
      <div class="community-stats">
        <span>${canViewCommunityPosts() ? `<strong>${communityPosts.length}</strong> постов` : "Посты скрыты"}</span>
        <span><strong>${community?.membersCount ?? 0}</strong> подписчиков</span>
      </div>
      ${renderCommunityAction(isMember, isOwner)}
    </section>`;
}

function renderCommunityAction(isMember, isOwner) {
  if (isOwner) {
    return '<button class="button secondary" type="button" disabled>Вы владелец</button>';
  }

  if (isMember) {
    return `<button class="button secondary" type="button" data-leave-community="${community.id}">Выйти</button>`;
  }

  if (community.type === "Closed") {
    if (community.currentUserJoinRequest?.status === "Pending") {
      return '<button class="button secondary" type="button" disabled>Заявка отправлена</button>';
    }

    return `<button class="button primary" type="button" data-request-join="${community.id}">Подать заявку</button>`;
  }

  return `<button class="button primary" type="button" data-join-community="${community.id}">Вступить</button>`;
}

function renderVoteControls(currentPost) {
  const upActive = currentPost.viewerVote === 1 ? " active" : "";
  const downActive = currentPost.viewerVote === -1 ? " active" : "";
  const upValue = currentPost.viewerVote === 1 ? 0 : 1;
  const downValue = currentPost.viewerVote === -1 ? 0 : -1;

  return `
    <div class="vote-bar" aria-label="Оценка поста">
      <button class="vote-button${upActive}" type="button" data-vote-post="${currentPost.id}" data-vote-value="${upValue}" title="Поднять пост">▲</button>
      <strong>${currentPost.score ?? 0}</strong>
      <button class="vote-button${downActive}" type="button" data-vote-post="${currentPost.id}" data-vote-value="${downValue}" title="Опустить пост">▼</button>
    </div>`;
}

function renderMedia(currentPostId, media = []) {
  if (!media.length) return "";

  return `
    <div class="detail-media-grid">
      ${media.map((item) => renderMediaItem(currentPostId, item)).join("")}
    </div>`;
}

function renderMediaItem(currentPostId, item) {
  const mediaUrl = `/posts/${currentPostId}/media/${item.id}`;
  if ((item.contentType || "").startsWith("image/")) {
    return `<figure class="detail-media-item"><img src="${mediaUrl}" alt="${escapeHtml(item.fileName)}" /></figure>`;
  }

  return `<a class="media-file" href="${mediaUrl}" target="_blank" rel="noreferrer">${escapeHtml(item.fileName)}</a>`;
}

function renderComment(comment) {
  const currentUserId = getSession().user?.id;
  const canMessage = comment.authorId && comment.authorId !== currentUserId;
  const authorName = comment.authorId ? userDisplayName(comment.authorId) : comment.author || "Пользователь";
  const authorHref = comment.authorId ? userProfileHref(comment.authorId) : "";

  return `
    <article class="comment">
      <div class="row">
        ${authorHref
          ? `<a class="comment-author" href="${escapeHtml(authorHref)}"><strong>${escapeHtml(authorName)}</strong></a>`
          : `<strong>${escapeHtml(authorName)}</strong>`}
        ${canMessage ? `<a class="button secondary" href="/Messages?recipientUserId=${comment.authorId}">Написать сообщение</a>` : ""}
      </div>
      <p>${escapeHtml(comment.text)}</p>
      <small>${formatDate(comment.createdAt)}</small>
    </article>`;
}

function bindPostActions() {
  for (const button of postRoot.querySelectorAll("[data-vote-post]")) {
    button.addEventListener("click", () => votePost(button));
  }

  postRoot.querySelector('[data-form="post-comment"]')?.addEventListener("submit", async (event) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = formData(form);
    addComment(form.dataset.postId, data.text);
    await preloadUsers([getSession().user?.id]);
    form.reset();
    toast("Комментарий добавлен.");
    renderPost();
    bindPostActions();
  });
}

function renderCommunityLink() {
  if (!community?.id) {
    return `<strong>${escapeHtml(community?.name ?? "Сообщество")}</strong>`;
  }

  const href = community.username
    ? `/CommunityDetails?username=${encodeURIComponent(community.username)}`
    : `/CommunityDetails?communityId=${community.id}`;
  return `<a class="community-inline-link" href="${escapeHtml(href)}">${escapeHtml(community.name)}</a>`;
}

function renderAuthorLink(userId) {
  const href = userProfileHref(userId);
  const label = `Автор: ${userDisplayName(userId)}`;
  return href
    ? `<a href="${escapeHtml(href)}">${escapeHtml(label)}</a>`
    : `<span>${escapeHtml(label)}</span>`;
}

function bindCommunityActions() {
  communityRoot.querySelector("[data-join-community]")?.addEventListener("click", async (event) => {
    await runWithButton(event.currentTarget, "Вступаем...", async () => {
      await api(`/api/communities/${community.id}/join`, { method: "POST" });
      toast("Вы вступили в сообщество.");
      community = await api(`/api/communities/${post.communityId}`);
      renderCommunity();
      bindCommunityActions();
    });
  });

  communityRoot.querySelector("[data-request-join]")?.addEventListener("click", async (event) => {
    await runWithButton(event.currentTarget, "Отправляем...", async () => {
      await api(`/api/communities/${community.id}/join-requests`, { method: "POST" });
      toast("Заявка отправлена владельцу сообщества.");
      community = await api(`/api/communities/${post.communityId}`);
      renderCommunity();
      bindCommunityActions();
    });
  });

  communityRoot.querySelector("[data-leave-community]")?.addEventListener("click", async (event) => {
    await runWithButton(event.currentTarget, "Выходим...", async () => {
      await api(`/api/communities/${community.id}/membership`, { method: "DELETE" });
      toast("Вы вышли из сообщества.");
      community = await api(`/api/communities/${post.communityId}`);
      renderCommunity();
      bindCommunityActions();
    });
  });
}

async function votePost(button) {
  await runWithButton(button, "...", async () => {
    await api(`/posts/${button.dataset.votePost}/vote`, toJson("POST", {
      value: Number(button.dataset.voteValue)
    }));
    post = await api(`/posts/${post.id}`);
    renderPost();
    bindPostActions();
  });
}

function getComments(currentPostId) {
  return readJson(`socialhub.comments.${currentPostId}`, []);
}

function addComment(currentPostId, text) {
  const session = getSession();
  const comments = getComments(currentPostId);
  comments.push({
    text,
    authorId: session.user?.id,
    author: session.user?.profile?.displayName || session.user?.username || "Пользователь",
    createdAt: new Date().toISOString()
  });
  localStorage.setItem(`socialhub.comments.${currentPostId}`, JSON.stringify(comments));
}

function readJson(key, fallback) {
  try {
    return JSON.parse(localStorage.getItem(key) || JSON.stringify(fallback));
  } catch {
    return fallback;
  }
}

function communityInitial() {
  return (community?.name ?? "C").trim().slice(0, 1).toUpperCase() || "C";
}

function canViewCommunityPosts() {
  return community?.type !== "Closed" || Boolean(community?.currentUserMembership);
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
