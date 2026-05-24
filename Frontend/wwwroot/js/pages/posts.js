import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formData, formatDate, shortId } from "../core/dom.js";
import { getSession, isPlatformModerator } from "../core/session.js";
import { toast } from "../core/toast.js";

const communitySelect = document.querySelector("[data-community-select]");
const list = document.querySelector("[data-posts-list]");

document.querySelector("[data-load-posts]")?.addEventListener("click", loadPosts);
communitySelect?.addEventListener("change", loadPosts);

document.querySelector('[data-form="create-post"]')?.addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  const button = form.querySelector('button[type="submit"]');
  const data = formData(form);
  const selectedCommunityId = data.communityId;

  await runWithButton(button, "Публикуем...", async () => {
    await api("/posts", toJson("POST", {
      communityId: selectedCommunityId,
      title: data.title,
      text: data.text
    }));

    form.reset();
    communitySelect.value = selectedCommunityId;
    toast("Пост опубликован");
    await loadPosts();
  });
});

await loadCommunities();
await loadPosts();

async function loadCommunities() {
  try {
    const communities = await api("/api/communities/my");
    communitySelect.innerHTML = communities.map((community) => (
      `<option value="${community.id}">${escapeHtml(community.name)}</option>`
    )).join("");

    const preset = new URLSearchParams(location.search).get("communityId");
    if (preset) communitySelect.value = preset;
  } catch (error) {
    communitySelect.innerHTML = `<option value="">${escapeHtml(error.message)}</option>`;
  }
}

async function loadPosts() {
  const communityId = communitySelect.value;
  if (!communityId) {
    list.innerHTML = empty("Выберите сообщество для просмотра постов.");
    return;
  }

  list.innerHTML = empty("Загружаем посты...");
  try {
    const posts = await api(`/communities/${communityId}/posts`);
    const visiblePosts = posts.filter((post) => !isPostHidden(post.id));
    list.innerHTML = visiblePosts.length ? visiblePosts.map(renderPost).join("") : empty("В этом сообществе пока нет постов.");
    bindPostActions();
  } catch (error) {
    list.innerHTML = empty(error.message);
  }
}

function renderPost(post) {
  const comments = getComments(post.id);
  const moderationActions = isPlatformModerator()
    ? `<button class="button danger" data-delete-post="${post.id}">Удалить</button>`
    : "";

  return `
    <article class="card post-card" data-post-card="${post.id}">
      <div class="row">
        <h2>${escapeHtml(post.title)}</h2>
        <span class="badge">${escapeHtml(post.status ?? "Published")}</span>
      </div>
      <p>${escapeHtml(post.text ?? "")}</p>
      <div class="meta">
        <span>ID ${shortId(post.id)}</span>
        <span>Автор ${shortId(post.authorId)}</span>
        <span>${formatDate(post.createdAt)}</span>
      </div>
      <section class="comments">
        <div class="row comments-title">
          <strong>Комментарии</strong>
          <span class="badge">${comments.length}</span>
        </div>
        <div class="comment-list">
          ${comments.length ? comments.map(renderComment).join("") : '<p class="muted">Комментариев пока нет.</p>'}
        </div>
        <form class="comment-form" data-form="add-comment" data-post-id="${post.id}">
          <input name="text" placeholder="Добавить комментарий" required />
          <button class="button secondary" type="submit">Отправить</button>
        </form>
      </section>
      <div class="actions">
        ${moderationActions}
      </div>
    </article>`;
}

function renderComment(comment) {
  return `
    <article class="comment">
      <strong>${escapeHtml(comment.author)}</strong>
      <p>${escapeHtml(comment.text)}</p>
      <small>${formatDate(comment.createdAt)}</small>
    </article>`;
}

function bindPostActions() {
  for (const form of document.querySelectorAll('[data-form="add-comment"]')) {
    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      const data = formData(form);
      const postId = form.dataset.postId;
      addComment(postId, data.text);
      form.reset();
      toast("Комментарий добавлен");
      await loadPosts();
    });
  }

  for (const button of document.querySelectorAll("[data-delete-post]")) {
    button.addEventListener("click", async () => {
      await runWithButton(button, "Удаляем...", async () => {
        const postId = button.dataset.deletePost;
        await api(`/api/posts/${postId}/moderation-delete`, toJson("POST", { reason: "Удалено модератором через frontend" }));
        hidePost(postId);
        toast("Пост скрыт из списка");
        await loadPosts();
      });
    });
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

function isPostHidden(postId) {
  return readJson("socialhub.hiddenPosts", []).includes(postId);
}

function hidePost(postId) {
  const hidden = readJson("socialhub.hiddenPosts", []);
  if (!hidden.includes(postId)) {
    hidden.push(postId);
    localStorage.setItem("socialhub.hiddenPosts", JSON.stringify(hidden));
  }
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
