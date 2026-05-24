import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formData, formatDate, shortId } from "../core/dom.js";
import { preloadUsers, userDisplayName } from "../core/identity.js";
import { getSession, isPlatformModerator } from "../core/session.js";
import { toast } from "../core/toast.js";

const communitySelect = document.querySelector("[data-community-select]");
const submitPostButton = document.querySelector("[data-submit-post]");
const formHint = document.querySelector("[data-post-form-hint]");
const list = document.querySelector("[data-posts-list]");

let communities = [];
let allMyCommunities = [];

document.querySelector("[data-load-posts]")?.addEventListener("click", loadPosts);
communitySelect?.addEventListener("change", loadPosts);

document.querySelector('[data-form="create-post"]')?.addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  const button = form.querySelector('button[type="submit"]');
  const data = formData(form);
  const selectedCommunityId = data.communityId;

  if (!selectedCommunityId) {
    toast("Прямую публикацию можно сделать только в своем сообществе.", "error");
    return;
  }

  await runWithButton(button, "Публикуем...", async () => {
    const media = await filesToMedia(form.media?.files);
    await api("/posts", toJson("POST", {
      communityId: selectedCommunityId,
      title: data.title,
      text: data.text,
      media
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
    allMyCommunities = await api("/api/communities/my");
    communities = allMyCommunities.filter((community) => communityRole(community) === "Owner");
    const preset = new URLSearchParams(location.search).get("communityId");

    if (!communities.length) {
      communitySelect.innerHTML = '<option value="">Нет сообществ, где вы владелец</option>';
      communitySelect.disabled = true;
      submitPostButton.disabled = true;
      formHint.textContent = allMyCommunities.length
        ? "Вы можете предложить пост на странице «Сообщества». Прямая публикация доступна только владельцу."
        : "Создайте свое сообщество или вступите в существующее, чтобы предложить пост владельцу.";
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
    await preloadUsers(posts.map((post) => post.authorId));
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
      ${renderVoteControls(post)}
      <p>${escapeHtml(post.text ?? "")}</p>
      <div class="meta">
        <span>ID ${shortId(post.id)}</span>
        <span>Сообщество: ${escapeHtml(community?.name ?? shortId(post.communityId))}</span>
        <a href="/UserProfile?userId=${post.authorId}">Автор: ${escapeHtml(userDisplayName(post.authorId))}</a>
        <span>${formatDate(post.createdAt)}</span>
        ${post.updatedAt ? `<span>Изменен: ${formatDate(post.updatedAt)}</span>` : ""}
      </div>
      ${renderMedia(post.id, post.media)}
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
      <span>${post.upvotes ?? 0} за · ${post.downvotes ?? 0} против</span>
    </div>`;
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

function bindPostActions() {
  for (const button of document.querySelectorAll("[data-vote-post]")) {
    button.addEventListener("click", () => votePost(button));
  }

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
    authorId: session.user?.id,
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

async function votePost(button) {
  await runWithButton(button, "...", async () => {
    await api(`/posts/${button.dataset.votePost}/vote`, toJson("POST", {
      value: Number(button.dataset.voteValue)
    }));
    await loadPosts();
  });
}

async function filesToMedia(fileList) {
  const files = Array.from(fileList || []);
  if (!files.length) return [];
  if (files.length > 10) {
    throw new Error("В один пост можно добавить не больше 10 файлов.");
  }

  return await Promise.all(files.map(async (file) => {
    if (file.size > 10 * 1024 * 1024) {
      throw new Error(`Файл ${file.name} больше 10 MB.`);
    }

    return {
      fileName: file.name,
      contentType: file.type || "application/octet-stream",
      base64Content: await readFileAsBase64(file)
    };
  }));
}

function readFileAsBase64(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.addEventListener("load", () => {
      const value = String(reader.result || "");
      resolve(value.includes(",") ? value.split(",").pop() : value);
    });
    reader.addEventListener("error", () => reject(new Error(`Не удалось прочитать файл ${file.name}.`)));
    reader.readAsDataURL(file);
  });
}

function communityRole(community) {
  return community.currentUserRole ?? community.currentUserMembership?.role ?? community.role;
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
