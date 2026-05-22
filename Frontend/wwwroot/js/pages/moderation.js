import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formData, formatDate, shortId } from "../core/dom.js";
import { isPlatformModerator } from "../core/session.js";
import { toast } from "../core/toast.js";

const userSelect = document.querySelector("[data-user-select]");
const reportsList = document.querySelector("[data-reports-list]");
const auditList = document.querySelector("[data-audit-list]");

if (!isPlatformModerator()) {
  document.querySelector(".page-header p").textContent = "Этот раздел доступен только модераторам платформы.";
  document.querySelector(".split").innerHTML = `
    <section class="panel empty">
      <h2>Доступ запрещен</h2>
      <p>Для просмотра жалоб, блокировок и аудита нужна роль модератора платформы.</p>
      <a class="button secondary" href="/Feed">Вернуться в ленту</a>
    </section>`;
} else {
  document.querySelector("[data-load-moderation]")?.addEventListener("click", loadModeration);
  document.querySelector('[data-form="create-report"]')?.addEventListener("submit", async (event) => {
    event.preventDefault();

    try {
      await api("/api/reports", toJson("POST", formData(event.currentTarget)));
      event.currentTarget.reset();
      toast("Жалоба создана");
      await loadReports();
    } catch (error) {
      toast(error.message, "error");
    }
  });

  document.querySelector('[data-form="block-user"]')?.addEventListener("submit", async (event) => {
    event.preventDefault();
    const data = formData(event.currentTarget);

    try {
      await api(`/api/users/${data.userId}/blocks`, toJson("POST", {
        durationDays: Number(data.durationDays),
        reason: data.reason
      }));
      toast("Пользователь заблокирован");
      await loadAudit();
    } catch (error) {
      toast(error.message, "error");
    }
  });

  await loadUsers();
  await loadModeration();
}

async function loadModeration() {
  await Promise.all([loadReports(), loadAudit()]);
}

async function loadUsers() {
  try {
    const users = await api("/api/users/");
    userSelect.innerHTML = users
      .map((user) => `<option value="${user.id}">${escapeHtml(user.profile?.displayName || user.username)}</option>`)
      .join("");
  } catch (error) {
    userSelect.innerHTML = `<option>${escapeHtml(error.message)}</option>`;
  }
}

async function loadReports() {
  reportsList.innerHTML = empty("Загружаем жалобы...");
  try {
    const reports = await api("/api/reports?status=NEW");
    reportsList.innerHTML = reports.length ? reports.map(renderReport).join("") : empty("Новых жалоб нет.");
    for (const button of document.querySelectorAll("[data-delete-reported-post]")) {
      button.addEventListener("click", () => resolveReport(button.dataset.deleteReportedPost));
    }
  } catch (error) {
    reportsList.innerHTML = empty(error.message);
  }
}

async function loadAudit() {
  auditList.innerHTML = empty("Загружаем аудит...");
  try {
    const entries = await api("/api/audit");
    auditList.innerHTML = entries.length ? entries.map(renderAudit).join("") : empty("Записей аудита пока нет.");
  } catch (error) {
    auditList.innerHTML = empty(error.message);
  }
}

async function resolveReport(reportId) {
  try {
    await api(`/api/reports/${reportId}/resolve/delete-post`, toJson("POST", { comment: "Удалено через панель модерации" }));
    toast("Жалоба обработана");
    await loadModeration();
  } catch (error) {
    toast(error.message, "error");
  }
}

function renderReport(report) {
  return `
    <article class="card">
      <div class="row">
        <h2>${escapeHtml(report.reason)}</h2>
        <span class="badge warn">${escapeHtml(report.status)}</span>
      </div>
      <p>${escapeHtml(report.comment ?? "")}</p>
      <div class="meta">
        <span>${escapeHtml(report.targetType)} ${shortId(report.targetId)}</span>
        <span>${formatDate(report.createdAtUtc)}</span>
      </div>
      <div class="actions">
        <button class="button danger" data-delete-reported-post="${report.id}">Удалить пост</button>
      </div>
    </article>`;
}

function renderAudit(entry) {
  return `
    <article class="card">
      <div class="row">
        <h2>${escapeHtml(entry.action)}</h2>
        <span class="badge">${escapeHtml(entry.actorRole ?? "MODERATOR")}</span>
      </div>
      <p>${escapeHtml(entry.reason ?? "")}</p>
      <div class="meta">
        <span>${escapeHtml(entry.targetType)} ${shortId(entry.targetId)}</span>
        <span>${formatDate(entry.createdAtUtc)}</span>
      </div>
    </article>`;
}
