import { api, toJson } from "./api.js";
import { escapeHtml } from "./dom.js";
import { toast } from "./toast.js";

const reportReasons = [
  "Спам или массовая реклама",
  "Оскорбления или травля",
  "Запрещенный контент",
  "Ложная информация",
  "Другая причина"
];

let modalElement = null;
let activeTarget = null;

export function bindReportButtons(root = document) {
  ensureReportModal();

  for (const button of root.querySelectorAll("[data-report-target-type][data-report-target-id]")) {
    if (button.dataset.reportBound === "true") continue;
    button.dataset.reportBound = "true";
    button.addEventListener("click", (event) => {
      event.preventDefault();
      event.stopPropagation();
      openReportDialog(button);
    });
  }
}

function ensureReportModal() {
  if (modalElement) return;

  modalElement = document.createElement("section");
  modalElement.className = "report-modal";
  modalElement.hidden = true;
  modalElement.innerHTML = `
    <div class="report-modal-backdrop" data-report-close></div>
    <form class="report-dialog" data-report-form>
      <div class="row">
        <h2>Пожаловаться</h2>
        <button class="button secondary" type="button" data-report-close>Закрыть</button>
      </div>
      <p class="muted" data-report-target-label></p>
      <label>Причина
        <select name="reason" required>
          ${reportReasons.map((reason) => `<option value="${escapeHtml(reason)}">${escapeHtml(reason)}</option>`).join("")}
        </select>
      </label>
      <label>Комментарий
        <textarea name="comment" maxlength="1000" placeholder="Можно оставить пустым"></textarea>
      </label>
      <div class="report-actions">
        <button class="button secondary" type="button" data-report-close>Отмена</button>
        <button class="button danger" type="submit">Отправить жалобу</button>
      </div>
    </form>`;

  document.body.appendChild(modalElement);

  for (const closeButton of modalElement.querySelectorAll("[data-report-close]")) {
    closeButton.addEventListener("click", closeReportDialog);
  }

  modalElement.querySelector("[data-report-form]")?.addEventListener("submit", submitReport);
}

function openReportDialog(button) {
  activeTarget = {
    targetType: button.dataset.reportTargetType,
    targetId: button.dataset.reportTargetId,
    label: button.dataset.reportTargetLabel || "Выбранный объект"
  };

  modalElement.querySelector("[data-report-target-label]").textContent = activeTarget.label;
  modalElement.querySelector("[data-report-form]").reset();
  modalElement.hidden = false;
  modalElement.querySelector("select")?.focus();
}

function closeReportDialog() {
  activeTarget = null;
  if (modalElement) {
    modalElement.hidden = true;
  }
}

async function submitReport(event) {
  event.preventDefault();
  if (!activeTarget) return;

  const form = event.currentTarget;
  const button = form.querySelector('button[type="submit"]');
  const originalText = button.textContent;

  button.disabled = true;
  button.textContent = "Отправляем...";
  try {
    await api("/api/reports", toJson("POST", {
      targetType: activeTarget.targetType,
      targetId: activeTarget.targetId,
      reason: form.reason.value,
      comment: form.comment.value || null
    }));
    toast("Жалоба отправлена модерации.");
    closeReportDialog();
  } catch (error) {
    toast(error.message, "error");
  } finally {
    button.disabled = false;
    button.textContent = originalText;
  }
}
