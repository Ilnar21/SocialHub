import { getSession } from "./session.js";

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
  if (response.status === 204) return null;

  const contentType = response.headers.get("content-type") || "";
  const payload = contentType.includes("application/json") ? await response.json() : await response.text();

  if (!response.ok) {
    const message = payload?.message || payload?.title || payload?.error || "Запрос не выполнен";
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
