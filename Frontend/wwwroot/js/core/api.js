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
  const payload = contentType.includes("application/json") ? await response.json() : await response.text();

  if (response.status === 403) {
    const code = `${payload?.code || payload?.title || payload?.error || ""}`;
    const detail = `${payload?.detail || payload?.message || ""}`.toLowerCase();

    if (code === "account_blocked" || detail.includes("blocked")) {
      clearSession();
      if (!isAuthPage()) {
        setTimeout(() => {
          location.href = "/";
        }, 500);
      }

      throw new Error("Аккаунт заблокирован. Вход и действия в системе недоступны.");
    }

    throw new Error("Недостаточно прав для выполнения действия.");
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
    if (payload.includes("invalid_credentials")) return "Неверный логин или пароль";
    if (payload.includes("duplicate_username")) return "Логин уже занят";
    if (payload.includes("duplicate_email")) return "Email уже занят";
    if (payload.includes("account_blocked")) return "Аккаунт заблокирован";
    return payload.length <= 180 ? payload : "";
  }

  const code = payload.code || payload.title || payload.error || "";
  if (code === "invalid_credentials") return "Неверный логин или пароль";
  if (code === "duplicate_username") return "Логин уже занят";
  if (code === "duplicate_email") return "Email уже занят";
  if (code === "account_blocked") return "Аккаунт заблокирован";

  return payload.message || payload.detail || payload.title || payload.error || "";
}

function isAuthPage() {
  return location.pathname === "/" || location.pathname === "/Register";
}
