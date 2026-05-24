import { api } from "../core/api.js";
import { empty, escapeHtml, formatDate } from "../core/dom.js";

const root = document.querySelector("[data-public-profile]");
const params = new URLSearchParams(location.search);
const username = params.get("username");
const userId = params.get("userId");

document.querySelector("[data-back]")?.addEventListener("click", () => {
  history.length > 1 ? history.back() : location.href = "/Feed";
});

if (!username && !userId) {
  root.innerHTML = empty("Пользователь не найден.");
} else {
  await loadProfile();
}

async function loadProfile() {
  root.innerHTML = empty("Загружаем профиль...");

  try {
    const user = username
      ? await api(`/api/users/by-username/${encodeURIComponent(username)}`)
      : await api(`/api/users/${userId}`);

    if (!username && user.username) {
      history.replaceState(null, "", `/UserProfile?username=${encodeURIComponent(user.username)}`);
    }

    renderProfile(user);
  } catch (error) {
    root.innerHTML = empty(error.message);
  }
}

function renderProfile(user) {
  const displayName = user.profile?.displayName || user.username;
  const avatarUrl = user.profile?.avatarUrl;

  root.innerHTML = `
    <article class="panel public-profile-card">
      ${avatarUrl
        ? `<img class="public-profile-avatar" src="${escapeHtml(avatarUrl)}" alt="${escapeHtml(displayName)}" />`
        : `<div class="public-profile-avatar fallback">${escapeHtml(displayName.slice(0, 1).toUpperCase() || "U")}</div>`}
      <div>
        <h1>${escapeHtml(displayName)}</h1>
        <p class="muted">@${escapeHtml(user.username)}</p>
        <p>${escapeHtml(user.profile?.bio || "Описание пока не заполнено.")}</p>
        <div class="meta">
          <span>${escapeHtml(user.role)}</span>
          <span>${escapeHtml(user.status)}</span>
          <span>С нами: ${formatDate(user.createdAt)}</span>
        </div>
      </div>
    </article>`;
}
