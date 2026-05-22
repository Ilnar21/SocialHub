import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formData, formatDate, shortId } from "../core/dom.js";
import { getSession } from "../core/session.js";
import { toast } from "../core/toast.js";

const userSelect = document.querySelector("[data-user-select]");
const dialogsList = document.querySelector("[data-dialogs-list]");
const messagesList = document.querySelector("[data-messages-list]");

let activeDialogId = "";

document.querySelector("[data-load-dialogs]")?.addEventListener("click", loadDialogs);
document.querySelector('[data-form="send-message"]')?.addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  const button = form.querySelector('button[type="submit"]');
  const data = formData(form);

  if ((data.text || "").length > 4000) {
    toast("Сообщение не должно быть длиннее 4000 символов", "error");
    return;
  }

  await runWithButton(button, "Отправляем...", async () => {
    const response = await api(`/api/dialogs/${data.recipientUserId}/messages`, toJson("POST", { text: data.text }));
    const dialogId = response.dialogId ?? response.DialogId;
    form.reset();
    toast("Сообщение отправлено");
    await loadDialogs();
    if (dialogId) {
      await loadMessages(dialogId);
    }
  });
});

await loadUsers();
await loadDialogs();

async function loadUsers() {
  const currentUserId = getSession().user?.id;
  try {
    const users = await api("/api/users/");
    userSelect.innerHTML = users
      .filter((user) => user.id !== currentUserId)
      .map((user) => `<option value="${user.id}">${escapeHtml(user.profile?.displayName || user.username)}</option>`)
      .join("");
  } catch (error) {
    userSelect.innerHTML = `<option value="">${escapeHtml(error.message)}</option>`;
  }
}

async function loadDialogs() {
  dialogsList.innerHTML = empty("Загружаем диалоги...");
  try {
    const dialogs = await api("/api/dialogs");
    dialogsList.innerHTML = dialogs.length ? dialogs.map(renderDialog).join("") : empty("Диалогов пока нет.");
    for (const button of document.querySelectorAll("[data-open-dialog]")) {
      button.addEventListener("click", () => loadMessages(button.dataset.openDialog));
    }
  } catch (error) {
    dialogsList.innerHTML = empty(error.message);
  }
}

async function loadMessages(dialogId) {
  activeDialogId = dialogId;
  messagesList.innerHTML = empty("Загружаем историю...");
  try {
    const response = await api(`/api/dialogs/${dialogId}/messages?skip=0&limit=100`);
    const messages = response.items ?? response.messages ?? [];
    messagesList.innerHTML = messages.length ? messages.map(renderMessage).join("") : empty("В диалоге пока нет сообщений.");
    messagesList.scrollTop = messagesList.scrollHeight;
  } catch (error) {
    messagesList.innerHTML = empty(error.message);
  }
}

function renderDialog(dialog) {
  const dialogId = dialog.dialogId ?? dialog.id;
  const isActive = dialogId === activeDialogId ? " active-card" : "";
  return `
    <article class="card${isActive}">
      <div class="row">
        <h2>Диалог ${shortId(dialogId)}</h2>
        <button class="button secondary" data-open-dialog="${dialogId}">Открыть</button>
      </div>
      <p>${escapeHtml(dialog.lastMessagePreview ?? dialog.lastMessageText ?? dialog.lastMessage?.text ?? "Нет сообщений")}</p>
      <div class="meta">
        <span>${formatDate(dialog.lastMessageAt ?? dialog.lastMessageAtUtc ?? dialog.updatedAtUtc)}</span>
      </div>
    </article>`;
}

function renderMessage(message) {
  const mine = message.senderUserId === getSession().user?.id;
  return `
    <article class="message ${mine ? "mine" : ""}">
      <strong>${mine ? "Вы" : "Собеседник"}</strong>
      <p>${escapeHtml(message.text)}</p>
      <small>${formatDate(message.sentAt ?? message.sentAtUtc ?? message.createdAtUtc)}</small>
    </article>`;
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
