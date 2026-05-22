import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formData, shortId } from "../core/dom.js";
import { toast } from "../core/toast.js";

const list = document.querySelector("[data-communities-list]");

document.querySelector("[data-load-communities]")?.addEventListener("click", loadCommunities);
document.querySelector('[data-form="create-community"]')?.addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  const button = form.querySelector('button[type="submit"]');
  const data = formData(form);

  await runWithButton(button, "Создаем...", async () => {
    await api("/api/communities", toJson("POST", data));
    form.reset();
    toast("Сообщество создано");
    await loadCommunities();
  });
});

loadCommunities();

async function loadCommunities() {
  list.innerHTML = empty("Загружаем сообщества...");
  try {
    const communities = await api("/api/communities");
    list.innerHTML = communities.length ? communities.map(renderCommunity).join("") : empty("Сообществ пока нет.");
    bindActions();
  } catch (error) {
    list.innerHTML = empty(error.message);
  }
}

function renderCommunity(community) {
  return `
    <article class="card">
      <div class="row">
        <h2>${escapeHtml(community.name)}</h2>
        <span class="badge">${escapeHtml(community.type ?? "Open")}</span>
      </div>
      <p>${escapeHtml(community.description ?? "")}</p>
      <div class="meta">
        <span>ID ${shortId(community.id)}</span>
        <span>Участников: ${community.membersCount ?? community.memberCount ?? 0}</span>
      </div>
      <div class="actions">
        <button class="button primary" data-join="${community.id}">Вступить</button>
        <button class="button secondary" data-leave="${community.id}">Покинуть</button>
        <a class="button secondary" href="/Posts?communityId=${community.id}">Посты</a>
      </div>
    </article>`;
}

function bindActions() {
  for (const button of document.querySelectorAll("[data-join]")) {
    button.addEventListener("click", () => runCommunityAction(button, button.dataset.join, "POST"));
  }

  for (const button of document.querySelectorAll("[data-leave]")) {
    button.addEventListener("click", () => runCommunityAction(button, button.dataset.leave, "DELETE"));
  }
}

async function runCommunityAction(button, id, method) {
  await runWithButton(button, method === "POST" ? "Вступаем..." : "Выходим...", async () => {
    const path = method === "POST" ? `/api/communities/${id}/join` : `/api/communities/${id}/membership`;
    await api(path, { method });
    toast(method === "POST" ? "Вы вступили в сообщество" : "Вы покинули сообщество");
    await loadCommunities();
  });
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
