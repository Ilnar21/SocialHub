import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formatDate } from "../core/dom.js";
import { preloadUsers, userDisplayName, userProfileHref } from "../core/identity.js";
import { toast } from "../core/toast.js";

const list = document.querySelector("[data-feed-list]");
const communityNames = new Map();

document.querySelector("[data-refresh-feed]")?.addEventListener("click", async (event) => {
  await runWithButton(event.currentTarget, "Обновляем...", async () => {
    await api("/feed/refresh", toJson("POST", {}));
    toast("Лента синхронизирована.");
    await loadFeed();
  });
});

loadFeed();

async function loadFeed() {
  list.innerHTML = empty("Загружаем ленту...");

  try {
    const [response] = await Promise.all([
      api("/feed?page=1&limit=20"),
      loadCommunityNames()
    ]);
    const items = response.items ?? response.posts ?? [];
    const posts = await Promise.all(items.map(loadPostDetails));

    await preloadUsers(posts.map((post) => post.authorId));

    list.innerHTML = posts.length
      ? posts.map(renderFeedPost).join("")
      : empty("Подпишитесь на сообщества, чтобы увидеть ленту.");
    bindFeedActions();
  } catch (error) {
    list.innerHTML = empty(error.message);
  }
}

async function loadPostDetails(item) {
  const postId = getPostId(item);
  try {
    return await api(`/posts/${postId}`);
  } catch {
    return {
      id: postId,
      authorId: item.authorId,
      communityId: item.communityId,
      title: item.title,
      text: item.previewText ?? item.text ?? "",
      createdAt: item.createdAt ?? item.createdAtUtc,
      score: item.likes ?? 0,
      media: []
    };
  }
}

function renderFeedPost(post) {
  const postId = getPostId(post);
  const commentsCount = getComments(postId).length;

  return `
    <article class="card feed-post-card" data-feed-post="${postId}" data-open-post="/PostDetails?postId=${postId}">
      <div class="post-card-link">
        <div class="post-card-meta">
          <span class="community-mark">${communityInitial(post.communityId)}</span>
          ${renderCommunityLink(post.communityId)}
          ${renderAuthorLink(post.authorId)}
          <span>${formatDate(post.createdAt ?? post.createdAtUtc)}</span>
        </div>
        <h2><a class="post-card-title" href="/PostDetails?postId=${postId}">${escapeHtml(post.title)}</a></h2>
        <p class="feed-post-text">${escapeHtml(post.text ?? post.previewText ?? "")}</p>
        ${renderMedia(postId, post.media)}
      </div>

      <div class="post-card-footer">
        ${renderVoteControls(post)}
        <a class="comment-pill" href="/PostDetails?postId=${postId}#comments">${commentsCount} комментариев</a>
      </div>
    </article>`;
}

function renderVoteControls(post) {
  const postId = getPostId(post);
  const upActive = post.viewerVote === 1 ? " active" : "";
  const downActive = post.viewerVote === -1 ? " active" : "";
  const upValue = post.viewerVote === 1 ? 0 : 1;
  const downValue = post.viewerVote === -1 ? 0 : -1;

  return `
    <div class="vote-bar" aria-label="Оценка поста">
      <button class="vote-button${upActive}" type="button" data-vote-post="${postId}" data-vote-value="${upValue}" title="Поднять пост">▲</button>
      <strong>${post.score ?? 0}</strong>
      <button class="vote-button${downActive}" type="button" data-vote-post="${postId}" data-vote-value="${downValue}" title="Опустить пост">▼</button>
    </div>`;
}

function bindFeedActions() {
  for (const button of document.querySelectorAll("[data-vote-post]")) {
    button.addEventListener("click", (event) => {
      event.stopPropagation();
      votePost(button);
    });
  }

  for (const card of document.querySelectorAll("[data-open-post]")) {
    card.addEventListener("click", (event) => {
      if (event.target.closest("a, button, input, textarea, select, label")) return;
      location.href = card.dataset.openPost;
    });
  }
}

async function votePost(button) {
  const scrollY = window.scrollY;
  await runWithButton(button, "...", async () => {
    await api(`/posts/${button.dataset.votePost}/vote`, toJson("POST", {
      value: Number(button.dataset.voteValue)
    }));
    await api("/feed/refresh", toJson("POST", {}));
    await loadFeed();
    requestAnimationFrame(() => window.scrollTo(0, scrollY));
  });
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

async function loadCommunityNames() {
  if (communityNames.size) return;
  const communities = await api("/api/communities");
  for (const community of communities) {
    communityNames.set(community.id, community.name);
  }
}

function communityName(communityId) {
  return communityNames.get(communityId) || "Сообщество";
}

function communityInitial(communityId) {
  return communityName(communityId).trim().slice(0, 1).toUpperCase() || "C";
}

function renderCommunityLink(communityId) {
  return `<a class="community-inline-link" href="/CommunityDetails?communityId=${communityId}">${escapeHtml(communityName(communityId))}</a>`;
}

function renderAuthorLink(userId) {
  const href = userProfileHref(userId);
  const label = `Автор: ${userDisplayName(userId)}`;
  return href
    ? `<a href="${escapeHtml(href)}">${escapeHtml(label)}</a>`
    : `<span>${escapeHtml(label)}</span>`;
}

function getPostId(post) {
  return post.postId ?? post.id;
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
