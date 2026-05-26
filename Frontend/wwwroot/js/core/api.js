import { clearSession, getSession } from "./session.js";

const apiBase = location.port === "3000" ? "http://localhost:8080" : "";

export async function api(path, options = {}) {
  const session = getSession();
  const headers = {
    "Content-Type": "application/json",
    "X-Correlation-Id": crypto.randomUUID(),
    ...(options.headers || {})
  };

  if (session.token) headers.Authorization = `Bearer ${session.token}`;

  const response = await fetch(`${apiBase}${path}`, { ...options, headers });
  if (response.status === 401) {
    clearSession();
    if (!isAuthPage()) {
      setTimeout(() => {
        location.href = "/";
      }, 500);
    }

    throw new Error("Сессия истекла. Войдите в аккаунт заново.");
  }

  if (response.status === 204) return null;

  const contentType = response.headers.get("content-type") || "";
  const payload = contentType.includes("json") ? await response.json() : await response.text();

  if (response.status === 403) {
    const code = `${payload?.code || payload?.title || payload?.error || ""}`;
    const detail = `${payload?.detail || payload?.message || ""}`.toLowerCase();

    if (code === "account_blocked" || detail.includes("blocked")) {
      const blockedMessage = payload?.detail || payload?.message || "Аккаунт заблокирован. Вход и действия в системе недоступны.";
      clearSession();
      if (!isAuthPage()) {
        setTimeout(() => {
          location.href = "/";
        }, 500);
      }

      throw new Error(blockedMessage);
    }

    const translatedForbidden = translateErrorText(`${code} ${payload?.detail || payload?.message || ""}`);
    throw new Error(translatedForbidden || "Недостаточно прав для выполнения действия.");
  }

  if (!response.ok) {
    const message = normalizeError(payload) || "Запрос не выполнен";
    throw new Error(message);
  }

  return payload;
}

export function toJson(method, body) {
  return {
    method,
    body: JSON.stringify(body)
  };
}

function normalizeError(payload) {
  if (!payload) return "";

  if (typeof payload === "string") {
    const parsedPayload = parseJsonError(payload);
    if (parsedPayload) return normalizeError(parsedPayload);

    const translated = translateErrorText(payload);
    if (translated) return translated;
    if (payload.includes("invalid_credentials")) return "Неверный логин или пароль";
    if (payload.includes("duplicate_username")) return "Логин уже занят";
    if (payload.includes("duplicate_email")) return "Email уже занят";
    if (payload.includes("account_blocked")) return "Аккаунт заблокирован";
    return payload.length <= 180 ? payload : "";
  }

  const code = payload.code || payload.title || payload.error || "";
  const translated = translateErrorText(`${code} ${payload.message || ""} ${payload.detail || ""}`);
  if (translated) return translated;

  if (payload.errors) {
    const validationText = Object.values(payload.errors).flat().join(" ");
    const validationMessage = translateErrorText(validationText);
    if (validationMessage) return validationMessage;
    if (validationText) return "Проверьте заполнение формы.";
  }

  if (code === "invalid_credentials") return "Неверный логин или пароль";
  if (code === "duplicate_username") return "Логин уже занят";
  if (code === "duplicate_email") return "Email уже занят";
  if (code === "account_blocked") return payload.message || payload.detail || "Аккаунт заблокирован";

  return payload.message || payload.detail || payload.title || payload.error || "";
}

function parseJsonError(payload) {
  const value = payload.trim();
  if (!value.startsWith("{") && !value.startsWith("[")) return null;

  try {
    return JSON.parse(value);
  } catch {
    return null;
  }
}

function translateErrorText(text) {
  const value = String(text || "").toLowerCase();
  if (!value) return "";

  if (value.includes("community name already exists") || value.includes("сообщество с таким названием")) {
    return "Сообщество с таким названием уже существует.";
  }

  if (value.includes("community username already exists") || value.includes("юзернейм сообщества уже занят")) {
    return "Юзернейм сообщества уже занят.";
  }

  if (value.includes("community username") || value.includes("username field is required") || value.includes("the username field is required")) {
    return "Укажите username сообщества: от 3 до 64 символов, латинские буквы, цифры, точка, дефис или нижнее подчеркивание.";
  }

  if (value.includes("the name field is required") || value.includes("name field is required")) {
    return "Укажите название.";
  }

  if (value.includes("the description field is required") || value.includes("description field is required")) {
    return "Заполните описание.";
  }

  if (value.includes("username must contain at least 3")) {
    return "Логин должен быть не короче 3 символов.";
  }

  if (value.includes("valid email is required")) {
    return "Введите корректный email.";
  }

  if (value.includes("password must contain at least 8")) {
    return "Пароль должен быть не короче 8 символов.";
  }

  if (value.includes("display name is required")) {
    return "Укажите отображаемое имя.";
  }

  if (value.includes("user was not found")) {
    return "Пользователь не найден.";
  }

  if (value.includes("block reason is required")) {
    return "Укажите причину блокировки.";
  }

  if (value.includes("moderator cannot block own account")) {
    return "Нельзя заблокировать собственный аккаунт модератора.";
  }

  if (value.includes("platform moderators cannot block other platform moderators")) {
    return "Нельзя заблокировать другого модератора платформы.";
  }

  if (value.includes("only post reports can delete a post")) {
    return "Удалить пост можно только по жалобе на пост.";
  }

  if (value.includes("community is blocked by platform moderation")) {
    return "Сообщество заблокировано модерацией платформы.";
  }

  if (value.includes("community status could not be changed")) {
    return "Не удалось изменить статус сообщества. Попробуйте еще раз.";
  }

  if (value.includes("membership limit") || value.includes("no more than 30") || value.includes("больше чем в 30 сообществах")) {
    return "Нельзя состоять больше чем в 30 сообществах.";
  }

  if (value.includes("community was not found") || value.includes("сообщество не найдено")) {
    return "Сообщество не найдено.";
  }

  if (value.includes("only community owner") || value.includes("только владелец сообщества")) {
    return "Это действие доступно только владельцу сообщества.";
  }

  if (value.includes("post is available only to approved community members")) {
    return "Посты закрытого сообщества видны только участникам после одобрения заявки владельцем.";
  }

  if (value.includes("сообщество закрытое") || value.includes("closed community")) {
    return "Сообщество закрытое. Отправьте заявку владельцу и дождитесь одобрения.";
  }

  if (value.includes("заявка на вступление уже рассмотрена")) {
    return "Эта заявка уже рассмотрена.";
  }

  return "";
}

function isAuthPage() {
  return location.pathname === "/" || location.pathname === "/Register";
}
