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
    const data = formData(form);
    const avatarFile = form.avatarFile?.files?.[0];
    if (avatarFile) {
      if (avatarFile.size > 1024 * 1024) {
        throw new Error("Аватар должен быть не больше 1 MB.");
      }

      data.avatarUrl = await readFileAsDataUrl(avatarFile);
    }

    delete data.avatarFile;
    const user = await api(`/api/users/${session.user.id}/profile`, toJson("PUT", data));
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

  const avatarUrl = user.profile?.avatarUrl;
  summary.innerHTML = `
    ${avatarUrl ? `<img class="profile-avatar" src="${escapeHtml(avatarUrl)}" alt="${escapeHtml(user.profile?.displayName || user.username)}" />` : ""}
    <h2>${escapeHtml(user.profile?.displayName || user.username)}</h2>
    <p>${escapeHtml(user.profile?.bio || "Описание пока не заполнено.")}</p>
    <div class="meta">
      <span>ID ${escapeHtml(user.id)}</span>
      <span>${escapeHtml(user.email)}</span>
      <span>${escapeHtml(user.role)}</span>
      <span>${escapeHtml(user.status)}</span>
    </div>`;
}

function readFileAsDataUrl(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.addEventListener("load", () => resolve(String(reader.result || "")));
    reader.addEventListener("error", () => reject(new Error(`Не удалось прочитать файл ${file.name}.`)));
    reader.readAsDataURL(file);
  });
}
