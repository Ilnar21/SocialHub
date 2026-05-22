import { api, toJson } from "../core/api.js";
import { escapeHtml, formData } from "../core/dom.js";
import { getSession, saveSession } from "../core/session.js";
import { toast } from "../core/toast.js";

const form = document.querySelector('[data-form="update-profile"]');
const summary = document.querySelector("[data-profile-summary]");

document.querySelector("[data-reload-me]")?.addEventListener("click", loadProfile);
form?.addEventListener("submit", async (event) => {
  event.preventDefault();
  const session = getSession();

  try {
    const user = await api(`/api/users/${session.user.id}/profile`, toJson("PUT", formData(form)));
    saveSession({ token: session.token, user });
    toast("Профиль обновлен");
    renderProfile(user);
  } catch (error) {
    toast(error.message, "error");
  }
});

await loadProfile();

async function loadProfile() {
  const session = getSession();

  try {
    const user = await api("/api/auth/me");
    saveSession({ token: session.token, user });
    renderProfile(user);
  } catch {
    renderProfile(session.user);
  }
}

function renderProfile(user) {
  if (!user) return;

  form.displayName.value = user.profile?.displayName ?? "";
  form.bio.value = user.profile?.bio ?? "";
  form.avatarUrl.value = user.profile?.avatarUrl ?? "";

  summary.innerHTML = `
    <h2>${escapeHtml(user.profile?.displayName || user.username)}</h2>
    <p>${escapeHtml(user.profile?.bio || "Описание пока не заполнено.")}</p>
    <div class="meta">
      <span>ID ${escapeHtml(user.id)}</span>
      <span>${escapeHtml(user.email)}</span>
      <span>${escapeHtml(user.role)}</span>
      <span>${escapeHtml(user.status)}</span>
    </div>`;
}
