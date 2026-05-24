import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formatDate, formData, shortId } from "../core/dom.js";
import { preloadUsers, userDisplayName } from "../core/identity.js";
import { getSession } from "../core/session.js";
import { toast } from "../core/toast.js";

const list = document.querySelector("[data-feed-list]");
const detail = document.querySelector("[data-post-detail]");
const feedItems = new Map();
const communityNames = new Map();
let selectedPostId = "";

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
    await preloadUsers(items.map((item) => item.authorId));
    feedItems.clear();
    for (const item of items) {
      feedItems.set(getPostId(item), item);
    }

    list.innerHTML = items.length
      ? items.map(renderFeedCard).join("")
      : empty("Подпишитесь на сообщества, чтобы увидеть ленту.");
    bindFeedActions();

    if (selectedPostId && feedItems.has(selectedPostId)) {
      await openPost(selectedPostId);
    }
  } catch (error) {
    list.innerHTML = empty(error.message);
  }
}

function renderFeedCard(post) {
  const postId = getPostId(post);
  const active = postId === selectedPostId ? " active-card" : "";

  return `
    <article class="card feed-card${active}">
      <div class="row">
        <button class="link-title" type="button" data-open-post="${postId}">${escapeHtml(post.title)}</button>
        <span class="badge">score ${displayVoteScore(post)}</span>
      </div>
      ${renderFeedVoteControls(post)}
      <p>${escapeHtml(post.previewText ?? post.text ?? "")}</p>
      <div class="meta">
        <span>Пост ${shortId(postId)}</span>
        <span>Сообщество: ${escapeHtml(communityName(post.communityId))}</span>
        <span>Автор: ${escapeHtml(userDisplayName(post.authorId))}</span>
        <span>${formatDate(post.createdAt ?? post.createdAtUtc)}</span>
      </div>
      <div class="actions">
        <button class="button secondary" data-open-post="${postId}">Открыть</button>
        <button class="button danger" data-report-card="${postId}">Пожаловаться</button>
      </div>
    </article>`;
}

function bindFeedActions() {
  for (const button of document.querySelectorAll("[data-open-post]")) {
    button.addEventListener("click", () => openPost(button.dataset.openPost));
  }

  for (const button of document.querySelectorAll("[data-report-card]")) {
    button.addEventListener("click", async () => {
      await openPost(button.dataset.reportCard);
      detail.querySelector("[name='reason']")?.focus();
    });
  }

  for (const button of document.querySelectorAll("[data-vote-post]")) {
    button.addEventListener("click", () => votePost(button));
  }
}

async function openPost(postId) {
  selectedPostId = postId;
  detail.innerHTML = empty("Загружаем пост...");
  markActiveCard(postId);

  try {
    const post = await api(`/posts/${postId}`);
    await preloadUsers([post.authorId]);
    detail.innerHTML = renderPostDetail(post);
  } catch {
    const snapshot = feedItems.get(postId);
    detail.innerHTML = renderPostDetail({
      id: postId,
      title: snapshot?.title ?? "Пост",
      text: snapshot?.previewText ?? snapshot?.text ?? "",
      authorId: snapshot?.authorId,
      communityId: snapshot?.communityId,
      createdAt: snapshot?.createdAt ?? snapshot?.createdAtUtc,
      status: "Published"
    });
  }

  bindDetailActions();
}

function renderPostDetail(post) {
  const postId = getPostId(post);
  const comments = getComments(postId);

  return `
    <article class="post-detail">
      <div class="row">
        <h2>${escapeHtml(post.title)}</h2>
        <span class="badge success">${escapeHtml(post.status ?? "Published")}</span>
      </div>
      ${renderPostVoteControls(post)}
      <p class="post-detail-text">${escapeHtml(post.text ?? post.previewText ?? "")}</p>
      <div class="meta">
        <span>Пост ${shortId(postId)}</span>
        <span>Сообщество: ${escapeHtml(communityName(post.communityId))}</span>
        <span>Автор: ${escapeHtml(userDisplayName(post.authorId))}</span>
        <span>${formatDate(post.createdAt ?? post.createdAtUtc)}</span>
      </div>
      ${renderMedia(postId, post.media)}

      <section class="comments">
        <div class="row comments-title">
          <strong>Комментарии</strong>
          <span class="badge">${comments.length}</span>
        </div>
        <div class="comment-list">
          ${comments.length ? comments.map(renderComment).join("") : '<p class="muted">Комментариев пока нет.</p>'}
        </div>
        <form class="comment-form" data-form="feed-comment" data-post-id="${postId}">
          <input name="text" maxlength="1000" placeholder="Добавить комментарий" required />
          <button class="button secondary" type="submit">Отправить</button>
        </form>
      </section>

      <form class="report-panel form-grid" data-form="report-post" data-post-id="${postId}">
        <h3>Пожаловаться</h3>
        <div class="report-fields">
          <label>Причина
            <select name="reason" required>
              <option value="Спам">Спам</option>
              <option value="Оскорбления">Оскорбления</option>
              <option value="Нарушение правил">Нарушение правил</option>
              <option value="Другое">Другое</option>
            </select>
          </label>
          <label>Комментарий
            <textarea name="comment" rows="2" placeholder="Кратко опишите проблему"></textarea>
          </label>
        </div>
        <button class="button danger" type="submit">Отправить жалобу</button>
      </form>
    </article>`;
}

