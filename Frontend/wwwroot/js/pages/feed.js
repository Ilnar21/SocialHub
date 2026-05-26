import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formatDate } from "../core/dom.js";
import { preloadUsers, userDisplayName, userProfileHref } from "../core/identity.js";
import { bindReportButtons } from "../core/reports.js";
import { toast } from "../core/toast.js";

const list = document.querySelector("[data-feed-list]");
const filtersForm = document.querySelector("[data-feed-filters]");
const sortSelect = document.querySelector("[data-feed-sort]");
const periodSelect = document.querySelector("[data-feed-period]");
const statusLabel = document.querySelector("[data-feed-status]");
const communitiesById = new Map();
const feedPreferencesKey = "socialhub.feed.filters";

const sortLabels = {
  popular: "популярности",
  newest: "новизне",
  discussed: "обсуждаемости"
};

const periodLabels = {
  all: "за все время",
  day: "за день",
  week: "за неделю",
  month: "за месяц",
  year: "за год"
};

restoreFilters();

filtersForm?.addEventListener("submit", (event) => event.preventDefault());

for (const control of [sortSelect, periodSelect]) {
  control?.addEventListener("change", async () => {
    saveFilters();
    await loadFeed();
  });
}

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

  updateStatus("Загружаем...");

  try {
    const filters = currentFilters();
    const [response] = await Promise.all([
      api(`/feed?${buildFeedQuery(filters)}`),
      loadCommunityNames()
    ]);
    const items = response.items ?? response.posts ?? [];
    const posts = await Promise.all(items.map(loadPostDetails));

    await preloadUsers(posts.map((post) => post.authorId));

    list.innerHTML = posts.length
      ? posts.map(renderFeedPost).join("")
      : empty(emptyFeedMessage(response.total ?? 0, filters));
    updateStatus(statusText(response.total ?? posts.length, filters));
    bindFeedActions();
  } catch (error) {
    list.innerHTML = empty(error.message);
    updateStatus("Не удалось загрузить");
  }
}

function currentFilters() {
  return {
    sort: sortSelect?.value || "popular",
    period: periodSelect?.value || "all"
  };
}

function buildFeedQuery(filters) {
  return new URLSearchParams({
    page: "1",
    limit: "20",
    sort: filters.sort,
    period: filters.period
  }).toString();
}

function restoreFilters() {
  const saved = readJson(feedPreferencesKey, {});
  if (sortSelect && saved.sort && sortLabels[saved.sort]) {
    sortSelect.value = saved.sort;
  }

  if (periodSelect && saved.period && periodLabels[saved.period]) {
    periodSelect.value = saved.period;
  }
}

function saveFilters() {
  localStorage.setItem(feedPreferencesKey, JSON.stringify(currentFilters()));
}

function statusText(total, filters) {
  const countText = total === 1 ? "1 пост" : `${total} постов`;
  return `${countText} · по ${sortLabels[filters.sort] ?? sortLabels.popular} · ${periodLabels[filters.period] ?? periodLabels.all}`;
}

function emptyFeedMessage(total, filters) {
  if (total === 0 && filters.period !== "all") {
    return "За выбранный период постов нет. Попробуйте увеличить промежуток.";
  }

  return "Подпишитесь на сообщества, чтобы увидеть ленту.";
}

function updateStatus(text) {
  if (statusLabel) {
    statusLabel.textContent = text;
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
        <button class="button secondary" type="button"
                data-report-target-type="POST"
                data-report-target-id="${postId}"
                data-report-target-label="Пост: ${escapeHtml(post.title)}">
          Пожаловаться
        </button>
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

  bindReportButtons(list);

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
  if (communitiesById.size) return;
  const communities = await api("/api/communities");
  for (const community of communities) {
    communitiesById.set(community.id, community);
  }
}

function communityName(communityId) {
  return communitiesById.get(communityId)?.name || "Сообщество";
}

function communityInitial(communityId) {
  return communityName(communityId).trim().slice(0, 1).toUpperCase() || "C";
}

function renderCommunityLink(communityId) {
  const community = communitiesById.get(communityId);
  const href = community?.username
    ? `/CommunityDetails?username=${encodeURIComponent(community.username)}`
    : `/CommunityDetails?communityId=${communityId}`;
  return `<a class="community-inline-link" href="${escapeHtml(href)}">${escapeHtml(communityName(communityId))}</a>`;
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
