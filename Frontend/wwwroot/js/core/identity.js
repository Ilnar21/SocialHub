import { api } from "./api.js";

const userCache = new Map();

export async function preloadUsers(userIds) {
  const uniqueIds = [...new Set(userIds.filter(Boolean))].filter((id) => !userCache.has(id));
  await Promise.all(uniqueIds.map(async (id) => {
    try {
      userCache.set(id, await api(`/api/users/${id}`));
    } catch {
      userCache.set(id, null);
    }
  }));
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