function bindDetailActions() {
  for (const button of detail.querySelectorAll("[data-vote-post]")) {
    button.addEventListener("click", () => votePost(button));
  }

  detail.querySelector('[data-form="report-post"]')?.addEventListener("submit", async (event) => {
    event.preventDefault();
    const form = event.currentTarget;
    const button = form.querySelector('button[type="submit"]');
    const data = formData(form);

    await runWithButton(button, "Отправляем...", async () => {
      await api("/api/reports", toJson("POST", {
        targetType: "POST",
        targetId: form.dataset.postId,
        reason: data.reason,
        comment: data.comment
      }));
      form.reset();
      toast("Жалоба отправлена.");
    });
  });

  detail.querySelector('[data-form="feed-comment"]')?.addEventListener("submit", async (event) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = formData(form);
    addComment(form.dataset.postId, data.text);
    form.reset();
    toast("Комментарий добавлен.");
    await openPost(form.dataset.postId);
  });
}

function renderComment(comment) {
  const currentUserId = getSession().user?.id;
  const canMessage = comment.authorId && comment.authorId !== currentUserId;

  return `
    <article class="comment">
      <div class="row">
        <strong>${escapeHtml(comment.author)}</strong>
        ${canMessage ? `<a class="button secondary" href="/Messages?recipientUserId=${comment.authorId}">Написать сообщение</a>` : ""}
      </div>
      <p>${escapeHtml(comment.text)}</p>
      <small>${formatDate(comment.createdAt)}</small>
    </article>`;
}

function renderFeedVoteControls(post) {
  const postId = getPostId(post);
  return `
    <div class="vote-bar compact" aria-label="Оценка поста">
      <button class="vote-button" type="button" data-vote-post="${postId}" data-vote-value="1" title="Поднять пост">▲</button>
      <strong>${displayVoteScore(post)}</strong>
      <button class="vote-button" type="button" data-vote-post="${postId}" data-vote-value="-1" title="Опустить пост">▼</button>
    </div>`;
}

function renderPostVoteControls(post) {
  const postId = getPostId(post);
  const upActive = post.viewerVote === 1 ? " active" : "";
  const downActive = post.viewerVote === -1 ? " active" : "";
  const upValue = post.viewerVote === 1 ? 0 : 1;
  const downValue = post.viewerVote === -1 ? 0 : -1;

  return `
    <div class="vote-bar" aria-label="Оценка поста">
      <button class="vote-button${upActive}" type="button" data-vote-post="${postId}" data-vote-value="${upValue}" title="Поднять пост">▲</button>
      <strong>${displayVoteScore(post)}</strong>
      <button class="vote-button${downActive}" type="button" data-vote-post="${postId}" data-vote-value="${downValue}" title="Опустить пост">▼</button>
      <span>${post.upvotes ?? 0} за · ${post.downvotes ?? 0} против</span>
    </div>`;
}

async function votePost(button) {
  await runWithButton(button, "...", async () => {
    await api(`/posts/${button.dataset.votePost}/vote`, toJson("POST", {
      value: Number(button.dataset.voteValue)
    }));

    if (selectedPostId === button.dataset.votePost) {
      await openPost(selectedPostId);
    }

    await loadFeed();
  });
}

function renderMedia(postId, media = []) {
  if (!media.length) return "";

  return `
    <div class="media-grid">
      ${media.map((item) => renderMediaItem(postId, item)).join("")}
    </div>`;
}

function renderMediaItem(postId, item) {
  const mediaUrl = `/posts/${postId}/media/${item.id}`;
  if ((item.contentType || "").startsWith("image/")) {
    return `<figure class="media-item"><img src="${mediaUrl}" alt="${escapeHtml(item.fileName)}" loading="lazy" /></figure>`;
  }

  return `
    <a class="media-file" href="${mediaUrl}" target="_blank" rel="noreferrer">
      ${escapeHtml(item.fileName)} · ${Math.ceil((item.size ?? 0) / 1024)} KB
    </a>`;
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

function displayVoteScore(post) {
  return post.postId ? post.likes ?? 0 : post.score ?? 0;
}

function getPostId(post) {
  return post.postId ?? post.id;
}

function markActiveCard(postId) {
  for (const card of document.querySelectorAll(".feed-card")) {
    card.classList.toggle("active-card", card.querySelector(`[data-open-post="${postId}"]`) !== null);
  }
}

function getComments(postId) {
  return readJson(`socialhub.comments.${postId}`, []);
}

function addComment(postId, text) {
  const session = getSession();
  const comments = getComments(postId);
  comments.push({
    text,
    authorId: session.user?.id,
    author: session.user?.profile?.displayName || session.user?.username || "Пользователь",
    createdAt: new Date().toISOString()
  });
  localStorage.setItem(`socialhub.comments.${postId}`, JSON.stringify(comments));
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
