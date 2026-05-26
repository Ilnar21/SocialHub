import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formData, formatDate } from "../core/dom.js";
import { preloadUsers, userDisplayName, userProfileHref, userUsername } from "../core/identity.js";
import { getSession } from "../core/session.js";
import { toast } from "../core/toast.js";

const recipientSearch = document.querySelector("[data-recipient-search]");
const activeTitle = document.querySelector("[data-active-chat-title]");
const activeSubtitle = document.querySelector("[data-active-chat-subtitle]");
const dialogsList = document.querySelector("[data-dialogs-list]");
const messagesList = document.querySelector("[data-messages-list]");
const sendForm = document.querySelector('[data-form="send-message"]');
const sendButton = sendForm?.querySelector('button[type="submit"]');
const messageText = sendForm?.querySelector('textarea[name="text"]');
const presetRecipientUsername = new URLSearchParams(location.search).get("recipientUsername") || "";

let activeDialogId = "";
let activeRecipientId = "";
let dialogsCache = [];

document.querySelector("[data-load-dialogs]")?.addEventListener("click", async (event) => {
  await runWithButton(event.currentTarget, "Обновляем...", loadDialogs);
});

document.querySelector('[data-form="open-recipient"]')?.addEventListener("submit", async (event) => {
  event.preventDefault();
  const data = formData(event.currentTarget);
  await openRecipientByUsername(data.recipientUsername);
});

sendForm?.addEventListener("submit", async (event) => {
  event.preventDefault();
  const data = formData(event.currentTarget);

  if (!activeRecipientId) {
    toast("Откройте чат с пользователем по username.", "error");
    return;
  }

  if ((data.text || "").length > 4000) {
    toast("Сообщение не должно быть длиннее 4000 символов.", "error");
    return;
  }

  await runWithButton(sendButton, "Отправляем...", async () => {
    const response = await api(`/api/dialogs/${activeRecipientId}/messages`, toJson("POST", { text: data.text }));
    const dialogId = response.dialogId ?? response.DialogId;
    messageText.value = "";
    toast("Сообщение отправлено.");
    await loadDialogs();
    if (dialogId) {
      await openDialog(dialogId);
    }
  });
});

await loadDialogs();

if (presetRecipientUsername) {
  await openRecipientByUsername(presetRecipientUsername);
} else {
  renderEmptyChat("Выберите диалог или найдите собеседника по username.");
}

async function loadDialogs() {
  dialogsList.innerHTML = empty("Загружаем диалоги...");
  try {
    dialogsCache = await api("/api/dialogs");
    await preloadUsers(dialogsCache.flatMap((dialog) => dialog.participantUserIds ?? []));
    renderDialogs();

    if (activeDialogId && dialogsCache.some((dialog) => dialogIdOf(dialog) === activeDialogId)) {
      await loadMessages(activeDialogId);
    }
  } catch (error) {
    dialogsList.innerHTML = empty(error.message);
  }
}

function renderDialogs() {
  dialogsList.innerHTML = dialogsCache.length
    ? dialogsCache.map(renderDialog).join("")
    : empty("Диалогов пока нет.");

  for (const button of dialogsList.querySelectorAll("[data-open-dialog]")) {
    button.addEventListener("click", () => openDialog(button.dataset.openDialog));
  }
}

async function openRecipientByUsername(rawUsername) {
  const username = normalizeUsername(rawUsername);
  if (!username) {
    toast("Введите username пользователя.", "error");
    return;
  }

  const user = await api(`/api/users/by-username/${encodeURIComponent(username)}`);
  await preloadUsers([user.id]);
  await openRecipientUser(user);
}

async function openRecipientUser(user) {
  const recipientId = user?.id || "";
  const currentUserId = getSession().user?.id;

  if (!recipientId || !user?.username) {
    toast("Пользователь не найден.", "error");
    return;
  }

  if (recipientId === currentUserId) {
    toast("Нельзя открыть чат с самим собой.", "error");
    return;
  }

  await preloadUsers([recipientId]);
  const existingDialog = dialogsCache.find((dialog) => (dialog.participantUserIds ?? []).includes(recipientId));

  activeRecipientId = recipientId;
  recipientSearch.value = `@${user.username}`;
  setComposerEnabled(true);

  if (existingDialog) {
    await openDialog(dialogIdOf(existingDialog));
    return;
  }

  activeDialogId = "";
  renderDialogs();
  updateActiveHeader(recipientId, "");
  renderEmptyChat("Переписки пока нет. Она появится после первого отправленного сообщения.");
  messageText?.focus();
}

