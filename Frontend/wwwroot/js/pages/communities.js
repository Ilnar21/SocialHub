import { api, toJson } from "../core/api.js";
import { getSession } from "../core/session.js";
import { empty, escapeHtml, formatDate, formData, shortId } from "../core/dom.js";
import { preloadUsers, userDisplayName } from "../core/identity.js";
import { toast } from "../core/toast.js";

const list = document.querySelector("[data-communities-list]");
const detail = document.querySelector("[data-community-detail]");
const membershipCounter = document.querySelector("[data-membership-counter]");

let communities = [];
let myCommunities = [];
let selectedCommunityId = "";

document.querySelector("[data-load-communities]")?.addEventListener("click", loadCommunities);
document.querySelector('[data-form="create-community"]')?.addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  const button = form.querySelector('button[type="submit"]');
  const data = formData(form);

  await runWithButton(button, "Создаем...", async () => {
    const created = await api("/api/communities", toJson("POST", data));
    form.reset();
    selectedCommunityId = created.id;
    toast("Сообщество создано. Вы назначены владельцем.");
    await loadCommunities({ keepSelection: true });
  });
});

loadCommunities();

async function loadCommunities(options = {}) {
  list.innerHTML = empty("Загружаем сообщества...");
  try {
    [communities, myCommunities] = await Promise.all([
      api("/api/communities"),
      api("/api/communities/my")
    ]);

    membershipCounter.textContent = `${myCommunities.length} из 30`;
    list.innerHTML = communities.length
      ? communities.map(renderCommunity).join("")
      : empty("Сообществ пока нет. Создайте первое сообщество.");

    bindActions();

    if (!options.keepSelection && !selectedCommunityId) {
      selectedCommunityId = communities[0]?.id ?? "";
    }

    if (selectedCommunityId && communities.some((community) => community.id === selectedCommunityId)) {
      await openCommunity(selectedCommunityId);
    } else {
      renderDetailEmpty();
    }
  } catch (error) {
    list.innerHTML = empty(error.message);
    renderDetailEmpty(error.message);
  }
}

function renderCommunity(community) {
  const membership = findMembership(community.id);
  const isMember = Boolean(membership);
  const role = membershipRole(membership);
  const canLeave = isMember && role !== "Owner";
  const isActive = selectedCommunityId === community.id;
  const canPublishDirectly = role === "Owner";

  return `
    <article class="card ${isActive ? "active-card" : ""}" data-community-card="${community.id}">
      <div class="row">
        <h2>${escapeHtml(community.name)}</h2>
        ${isMember ? `<span class="badge success">${roleLabel(role)}</span>` : `<span class="badge">${typeLabel(community.type)}</span>`}
      </div>
      <p>${escapeHtml(community.description ?? "")}</p>
      <div class="meta">
        <span>ID ${shortId(community.id)}</span>
        <span>Участников: ${community.membersCount ?? community.memberCount ?? 0}</span>
        ${isMember ? "<span>Вы в сообществе</span>" : ""}
      </div>
      <div class="actions">
        <button class="button secondary" data-open-community="${community.id}">Открыть</button>
        ${isMember
          ? `<button class="button secondary" data-leave="${community.id}" ${canLeave ? "" : "disabled"}>${canLeave ? "Покинуть" : "Владелец"}</button>`
          : `<button class="button primary" data-join="${community.id}">Вступить</button>`}
        ${canPublishDirectly ? `<a class="button secondary" href="/Posts?communityId=${community.id}">Опубликовать пост</a>` : ""}
      </div>
    </article>`;
}

function bindActions() {
  for (const button of document.querySelectorAll("[data-open-community]")) {
    button.addEventListener("click", () => openCommunity(button.dataset.openCommunity));
  }

  for (const button of document.querySelectorAll("[data-join]")) {
    button.addEventListener("click", () => runCommunityAction(button, button.dataset.join, "POST"));
  }

  for (const button of document.querySelectorAll("[data-leave]")) {
    button.addEventListener("click", () => runCommunityAction(button, button.dataset.leave, "DELETE"));
  }
}

