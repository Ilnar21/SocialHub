import { clearSession, getSession } from "./core/session.js";

const session = getSession();
const userName = document.querySelector("[data-user-name]");
const userRole = document.querySelector("[data-user-role]");

if (!session.token && !location.pathname.match(/^\/(Register)?$/)) {
  location.href = "/";
}

if (userName && userRole) {
  userName.textContent = session.user?.profile?.displayName || session.user?.username || "Гость";
  userRole.textContent = session.user?.role || "Войдите в систему";
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
