import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formData, formatDate } from "../core/dom.js";
import { preloadUsers, userDisplayName, userProfileHref } from "../core/identity.js";
import { bindReportButtons } from "../core/reports.js";
import { getSession, isPlatformModerator } from "../core/session.js";
import { toast } from "../core/toast.js";

const createPanel = document.querySelector("[data-create-post-panel]");
const createCommunitySelect = document.querySelector("[data-create-community-select]");
const filterCommunitySelect = document.querySelector("[data-posts-community-filter]");
const sortSelect = document.querySelector("[data-posts-sort]");
const authorFilter = document.querySelector("[data-posts-author-filter]");
const submitPostButton = document.querySelector("[data-submit-post]");
const formHint = document.querySelector("[data-post-form-hint]");
const list = document.querySelector("[data-posts-list]");

let allMyCommunities = [];
let ownerCommunities = [];
let loadedPosts = [];

document.querySelector("[data-load-posts]")?.addEventListener("click", loadPosts);
document.querySelector("[data-toggle-create-post]")?.addEventListener("click", () => {
  createPanel.hidden = !createPanel.hidden;
});

filterCommunitySelect?.addEventListener("change", loadPosts);
sortSelect?.addEventListener("change", renderLoadedPosts);
authorFilter?.addEventListener("change", renderLoadedPosts);

document.querySelector("[data-posts-filters]")?.addEventListener("submit", (event) => {
  event.preventDefault();
});

document.querySelector('[data-form="create-post"]')?.addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  const button = form.querySelector('button[type="submit"]');
  const data = formData(form);
  const selectedCommunityId = data.communityId;

  if (!selectedCommunityId) {
    toast("Прямую публикацию можно сделать только в сообществе, где вы владелец.", "error");
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
    createCommunitySelect.value = selectedCommunityId;
    filterCommunitySelect.value = selectedCommunityId;
    createPanel.hidden = true;
    toast("Пост опубликован.");
    await loadPosts();
  });
});

await loadCommunities();
await loadPosts();

async function loadCommunities() {
  try {
    allMyCommunities = await api("/api/communities/my");
    ownerCommunities = allMyCommunities.filter((community) => communityRole(community) === "Owner");
    const preset = new URLSearchParams(location.search).get("communityId");

    renderCreateCommunityOptions(preset);
    renderFilterCommunityOptions(preset);
  } catch (error) {
    createCommunitySelect.innerHTML = `<option value="">${escapeHtml(error.message)}</option>`;
    createCommunitySelect.disabled = true;
    filterCommunitySelect.innerHTML = `<option value="">${escapeHtml(error.message)}</option>`;
    filterCommunitySelect.disabled = true;
    submitPostButton.disabled = true;
    formHint.textContent = "Не удалось загрузить сообщества пользователя.";
  }
}

function renderCreateCommunityOptions(preset) {
  if (!ownerCommunities.length) {
    createCommunitySelect.innerHTML = '<option value="">Нет сообществ, где вы владелец</option>';
    createCommunitySelect.disabled = true;
    submitPostButton.disabled = true;
    formHint.textContent = allMyCommunities.length
      ? "Прямую публикацию может сделать только владелец сообщества. В чужом сообществе используйте предложенный пост."
      : "Создайте свое сообщество или вступите в существующее, чтобы работать с постами.";
    return;
  }

  createCommunitySelect.disabled = false;
  submitPostButton.disabled = false;
  formHint.textContent = "";
  createCommunitySelect.innerHTML = ownerCommunities.map((community) => (
    `<option value="${community.id}">${escapeHtml(community.name)}</option>`
  )).join("");

  if (preset && ownerCommunities.some((community) => community.id === preset)) {
    createCommunitySelect.value = preset;
  }
}

function renderFilterCommunityOptions(preset) {
  if (!allMyCommunities.length) {
    filterCommunitySelect.innerHTML = '<option value="">Нет ваших сообществ</option>';
    filterCommunitySelect.disabled = true;
    return;
  }

  filterCommunitySelect.disabled = false;
  filterCommunitySelect.innerHTML = `
    <option value="">Все мои сообщества</option>
    ${allMyCommunities.map((community) => `<option value="${community.id}">${escapeHtml(community.name)}</option>`).join("")}`;

  if (preset && allMyCommunities.some((community) => community.id === preset)) {
    filterCommunitySelect.value = preset;
  }
}

async function loadPosts() {
  if (!allMyCommunities.length) {
    list.innerHTML = empty("Подпишитесь на сообщества, чтобы видеть посты.");
    return;
  }

  const selectedCommunityId = filterCommunitySelect.value;
  const targetCommunities = selectedCommunityId
    ? allMyCommunities.filter((community) => community.id === selectedCommunityId)
    : allMyCommunities;

  list.innerHTML = empty("Загружаем посты...");
  try {
    const batches = await Promise.all(targetCommunities.map((community) => api(`/communities/${community.id}/posts`)));
    loadedPosts = batches.flat();
    await preloadUsers(loadedPosts.map((post) => post.authorId));
    renderAuthorOptions();
    renderLoadedPosts();
  } catch (error) {
    list.innerHTML = empty(error.message);
  }
}

