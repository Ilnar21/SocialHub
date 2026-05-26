import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formData } from "../core/dom.js";
import { toast } from "../core/toast.js";

const list = document.querySelector("[data-communities-list]");
const ownedList = document.querySelector("[data-owned-communities-list]");
const subscriptionsList = document.querySelector("[data-subscriptions-list]");
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
    const data = formData(createForm);
    data.username = normalizeUsername(data.username);
    const created = await api("/api/communities", toJson("POST", data));
    createForm.reset();
    createForm.hidden = true;
    toast("Сообщество создано. Вы назначены владельцем.");
    location.href = communityUrl(created);
  });
});

await loadCommunities();

async function loadCommunities() {
  list.innerHTML = empty("Загружаем сообщества...");
  ownedList.innerHTML = empty("Проверяем ваши сообщества...");
  subscriptionsList.innerHTML = empty("Проверяем ваши подписки...");

  try {
    [communities, myCommunities] = await Promise.all([
      api("/api/communities"),
      api("/api/communities/my")
    ]);

    membershipCounter.textContent = `${myCommunities.length} из 30 подписок`;
    renderCommunities();
    renderSubscriptions();
    renderOwnedCommunities();
  } catch (error) {
    list.innerHTML = empty(error.message);
    ownedList.innerHTML = empty(error.message);
    subscriptionsList.innerHTML = empty(error.message);
  }
}

function renderCommunities() {
  const query = normalize(searchInput?.value);
  if (!query) {
    list.innerHTML = empty("Введите название, описание или @username сообщества, чтобы увидеть результаты поиска.");
    return;
  }

  const results = communities
    .filter((community) => normalize(`${community.name} ${community.username} ${community.description}`).includes(query))
    .sort((left, right) => left.name.localeCompare(right.name, "ru"));

  list.innerHTML = results.length
    ? results.map(renderCommunityResult).join("")
    : empty("Ничего не найдено.");
  bindCommunityActions(list);
}

function renderSubscriptions() {
  const subscriptions = myCommunities
    .sort((left, right) => left.name.localeCompare(right.name, "ru"));

  subscriptionsList.innerHTML = subscriptions.length
    ? subscriptions.map(renderSubscription).join("")
    : empty("Вы пока не подписаны ни на одно сообщество.");
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
      <a class="community-result-main" href="${communityUrl(community)}">
        <span class="community-logo">${communityInitial(community.name)}</span>
        <span>
          <strong>${escapeHtml(community.name)}</strong>
          <small>@${escapeHtml(community.username)} · ${escapeHtml(community.description ?? "")}</small>
        </span>
      </a>
      <div class="community-result-meta">
        <span>${community.membersCount ?? 0} подписчиков</span>
        <span>${typeLabel(community.type)}</span>
      </div>
      <div class="actions no-margin">
        ${renderMembershipAction(community, isMember, role, canLeave)}
      </div>
    </article>`;
}

function renderSubscription(community) {
  return `
    <a class="owned-community-link" href="${communityUrl(community)}">
      <span class="community-logo">${communityInitial(community.name)}</span>
      <span>
        <strong>${escapeHtml(community.name)}</strong>
        <small>@${escapeHtml(community.username)} · ${roleLabel(communityRole(community))}</small>
      </span>
    </a>`;
}

function renderOwnedCommunity(community) {
  return `
    <a class="owned-community-link" href="${communityUrl(community)}">
      <span class="community-logo">${communityInitial(community.name)}</span>
      <span>
        <strong>${escapeHtml(community.name)}</strong>
        <small>@${escapeHtml(community.username)} · ${community.membersCount ?? 0} подписчиков</small>
      </span>
    </a>`;
}

function renderMembershipAction(community, isMember, role, canLeave) {
  const communityId = community.id;
  if (!isMember) {
    if (community.type === "Closed") {
      if (community.currentUserJoinRequest?.status === "Pending") {
        return `<button class="button secondary" type="button" disabled>Заявка отправлена</button>`;
      }

      return `<button class="button primary" type="button" data-request-join="${communityId}">Подать заявку</button>`;
    }

    return `<button class="button primary" type="button" data-join="${communityId}">Вступить</button>`;
  }

  if (role === "Owner") {
    return `<a class="button secondary" href="${communityUrl(findCommunity(communityId))}">Управлять</a>`;
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

  for (const button of scope.querySelectorAll("[data-request-join]")) {
    button.addEventListener("click", () => runJoinRequestAction(button, button.dataset.requestJoin));
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

async function runJoinRequestAction(button, id) {
  await runWithButton(button, "Отправляем...", async () => {
    await api(`/api/communities/${id}/join-requests`, { method: "POST" });
    toast("Заявка отправлена владельцу сообщества.");
    await loadCommunities();
  });
}

function findMembership(communityId) {
  return myCommunities.find((community) => community.id === communityId);
}

function findCommunity(communityId) {
  return communities.find((community) => community.id === communityId) ?? findMembership(communityId);
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

function roleLabel(role) {
  return {
    Owner: "владелец",
    Admin: "администратор",
    Member: "подписка"
  }[role] ?? "подписка";
}

function communityUrl(community) {
  return community?.username
    ? `/CommunityDetails?username=${encodeURIComponent(community.username)}`
    : `/CommunityDetails?communityId=${community?.id ?? ""}`;
}

function normalizeUsername(value) {
  return String(value ?? "").trim().toLowerCase();
}

async function runWithButton(button, pendingText, action) {
  const originalText = button.textContent;
  button.dataset.busy = "true";
  button.disabled = true;
  button.textContent = pendingText;
  try {
    await action();
  } catch (error) {
    toast(error.message, "error");
  } finally {
    button.disabled = false;
    delete button.dataset.busy;
    button.textContent = originalText;
  }
}
