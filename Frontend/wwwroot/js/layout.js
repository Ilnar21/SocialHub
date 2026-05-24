import { clearSession, getSession, hasRole } from "./core/session.js";
import { escapeHtml } from "./core/dom.js";

const session = getSession();
const userName = document.querySelector("[data-user-name]");
const userRole = document.querySelector("[data-user-role]");
const userAvatar = document.querySelector("[data-user-avatar]");

if (!session.token && !location.pathname.match(/^\/(Register)?$/)) {
  location.href = "/";
}

const displayName = session.user?.profile?.displayName || session.user?.username || "Гость";

if (userName && userRole) {
  userName.textContent = displayName;
  userRole.textContent = translateRole(session.user?.role);
}

if (userAvatar) {
  const avatarUrl = session.user?.profile?.avatarUrl;
  userAvatar.innerHTML = avatarUrl
    ? `<img src="${escapeHtml(avatarUrl)}" alt="${escapeHtml(displayName)}" />`
    : displayName.trim().slice(0, 1).toUpperCase() || "U";
}

for (const link of document.querySelectorAll("[data-role-link]")) {
  link.hidden = !hasRole(link.dataset.roleLink);
}

document.querySelector("[data-logout]")?.addEventListener("click", () => {
  clearSession();
  location.href = "/";
});

for (const link of document.querySelectorAll(".nav a")) {
  if (link.pathname === location.pathname) {
    link.classList.add("active");
  }
}

/* ---------- Mobile sidebar toggle ---------- */
const sidebar = document.querySelector("[data-sidebar]");
const sidebarToggle = document.querySelector("[data-sidebar-toggle]");

sidebarToggle?.addEventListener("click", () => {
  sidebar?.classList.toggle("is-open");
});

/* close on nav item click (mobile) */
for (const link of document.querySelectorAll(".nav a")) {
  link.addEventListener("click", () => {
    if (window.matchMedia("(max-width: 820px)").matches) {
      sidebar?.classList.remove("is-open");
    }
  });
}

/* close on outside click (mobile) */
document.addEventListener("click", (event) => {
  if (!sidebar?.classList.contains("is-open")) return;
  if (!window.matchMedia("(max-width: 820px)").matches) return;
  const target = event.target;
  if (target instanceof Node && !sidebar.contains(target) && !sidebarToggle?.contains(target)) {
    sidebar.classList.remove("is-open");
  }
});

function translateRole(role) {
  if (role === "PlatformModerator") return "Модератор платформы";
  if (role === "User") return "Пользователь";
  return "Войдите в систему";
}
