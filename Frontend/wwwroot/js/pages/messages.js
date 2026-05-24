import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formData, formatDate, shortId } from "../core/dom.js";
import { getSession } from "../core/session.js";
import { toast } from "../core/toast.js";

const userSelect = document.querySelector("[data-user-select]");
const dialogsList = document.querySelector("[data-dialogs-list]");
const messagesList = document.querySelector("[data-messages-list]");
const sendButton = document.querySelector('[data-form="send-message"] button[type="submit"]');
const messageText = document.querySelector('[data-form="send-message"] textarea[name="text"]');
const presetRecipientId = new URLSearchParams(location.search).get("recipientUserId") || "";

let activeDialogId = "";
let dialogsCache = [];

document.querySelector("[data-load-dialogs]")?.addEventListener("click", loadDialogs);
document.querySelector('[data-form="send-message"]')?.addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  const button = form.querySelector('button[type="submit"]');
  const data = formData(form);

  if (!data.recipientUserId) {
    toast("Выберите получателя.", "error");
    return;
  }

  if ((data.text || "").length > 4000) {
    toast("Сообщение не должно быть длиннее 4000 символов", "error");
    return;
  }

  await runWithButton(button, "Отправляем...", async () => {
    const response = await api(`/api/dialogs/${data.recipientUserId}/messages`, toJson("POST", { text: data.text }));
    const dialogId = response.dialogId ?? response.DialogId;
    form.reset();
    userSelect.value = data.recipientUserId;
    toast("Сообщение отправлено.");
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
    const availableUsers = users
      .filter((user) => user.id !== currentUserId)
      .filter((user) => user.status !== "Blocked" && user.status !== "BLOCKED")
      .filter((user) => user.role !== "PlatformModerator");

    userSelect.innerHTML = availableUsers
      .map((user) => `<option value="${user.id}">${escapeHtml(user.profile?.displayName || user.username)}</option>`)
      .join("");

    const hasRecipients = availableUsers.length > 0;
    userSelect.disabled = !hasRecipients;
    sendButton.disabled = !hasRecipients;
    if (!hasRecipients) {
      userSelect.innerHTML = '<option value="">Нет доступных получателей</option>';
      return;
    }

    if (presetRecipientId && availableUsers.some((user) => user.id === presetRecipientId)) {
      userSelect.value = presetRecipientId;
      messageText?.focus();
    }
  } catch (error) {
    userSelect.innerHTML = `<option value="">${escapeHtml(error.message)}</option>`;
    userSelect.disabled = true;
    sendButton.disabled = true;
  }
}

async function loadDialogs() {
  dialogsList.innerHTML = empty("Загружаем диалоги...");
  try {
    const dialogs = await api("/api/dialogs");
    dialogsCache = dialogs;
    dialogsList.innerHTML = dialogs.length ? dialogs.map(renderDialog).join("") : empty("Диалогов пока нет.");
    for (const button of document.querySelectorAll("[data-open-dialog]")) {
      button.addEventListener("click", () => loadMessages(button.dataset.openDialog));
    }

    if (activeDialogId && dialogs.some((dialog) => (dialog.dialogId ?? dialog.id) === activeDialogId)) {
      await loadMessages(activeDialogId);
      return;
    }

    if (!activeDialogId && presetRecipientId) {
      const existingDialog = dialogs.find((dialog) => (dialog.participantUserIds ?? []).includes(presetRecipientId));
      if (existingDialog) {
        await loadMessages(existingDialog.dialogId ?? existingDialog.id);
      } else {
        messagesList.innerHTML = empty("Диалога пока нет. Напишите первое сообщение выбранному пользователю.");
        messageText?.focus();
      }
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
  const participants = dialog.participantUserIds ?? [];
  return `
    <article class="card${isActive}">
      <div class="row">
        <h2>Диалог ${shortId(dialogId)}</h2>
        <button class="button secondary" data-open-dialog="${dialogId}">Открыть</button>
      </div>
      <p>${escapeHtml(dialog.lastMessagePreview ?? dialog.lastMessageText ?? dialog.lastMessage?.text ?? "Нет сообщений")}</p>
      <div class="meta">
        <span>Участники: ${participants.map(shortId).join(" · ")}</span>
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