async function openCommunity(id) {
  selectedCommunityId = id;
  detail.innerHTML = empty("Загружаем детали сообщества...");
  refreshActiveCard();

  try {
    const community = await api(`/api/communities/${id}`);
    const members = community.currentUserMembership
      ? await api(`/api/communities/${id}/members`)
      : [];
    const isOwner = isOwnerRole(community.currentUserMembership?.role);
    const suggestedPosts = isOwner
      ? await api(`/api/communities/${id}/suggested-posts?status=Pending`)
      : [];

    await preloadUsers([
      ...members.map((member) => member.userId),
      ...suggestedPosts.map((post) => post.authorUserId)
    ]);
    detail.innerHTML = renderDetail(community, members, suggestedPosts);
    bindDetailActions(community);
  } catch (error) {
    detail.innerHTML = empty(error.message);
  }
}

function renderDetail(community, members, suggestedPosts) {
  const membership = community.currentUserMembership;
  const isMember = Boolean(membership);
  const isAdmin = hasAdminRole(membership?.role);
  const isOwner = isOwnerRole(membership?.role);

  return `
    <div class="post-detail">
      <div>
        <div class="row">
          <h2>${escapeHtml(community.name)}</h2>
          <span class="badge ${isMember ? "success" : ""}">${isMember ? roleLabel(membership.role) : typeLabel(community.type)}</span>
        </div>
        <p>${escapeHtml(community.description ?? "")}</p>
        <div class="meta">
          <span>Создано: ${formatDate(community.createdAtUtc)}</span>
          <span>Участников: ${community.membersCount ?? members.length}</span>
        </div>
      </div>

      ${isMember ? renderSuggestionForm(community) : `
        <div class="empty">
          Вступите в сообщество, чтобы публиковать и предлагать посты.
        </div>`}

      <section class="stack">
        <div class="row">
          <h2>Участники</h2>
          <span class="badge">${members.length}</span>
        </div>
        ${members.length ? members.map((member) => renderMember(member, isAdmin)).join("") : empty("Список участников доступен после вступления.")}
      </section>

      ${isOwner ? `
        <section class="stack">
          <div class="row">
            <h2>Предложенные посты</h2>
            <span class="badge">${suggestedPosts.length}</span>
          </div>
          ${suggestedPosts.length ? suggestedPosts.map(renderSuggestedPost).join("") : empty("Новых предложений нет.")}
        </section>` : ""}
    </div>`;
}

function renderSuggestionForm(community) {
  return `
    <form class="panel form-grid" data-form="suggest-post" data-community-id="${community.id}">
      <h2>Предложить пост</h2>
      <label>Заголовок
        <input name="title" maxlength="120" placeholder="Разбор C4" required />
      </label>
      <label>Текст
        <textarea name="text" maxlength="10000" placeholder="Полезная подборка материалов" required></textarea>
      </label>
      <button class="button primary" type="submit">Отправить на рассмотрение</button>
    </form>`;
}

function renderMember(member, isAdmin) {
  const currentUserId = getSession().user?.id;
  const canRemove = isAdmin && member.userId !== currentUserId && member.role !== "Owner";

  return `
    <article class="card">
      <div class="row">
        <strong>${escapeHtml(userDisplayName(member.userId))}</strong>
        <span class="badge">${roleLabel(member.role)}</span>
      </div>
      <div class="meta">
        <span>Вступил: ${formatDate(member.joinedAtUtc)}</span>
      </div>
      ${canRemove ? `
        <div class="actions">
          <button class="button danger" data-remove-member="${member.userId}">Удалить из сообщества</button>
        </div>` : ""}
    </article>`;
}

