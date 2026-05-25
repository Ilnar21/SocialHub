import { api } from "./api.js";

const userCache = new Map();

export async function preloadUsers(userIds) {
  const uniqueIds = [...new Set(userIds.filter(Boolean))].filter((id) => !userCache.has(id));
  await Promise.all(uniqueIds.map(async (id) => {
    try {
      cacheUser(await api(`/api/users/${id}`));
    } catch {
      userCache.set(id, null);
    }
  }));
}

export async function findUserByUsername(username) {
  const normalized = normalizeUsername(username);
  if (!normalized) return null;

  const cached = [...userCache.values()]
    .filter(Boolean)
    .find((user) => normalizeUsername(user.username) === normalized);

  if (cached) {
    return cached;
  }

  const user = await api(`/api/users/by-username/${encodeURIComponent(normalized)}`);
  cacheUser(user);
  return user;
}

export function userDisplayName(userId) {
  const user = userCache.get(userId);
  return user?.profile?.displayName || user?.username || "Пользователь";
}

export function userUsername(userId) {
  return userCache.get(userId)?.username || "";
}

export function userProfileHref(userId) {
  const username = userUsername(userId);
  return username ? `/UserProfile?username=${encodeURIComponent(username)}` : "";
}

function cacheUser(user) {
  if (user?.id) {
    userCache.set(user.id, user);
  }
}

function normalizeUsername(value) {
  return String(value || "").trim().replace(/^@/, "").toLowerCase();
}
