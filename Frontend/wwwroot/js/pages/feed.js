import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formatDate, formData, shortId } from "../core/dom.js";
import { getSession } from "../core/session.js";
import { toast } from "../core/toast.js";

const list = document.querySelector("[data-feed-list]");
const detail = document.querySelector("[data-post-detail]");
const feedItems = new Map();
let selectedPostId = "";

document.querySelector("[data-refresh-feed]")?.addEventListener("click", async (event) => {
  await runWithButton(event.currentTarget, "Обновляем...", async () => {
    await api("/feed/refresh", toJson("POST", {}));
    toast("Лента синхронизирована");
    await loadFeed();
  });
});

loadFeed();

async function loadFeed() {
  list.innerHTML = empty("Загружаем ленту...");
  try {
    const response = await api("/feed?page=1&limit=20");
    const items = response.items ?? response.posts ?? [];
    feedItems.clear();
    for (const item of items) {
      feedItems.set(getPostId(item), item);
    }

    list.innerHTML = items.length ? items.map(renderFeedCard).join("") : empty("Подпишитесь на сообщества, чтобы увидеть ленту.");
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
        <span class="badge">score ${post.score ?? post.likes ?? 0}</span>
      </div>
      <p>${escapeHtml(post.previewText ?? post.text ?? "")}</p>
      <div class="meta">
        <span>Пост ${shortId(postId)}</span>
        <span>Сообщество ${shortId(post.communityId)}</span>
        <span>${formatDate(post.createdAt ?? post.createdAtUtc)}</span>
      </div>
      <div class="actions">
        <button class="button secondary" data-open-post="${postId}">Открыть</button>
        <button class="button danger" data-report-post="${postId}">Пожаловаться</button>
      </div>
    </article>`;
}

function bindFeedActions() {
  for (const button of document.querySelectorAll("[data-open-post]")) {
    button.addEventListener("click", () => openPost(button.dataset.openPost));
  }

  for (const button of document.querySelectorAll("[data-report-post]")) {
    button.addEventListener("click", async () => {
      await reportPost(button, button.dataset.reportPost);
    });
  }
}

async function openPost(postId) {
  selectedPostId = postId;
  detail.innerHTML = empty("Загружаем пост...");
  markActiveCard(postId);

  try {
    const post = await api(`/posts/${postId}`);
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
        <span class="badge">${escapeHtml(post.status ?? "Published")}</span>
      </div>
      <p class="post-detail-text">${escapeHtml(post.text ?? post.previewText ?? "")}</p>
      <div class="meta">
        <span>Пост ${shortId(postId)}</span>
        <span>Сообщество ${shortId(post.communityId)}</span>
        <span>Автор ${shortId(post.authorId)}</span>
        <span>${formatDate(post.createdAt ?? post.createdAtUtc)}</span>
      </div>
      <div class="actions">
        <button class="button danger" data-detail-report="${postId}">Пожаловаться</button>
      </div>
      <section class="comments">
        <div class="row comments-title">
          <strong>Комментарии</strong>
          <span class="badge">${comments.length}</span>
        </div>
        <div class="comment-list">
          ${comments.length ? comments.map(renderComment).join("") : '<p class="muted">Комментариев пока нет.</p>'}
        </div>
        <form class="comment-form" data-form="feed-comment" data-post-id="${postId}">
          <input name="text" placeholder="Добавить комментарий" required />
          <button class="button secondary" type="submit">Отправить</button>
        </form>
      </section>
    </article>`;
}

function bindDetailActions() {
  detail.querySelector("[data-detail-report]")?.addEventListener("click", async (event) => {
    await reportPost(event.currentTarget, event.currentTarget.dataset.detailReport);
  });

  detail.querySelector('[data-form="feed-comment"]')?.addEventListener("submit", async (event) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = formData(form);
    addComment(form.dataset.postId, data.text);
    form.reset();
    toast("Комментарий добавлен");
    await openPost(form.dataset.postId);
  });
}

async function reportPost(button, postId) {
  await runWithButton(button, "Отправляем...", async () => {
    await api("/api/reports", toJson("POST", {
      targetType: "POST",
      targetId: postId,
      reason: "Спам",
      comment: "Жалоба из ленты"
    }));
    toast("Жалоба отправлена");
  });
}

function renderComment(comment) {
  return `
    <article class="comment">
      <strong>${escapeHtml(comment.author)}</strong>
      <p>${escapeHtml(comment.text)}</p>
      <small>${formatDate(comment.createdAt)}</small>
    </article>`;
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
