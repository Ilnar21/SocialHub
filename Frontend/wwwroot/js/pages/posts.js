import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formData, formatDate, shortId } from "../core/dom.js";
import { getSession, isPlatformModerator } from "../core/session.js";
import { toast } from "../core/toast.js";

const communitySelect = document.querySelector("[data-community-select]");
const submitPostButton = document.querySelector("[data-submit-post]");
const formHint = document.querySelector("[data-post-form-hint]");
const list = document.querySelector("[data-posts-list]");

let communities = [];

document.querySelector("[data-load-posts]")?.addEventListener("click", loadPosts);
communitySelect?.addEventListener("change", loadPosts);

document.querySelector('[data-form="create-post"]')?.addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  const button = form.querySelector('button[type="submit"]');
  const data = formData(form);
  const selectedCommunityId = data.communityId;

  if (!selectedCommunityId) {
    toast("Сначала вступите в сообщество.", "error");
    return;
  }

  await runWithButton(button, "Публикуем...", async () => {
    await api("/posts", toJson("POST", {
      communityId: selectedCommunityId,
      title: data.title,
      text: data.text
    }));

    form.reset();
    communitySelect.value = selectedCommunityId;
    toast("Пост опубликован.");
    await loadPosts();
  });
});

await loadCommunities();
await loadPosts();

async function loadCommunities() {
  try {
    communities = await api("/api/communities/my");
    const preset = new URLSearchParams(location.search).get("communityId");

    if (!communities.length) {
      communitySelect.innerHTML = '<option value="">Нет сообществ для публикации</option>';
      communitySelect.disabled = true;
      submitPostButton.disabled = true;
      formHint.textContent = "Вступите в сообщество на странице «Сообщества», чтобы создать пост.";
      return;
    }

    communitySelect.disabled = false;
    submitPostButton.disabled = false;
    formHint.textContent = "";
    communitySelect.innerHTML = communities.map((community) => (
      `<option value="${community.id}">${escapeHtml(community.name)}</option>`
    )).join("");

    if (preset && communities.some((community) => community.id === preset)) {
      communitySelect.value = preset;
    }
  } catch (error) {
    communitySelect.innerHTML = `<option value="">${escapeHtml(error.message)}</option>`;
    communitySelect.disabled = true;
    submitPostButton.disabled = true;
    formHint.textContent = "Не удалось загрузить сообщества пользователя.";
  }
}

async function loadPosts() {
  const communityId = communitySelect.value;
  if (!communityId) {
    list.innerHTML = empty("Подпишитесь на сообщество, чтобы видеть и создавать посты.");
    return;
  }

  list.innerHTML = empty("Загружаем посты...");
  try {
    const posts = await api(`/communities/${communityId}/posts`);
    list.innerHTML = posts.length
      ? posts.map(renderPost).join("")
      : empty("В этом сообществе пока нет постов. Опубликуйте первый.");
    bindPostActions();
  } catch (error) {
    list.innerHTML = empty(error.message);
  }
}

function renderPost(post) {
  const session = getSession();
  const comments = getComments(post.id);
  const isAuthor = post.authorId === session.user?.id;
  const canModerate = isPlatformModerator();
  const community = communities.find((item) => item.id === post.communityId);

  return `
    <article class="card post-card" data-post-card="${post.id}">
      <div class="row">
        <h2>${escapeHtml(post.title)}</h2>
        <span class="badge success">${escapeHtml(post.status ?? "Published")}</span>
      </div>
      <p>${escapeHtml(post.text ?? "")}</p>
      <div class="meta">
        <span>ID ${shortId(post.id)}</span>
        <span>Сообщество: ${escapeHtml(community?.name ?? shortId(post.communityId))}</span>
        <span>Автор ${shortId(post.authorId)}</span>
        <span>${formatDate(post.createdAt)}</span>
        ${post.updatedAt ? `<span>Изменен: ${formatDate(post.updatedAt)}</span>` : ""}
      </div>
      ${renderMedia(post.media)}
      ${isAuthor ? renderEditForm(post) : ""}
      <section class="comments">
        <div class="row comments-title">
          <strong>Комментарии</strong>
          <span class="badge">${comments.length}</span>
        </div>
        <div class="comment-list">
          ${comments.length ? comments.map(renderComment).join("") : '<p class="muted">Комментариев пока нет.</p>'}
        </div>
        <form class="comment-form" data-form="add-comment" data-post-id="${post.id}">
          <input name="text" maxlength="1000" placeholder="Добавить комментарий" required />
          <button class="button secondary" type="submit">Отправить</button>
        </form>
      </section>
      <div class="actions">
        ${isAuthor ? `<button class="button danger" data-delete-own-post="${post.id}">Удалить свой пост</button>` : ""}
        ${canModerate ? `<button class="button danger" data-delete-post="${post.id}">Удалить как модератор</button>` : ""}
      </div>
    </article>`;
}

function renderMedia(media = []) {
  if (!media.length) return "";

  return `
    <div class="meta">
      ${media.map((item) => `<span>Медиа: ${escapeHtml(item.fileName)} · ${Math.ceil((item.size ?? 0) / 1024)} KB</span>`).join("")}
    </div>`;
}

function renderEditForm(post) {
  return `
    <form class="panel form-grid" data-form="edit-post" data-post-id="${post.id}">
      <h2>Редактировать пост</h2>
      <label>Заголовок
        <input name="title" maxlength="120" value="${escapeHtml(post.title)}" required />
      </label>
      <label>Текст
        <textarea name="text" maxlength="10000" required>${escapeHtml(post.text ?? "")}</textarea>
      </label>
      <button class="button secondary" type="submit">Сохранить изменения</button>
    </form>`;
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
      toast("Комментарий добавлен.");
      await loadPosts();
    });
  }

  for (const form of document.querySelectorAll('[data-form="edit-post"]')) {
    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      const button = form.querySelector('button[type="submit"]');
      const data = formData(form);

      await runWithButton(button, "Сохраняем...", async () => {
        await api(`/posts/${form.dataset.postId}`, toJson("PUT", {
          title: data.title,
          text: data.text
        }));
        toast("Пост обновлен.");
        await loadPosts();
      });
    });
  }

  for (const button of document.querySelectorAll("[data-delete-own-post]")) {
    button.addEventListener("click", async () => {
      if (!confirm("Удалить свой пост?")) return;
      await runWithButton(button, "Удаляем...", async () => {
        await api(`/posts/${button.dataset.deleteOwnPost}`, toJson("DELETE", {}));
        toast("Пост удален.");
        await loadPosts();
      });
    });
  }

  for (const button of document.querySelectorAll("[data-delete-post]")) {
    button.addEventListener("click", async () => {
      if (!confirm("Удалить пост как модератор платформы?")) return;
      await runWithButton(button, "Удаляем...", async () => {
        await api(`/api/posts/${button.dataset.deletePost}/moderation-delete`, toJson("POST", { reason: "Удалено модератором через frontend" }));
        toast("Пост удален модератором.");
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
  commentsFor(postId).push({
    text,
    author: session.user?.profile?.displayName || session.user?.username || "Пользователь",
    createdAt: new Date().toISOString()
  });
}

function commentsFor(postId) {
  const key = `socialhub.comments.${postId}`;
  const comments = getComments(postId);
  localStorage.setItem(key, JSON.stringify(comments));
  return {
    push(comment) {
      comments.push(comment);
      localStorage.setItem(key, JSON.stringify(comments));
    }
  };
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
