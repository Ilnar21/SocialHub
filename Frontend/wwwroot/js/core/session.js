export function getSession() {
  return {
    token: localStorage.getItem("socialhub.token") || "",
    user: readJson("socialhub.user")
  };
}

export function saveSession(auth) {
  localStorage.setItem("socialhub.token", auth.token);
  localStorage.setItem("socialhub.user", JSON.stringify(auth.user));
}

export function clearSession() {
  localStorage.removeItem("socialhub.token");
  localStorage.removeItem("socialhub.user");
}

function readJson(key) {
  try {
    return JSON.parse(localStorage.getItem(key) || "null");
  } catch {
    return null;
  }
}
