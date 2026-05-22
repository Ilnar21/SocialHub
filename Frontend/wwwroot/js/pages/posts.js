import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formData, formatDate, shortId } from "../core/dom.js";
import { getSession } from "../core/session.js";
import { toast } from "../core/toast.js";

const communitySelect = document.querySelector("[data-community-select]");
const list = document.querySelector("[data-posts-list]");

document.querySelector("[data-load-posts]")?.addEventListener("click", loadPosts);
document.querySelector('[data-form="create-post"]')?.addEventListener("submit", async (event) => {
  event.preventDefault();
  const session = getSession();
  const data = formData(event.currentTarget);

  try {
    await api("/posts", toJson("POST", {
      authorId: session.user.id,
      communityId: data.communityId,
      title: data.title,
      text: data.text
    }));
    event.currentTarget.reset();
    toast("Пост опубликован");
    await loadPosts();
  } catch (error) {
    toast(error.message, "error");
  }
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
    communitySelect.innerHTML = `<option>${escapeHtml(error.message)}</option>`;
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
    list.innerHTML = posts.length ? posts.map(renderPost).join("") : empty("В этом сообществе пока нет постов.");
    bindDeleteButtons();
  } catch (error) {
    list.innerHTML = empty(error.message);
  }
}

function renderPost(post) {
  return `
    <article class="card">
      <div class="row">
        <h2>${escapeHtml(post.title)}</h2>
        <span class="badge">${escapeHtml(post.status ?? "Published")}</span>
      </div>
      <p>${escapeHtml(post.text ?? "")}</p>
      <div class="meta">
        <span>ID ${shortId(post.id)}</span>
        <span>${formatDate(post.createdAt)}</span>
      </div>
      <div class="actions">
        <button class="button danger" data-delete-post="${post.id}">Удалить</button>
      </div>
    </article>`;
}

function bindDeleteButtons() {
  for (const button of document.querySelectorAll("[data-delete-post]")) {
    button.addEventListener("click", async () => {
      try {
        await api(`/posts/${button.dataset.deletePost}`, toJson("DELETE", { actorId: getSession().user.id }));
        toast("Пост удален");
        await loadPosts();
      } catch (error) {
        toast(error.message, "error");
      }
    });
  }
}