function renderSuggestedPost(post) {
  return `
    <article class="card">
      <div class="row">
        <h2>${escapeHtml(post.title)}</h2>
        <span class="badge warn">${escapeHtml(post.status)}</span>
      </div>
      <p>${escapeHtml(post.text)}</p>
      <div class="meta">
        <span>Автор: ${escapeHtml(userDisplayName(post.authorUserId))}</span>
        <span>${formatDate(post.createdAtUtc)}</span>
      </div>
      <div class="actions">
        <button class="button primary" data-approve-suggested="${post.id}">Опубликовать</button>
        <button class="button secondary" data-reject-suggested="${post.id}">Отклонить</button>
      </div>
    </article>`;
}

function bindDetailActions(community) {
  detail.querySelector('[data-form="suggest-post"]')?.addEventListener("submit", async (event) => {
    event.preventDefault();
    const form = event.currentTarget;
    const button = form.querySelector('button[type="submit"]');

    await runWithButton(button, "Отправляем...", async () => {
      await api(`/api/communities/${community.id}/suggested-posts`, toJson("POST", formData(form)));
      form.reset();
      toast("Пост отправлен администраторам сообщества.");
      await openCommunity(community.id);
    });
  });

  for (const button of detail.querySelectorAll("[data-remove-member]")) {
    button.addEventListener("click", async () => {
      if (!confirm("Удалить участника из сообщества?")) return;
      await runWithButton(button, "Удаляем...", async () => {
        await api(`/api/communities/${community.id}/members/${button.dataset.removeMember}`, { method: "DELETE" });
        toast("Участник удален из сообщества.");
        await openCommunity(community.id);
      });
    });
  }

  for (const button of detail.querySelectorAll("[data-approve-suggested]")) {
    button.addEventListener("click", () => reviewSuggestedPost(button, community.id, button.dataset.approveSuggested, "approve"));
  }

  for (const button of detail.querySelectorAll("[data-reject-suggested]")) {
    button.addEventListener("click", () => reviewSuggestedPost(button, community.id, button.dataset.rejectSuggested, "reject"));
  }
}

async function reviewSuggestedPost(button, communityId, suggestedPostId, action) {
  const request = action === "reject"
    ? toJson("POST", { comment: "Отклонено администратором сообщества" })
    : { method: "POST" };

  await runWithButton(button, action === "approve" ? "Публикуем..." : "Отклоняем...", async () => {
    await api(`/api/communities/${communityId}/suggested-posts/${suggestedPostId}/${action}`, request);
    toast(action === "approve" ? "Предложенный пост опубликован." : "Предложенный пост отклонен.");
    await openCommunity(communityId);
  });
}

async function runCommunityAction(button, id, method) {
  await runWithButton(button, method === "POST" ? "Вступаем..." : "Выходим...", async () => {
    const path = method === "POST" ? `/api/communities/${id}/join` : `/api/communities/${id}/membership`;
    await api(path, { method });
    toast(method === "POST" ? "Вы вступили в сообщество." : "Вы покинули сообщество.");
    selectedCommunityId = id;
    await loadCommunities({ keepSelection: true });
  });
}

function findMembership(communityId) {
  return myCommunities.find((community) => community.id === communityId);
}

function hasAdminRole(role) {
  return role === "Owner" || role === "Admin";
}

function isOwnerRole(role) {
  return role === "Owner";
}

function membershipRole(membership) {
  return membership?.currentUserMembership?.role ?? membership?.currentUserRole ?? membership?.role;
}

function roleLabel(role) {
  return {
    Owner: "Владелец",
    Admin: "Администратор",
    Member: "Участник"
  }[role] ?? "Участник";
}

function typeLabel(type) {
  return type === "Closed" ? "Закрытое" : "Открытое";
}

function refreshActiveCard() {
  for (const card of document.querySelectorAll("[data-community-card]")) {
    card.classList.toggle("active-card", card.dataset.communityCard === selectedCommunityId);
  }
}

function renderDetailEmpty(message = "Выберите сообщество, чтобы увидеть участников, роль и предложенные посты.") {
  detail.innerHTML = `<div class="empty">${escapeHtml(message)}</div>`;
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
