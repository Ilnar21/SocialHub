import { api, toJson } from "../core/api.js";
import { formData } from "../core/dom.js";
import { saveSession } from "../core/session.js";
import { toast } from "../core/toast.js";

document.querySelector('[data-form="login"]')?.addEventListener("submit", async (event) => {
  event.preventDefault();
  await submitLogin(event.currentTarget);
});

document.querySelector('[data-form="register"]')?.addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  const data = formData(form);

  try {
    await api("/api/auth/register", toJson("POST", {
      username: data.username,
      email: data.email,
      password: data.password,
      displayName: data.displayName,
      bio: data.bio,
      avatarUrl: ""
    }));

    await submitLogin(form, data.username, data.password);
  } catch (error) {
    toast(error.message, "error");
  }
});

async function submitLogin(form, username = null, password = null) {
  const button = form.querySelector('button[type="submit"]');
  const originalText = button?.textContent;

  if (button) {
    button.disabled = true;
    button.textContent = "Проверяем...";
  }

  try {
    const data = formData(form);
    const auth = await api("/api/auth/login", toJson("POST", {
      usernameOrEmail: username ?? data.usernameOrEmail,
      password: password ?? data.password
    }));

    saveSession(auth);
    location.href = "/Feed";
  } catch (error) {
    toast(error.message, "error");
  } finally {
    if (button) {
      button.disabled = false;
      button.textContent = originalText;
    }
  }
}