async function openDialog(dialogId) {
  const dialog = dialogsCache.find((item) => dialogIdOf(item) === dialogId);
  activeDialogId = dialogId;
  activeRecipientId = dialog ? otherParticipantId(dialog) : activeRecipientId;
  recipientSearch.value = userUsername(activeRecipientId) ? `@${userUsername(activeRecipientId)}` : "";
  setComposerEnabled(Boolean(activeRecipientId));
  updateActiveHeader(activeRecipientId, dialogId);
  renderDialogs();
  await loadMessages(dialogId);
}

async function loadMessages(dialogId) {
  messagesList.innerHTML = empty("Загружаем историю...");
  try {
    const response = await api(`/api/dialogs/${dialogId}/messages?skip=0&limit=100`);
    const messages = response.items ?? response.messages ?? [];
    await preloadUsers(messages.map((message) => message.senderUserId));
    messagesList.innerHTML = messages.length ? messages.map(renderMessage).join("") : empty("В диалоге пока нет сообщений.");
    messagesList.scrollTop = messagesList.scrollHeight;
  } catch (error) {
    messagesList.innerHTML = empty(error.message);
  }
}

function renderDialog(dialog) {
  const dialogId = dialogIdOf(dialog);
  const otherId = otherParticipantId(dialog);
  const isActive = dialogId === activeDialogId ? " active-card" : "";
  const name = userDisplayName(otherId);
  const username = userUsername(otherId);

  return `
    <button class="dialog-card${isActive}" type="button" data-open-dialog="${dialogId}">
      <span class="dialog-avatar">${name.trim().slice(0, 1).toUpperCase() || "U"}</span>
      <span>
        <strong>${escapeHtml(name)}</strong>
        <small>${escapeHtml(dialog.lastMessagePreview ?? dialog.lastMessageText ?? dialog.lastMessage?.text ?? "Нет сообщений")}</small>
        <small>@${escapeHtml(username || "user")} · ${formatDate(dialog.lastMessageAt ?? dialog.lastMessageAtUtc ?? dialog.updatedAtUtc)}</small>
      </span>
    </button>`;
}

function renderMessage(message) {
  const mine = message.senderUserId === getSession().user?.id;
  const senderName = mine ? "Вы" : userDisplayName(message.senderUserId);
  return `
    <article class="message ${mine ? "mine" : ""}">
      <strong>${escapeHtml(senderName)}</strong>
      <p>${escapeHtml(message.text)}</p>
      <small>${formatDate(message.sentAt ?? message.sentAtUtc ?? message.createdAtUtc)}</small>
    </article>`;
}

function renderEmptyChat(text) {
  messagesList.innerHTML = empty(text);
}

function updateActiveHeader(recipientId, dialogId) {
  const name = userDisplayName(recipientId);
  const href = userProfileHref(recipientId);
  const username = userUsername(recipientId);
  activeTitle.innerHTML = href
    ? `<a href="${escapeHtml(href)}">${escapeHtml(name)}</a>`
    : escapeHtml(name);
  activeSubtitle.textContent = dialogId
    ? `@${username}`
    : `Новый чат с @${username}`;
}

function setComposerEnabled(enabled) {
  messageText.disabled = !enabled;
  sendButton.disabled = !enabled;
}

function dialogIdOf(dialog) {
  return dialog.dialogId ?? dialog.id;
}

function otherParticipantId(dialog) {
  const currentUserId = getSession().user?.id;
  return (dialog.participantUserIds ?? []).find((id) => id !== currentUserId) || "";
}

function normalizeUsername(value) {
  return String(value || "").trim().replace(/^@/, "").toLowerCase();
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
