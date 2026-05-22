import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formData, formatDate, shortId } from "../core/dom.js";
import { getSession } from "../core/session.js";
import { toast } from "../core/toast.js";

const userSelect = document.querySelector("[data-user-select]");
const dialogsList = document.querySelector("[data-dialogs-list]");
const messagesList = document.querySelector("[data-messages-list]");

document.querySelector("[data-load-dialogs]")?.addEventListener("click", loadDialogs);
document.querySelector('[data-form="send-message"]')?.addEventListener("submit", async (event) => {
  event.preventDefault();
  const data = formData(event.currentTarget);

  if ((data.text || "").length > 4000) {
    toast("Сообщение не должно быть длиннее 4000 символов", "error");
    return;
  }

  try {
    const response = await api(`/api/dialogs/${data.recipientUserId}/messages`, toJson("POST", { text: data.text }));
    event.currentTarget.reset();
    toast("Сообщение отправлено");
    await loadDialogs();
    await loadMessages(response.dialogId);
  } catch (error) {
    toast(error.message, "error");
  }
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
    userSelect.innerHTML = `<option>${escapeHtml(error.message)}</option>`;
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
  return `
    <article class="card">
      <div class="row">
        <h2>Диалог ${shortId(dialog.dialogId ?? dialog.id)}</h2>
        <button class="button secondary" data-open-dialog="${dialog.dialogId ?? dialog.id}">Открыть</button>
      </div>
      <p>${escapeHtml(dialog.lastMessageText ?? dialog.lastMessage?.text ?? "Нет сообщений")}</p>
      <div class="meta">
        <span>${formatDate(dialog.lastMessageAtUtc ?? dialog.updatedAtUtc)}</span>
      </div>
    </article>`;
}

function renderMessage(message) {
  const mine = message.senderUserId === getSession().user?.id;
  return `
    <article class="message ${mine ? "mine" : ""}">
      <strong>${mine ? "Вы" : "Собеседник"}</strong>
      <p>${escapeHtml(message.text)}</p>
      <small>${formatDate(message.sentAtUtc ?? message.createdAtUtc)}</small>
    </article>`;
}
