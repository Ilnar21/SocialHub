import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formData, shortId } from "../core/dom.js";
import { toast } from "../core/toast.js";

const list = document.querySelector("[data-communities-list]");
const ownedList = document.querySelector("[data-owned-communities-list]");
const membershipCounter = document.querySelector("[data-membership-counter]");
const searchInput = document.querySelector("[data-community-search]");
const createForm = document.querySelector('[data-form="create-community"]');

let communities = [];
let myCommunities = [];

document.querySelector("[data-load-communities]")?.addEventListener("click", () => loadCommunities());
document.querySelector("[data-toggle-create-community]")?.addEventListener("click", () => {
  createForm.hidden = !createForm.hidden;
  if (!createForm.hidden) createForm.elements.name.focus();
});
document.querySelector("[data-close-create-community]")?.addEventListener("click", () => {
  createForm.hidden = true;
});
searchInput?.addEventListener("input", renderCommunities);

createForm?.addEventListener("submit", async (event) => {
  event.preventDefault();
  const button = createForm.querySelector('button[type="submit"]');

  await runWithButton(button, "Создаем...", async () => {
    const created = await api("/api/communities", toJson("POST", formData(createForm)));
    createForm.reset();
    createForm.hidden = true;
    toast("Сообщество создано. Вы назначены владельцем.");
    location.href = `/CommunityDetails?communityId=${created.id}`;
  });
});

await loadCommunities();

async function loadCommunities() {
  list.innerHTML = empty("Загружаем сообщества...");
  ownedList.innerHTML = empty("Проверяем ваши сообщества...");

  try {
    [communities, myCommunities] = await Promise.all([
      api("/api/communities"),
      api("/api/communities/my")
    ]);

    membershipCounter.textContent = `${myCommunities.length} из 30`;
    renderCommunities();
    renderOwnedCommunities();
  } catch (error) {
    list.innerHTML = empty(error.message);
    ownedList.innerHTML = empty(error.message);
  }
}

function renderCommunities() {
  const query = normalize(searchInput?.value);
  const results = communities
    .filter((community) => !query || normalize(`${community.name} ${community.description}`).includes(query))
    .sort((left, right) => left.name.localeCompare(right.name, "ru"));

  list.innerHTML = results.length
    ? results.map(renderCommunityResult).join("")
    : empty(query ? "Ничего не найдено." : "Сообществ пока нет.");
  bindCommunityActions(list);
}

function renderOwnedCommunities() {
  const owned = myCommunities
    .filter((community) => communityRole(community) === "Owner")
    .sort((left, right) => left.name.localeCompare(right.name, "ru"));

  ownedList.innerHTML = owned.length
    ? owned.map(renderOwnedCommunity).join("")
    : empty("У вас пока нет своих сообществ.");
}

function renderCommunityResult(community) {
  const membership = findMembership(community.id);
  const isMember = Boolean(membership);
  const role = communityRole(membership);
  const canLeave = isMember && role !== "Owner";

  return `
    <article class="community-result-card">
      <a class="community-result-main" href="/CommunityDetails?communityId=${community.id}">
        <span class="community-logo">${communityInitial(community.name)}</span>
        <span>
          <strong>${escapeHtml(community.name)}</strong>
          <small>${escapeHtml(community.description ?? "")}</small>
        </span>
      </a>
      <div class="community-result-meta">
        <span>${community.membersCount ?? 0} подписчиков</span>
        <span>${typeLabel(community.type)}</span>
      </div>
      <div class="actions no-margin">
        ${renderMembershipAction(community.id, isMember, role, canLeave)}
      </div>
    </article>`;
}

function renderOwnedCommunity(community) {
  return `
    <a class="owned-community-link" href="/CommunityDetails?communityId=${community.id}">
      <span class="community-logo">${communityInitial(community.name)}</span>
      <span>
        <strong>${escapeHtml(community.name)}</strong>
        <small>${community.membersCount ?? 0} подписчиков</small>
      </span>
    </a>`;
}

function renderMembershipAction(communityId, isMember, role, canLeave) {
  if (!isMember) {
    return `<button class="button primary" type="button" data-join="${communityId}">Вступить</button>`;
  }

  if (role === "Owner") {
    return `<a class="button secondary" href="/CommunityDetails?communityId=${communityId}">Управлять</a>`;
  }

  return `<button class="button secondary" type="button" data-leave="${communityId}" ${canLeave ? "" : "disabled"}>Выйти</button>`;
}

function bindCommunityActions(scope) {
  for (const button of scope.querySelectorAll("[data-join]")) {
    button.addEventListener("click", () => runCommunityAction(button, button.dataset.join, "POST"));
  }

  for (const button of scope.querySelectorAll("[data-leave]")) {
    button.addEventListener("click", () => runCommunityAction(button, button.dataset.leave, "DELETE"));
  }
}

async function runCommunityAction(button, id, method) {
  await runWithButton(button, method === "POST" ? "Вступаем..." : "Выходим...", async () => {
    const path = method === "POST" ? `/api/communities/${id}/join` : `/api/communities/${id}/membership`;
    await api(path, { method });
    toast(method === "POST" ? "Вы вступили в сообщество." : "Вы вышли из сообщества.");
    await loadCommunities();
  });
}

function findMembership(communityId) {
  return myCommunities.find((community) => community.id === communityId);
}

function communityRole(community) {
  return community?.currentUserRole ?? community?.currentUserMembership?.role ?? community?.role;
}

function communityInitial(name) {
  return String(name || "C").trim().slice(0, 1).toUpperCase() || "C";
}

function normalize(value) {
  return String(value ?? "").trim().toLocaleLowerCase("ru");
}

function typeLabel(type) {
  return type === "Closed" ? "Закрытое" : "Открытое";
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