function renderAuthorOptions() {
  const selectedAuthorId = authorFilter.value;
  const authors = [...new Set(loadedPosts.map((post) => post.authorId).filter(Boolean))]
    .sort((left, right) => userDisplayName(left).localeCompare(userDisplayName(right), "ru"));

  authorFilter.innerHTML = `
    <option value="">Все авторы</option>
    ${authors.map((authorId) => `<option value="${authorId}">${escapeHtml(userDisplayName(authorId))}</option>`).join("")}`;

  if (selectedAuthorId && authors.includes(selectedAuthorId)) {
    authorFilter.value = selectedAuthorId;
  }
}

function renderLoadedPosts() {
  const posts = applyFilters(loadedPosts);
  list.innerHTML = posts.length
    ? posts.map(renderPost).join("")
    : empty("По выбранным фильтрам постов нет.");
  bindPostActions();
}

function applyFilters(posts) {
  const selectedAuthorId = authorFilter.value;
  const filtered = selectedAuthorId
    ? posts.filter((post) => post.authorId === selectedAuthorId)
    : [...posts];

  return filtered.sort((left, right) => {
    const leftTime = new Date(left.createdAt ?? left.createdAtUtc ?? 0).getTime();
    const rightTime = new Date(right.createdAt ?? right.createdAtUtc ?? 0).getTime();
    return sortSelect.value === "oldest" ? leftTime - rightTime : rightTime - leftTime;
  });
}

function renderPost(post) {
  const session = getSession();
  const commentsCount = getComments(post.id).length;
  const isAuthor = post.authorId === session.user?.id;
  const community = allMyCommunities.find((item) => item.id === post.communityId);
  const isCommunityOwner = communityRole(community) === "Owner";
  const canEdit = isAuthor && isCommunityOwner;
  const canDelete = isAuthor || isCommunityOwner;
  const canModerate = isPlatformModerator();

  return `
    <article class="card post-card" data-post-card="${post.id}">
      <div class="post-card-meta">
        <span class="community-mark">${communityInitial(community)}</span>
        <a class="community-inline-link" href="${escapeHtml(communityUrl(community, post.communityId))}">${escapeHtml(community?.name ?? "Сообщество")}</a>
        ${renderAuthorLink(post.authorId)}
        <span>${formatDate(post.createdAt)}</span>
        ${post.updatedAt ? `<span>Изменен: ${formatDate(post.updatedAt)}</span>` : ""}
      </div>
      <h2><a class="post-card-title" href="/PostDetails?postId=${post.id}">${escapeHtml(post.title)}</a></h2>
      ${renderVoteControls(post)}
      <p>${escapeHtml(post.text ?? "")}</p>
      ${renderMedia(post.id, post.media)}
      ${canEdit ? renderEditForm(post) : ""}
      <div class="post-card-footer">
        <a class="comment-pill" href="/PostDetails?postId=${post.id}#comments">${commentsCount} комментариев</a>
        <button class="button secondary" type="button"
                data-report-target-type="POST"
                data-report-target-id="${post.id}"
                data-report-target-label="Пост: ${escapeHtml(post.title)}">
          Пожаловаться
        </button>
        ${canDelete ? `<button class="button danger" data-delete-owned-post="${post.id}">${isAuthor ? "Удалить свой пост" : "Удалить пост сообщества"}</button>` : ""}
        ${canModerate ? `<button class="button danger" data-delete-post-moderation="${post.id}">Удалить как модератор</button>` : ""}
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

function bindPostActions() {
  for (const button of list.querySelectorAll("[data-vote-post]")) {
    button.addEventListener("click", () => votePost(button));
  }

  bindReportButtons(list);

  for (const form of list.querySelectorAll('[data-form="edit-post"]')) {
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

  for (const button of list.querySelectorAll("[data-delete-owned-post]")) {
    button.addEventListener("click", async () => {
      if (!confirm("Удалить пост?")) return;
      await runWithButton(button, "Удаляем...", async () => {
        await api(`/posts/${button.dataset.deleteOwnedPost}`, toJson("DELETE", {}));
        toast("Пост удален.");
        await loadPosts();
      });
    });
  }

  for (const button of list.querySelectorAll("[data-delete-post-moderation]")) {
    button.addEventListener("click", async () => {
      if (!confirm("Удалить пост как модератор платформы?")) return;
      await runWithButton(button, "Удаляем...", async () => {
        await api(`/api/posts/${button.dataset.deletePostModeration}/moderation-delete`, toJson("POST", {
          reason: "Удалено модератором через frontend"
        }));
        toast("Пост удален модератором.");
        await loadPosts();
      });
    });
  }
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

async function votePost(button) {
  const scrollY = window.scrollY;
  await runWithButton(button, "...", async () => {
    await api(`/posts/${button.dataset.votePost}/vote`, toJson("POST", {
      value: Number(button.dataset.voteValue)
    }));
    await loadPosts();
    requestAnimationFrame(() => window.scrollTo(0, scrollY));
  });
}

function renderAuthorLink(userId) {
  const href = userProfileHref(userId);
  const label = `Автор: ${userDisplayName(userId)}`;
  return href
    ? `<a href="${escapeHtml(href)}">${escapeHtml(label)}</a>`
    : `<span>${escapeHtml(label)}</span>`;
}

function communityUrl(community, fallbackId) {
  return community?.username
    ? `/CommunityDetails?username=${encodeURIComponent(community.username)}`
    : `/CommunityDetails?communityId=${fallbackId}`;
}

function communityInitial(community) {
  return String(community?.name ?? "C").trim().slice(0, 1).toUpperCase() || "C";
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
  return community?.currentUserRole ?? community?.currentUserMembership?.role ?? community?.role;
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
