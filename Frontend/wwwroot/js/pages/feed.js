import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formatDate, shortId } from "../core/dom.js";
import { preloadUsers, userDisplayName } from "../core/identity.js";
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
    <article class="card feed-post-card" data-feed-post="${postId}">
      <a class="post-card-link" href="/PostDetails?postId=${postId}" aria-label="Открыть пост ${escapeHtml(post.title)}">
        <div class="post-card-meta">
          <span class="community-mark">${communityInitial(post.communityId)}</span>
          <strong>${escapeHtml(communityName(post.communityId))}</strong>
          <span>Автор: ${escapeHtml(userDisplayName(post.authorId))}</span>
          <span>${formatDate(post.createdAt ?? post.createdAtUtc)}</span>
        </div>
        <h2>${escapeHtml(post.title)}</h2>
        <p class="feed-post-text">${escapeHtml(post.text ?? post.previewText ?? "")}</p>
        ${renderMedia(postId, post.media)}
      </a>

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
    button.addEventListener("click", () => votePost(button));
  }
}

async function votePost(button) {
  await runWithButton(button, "...", async () => {
    await api(`/posts/${button.dataset.votePost}/vote`, toJson("POST", {
      value: Number(button.dataset.voteValue)
    }));
    await api("/feed/refresh", toJson("POST", {}));
    await loadFeed();
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
  return communityNames.get(communityId) || shortId(communityId);
}

function communityInitial(communityId) {
  return communityName(communityId).trim().slice(0, 1).toUpperCase() || "C";
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
