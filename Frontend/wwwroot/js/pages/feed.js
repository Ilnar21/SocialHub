import { api, toJson } from "../core/api.js";
import { empty, escapeHtml, formatDate, shortId } from "../core/dom.js";
import { toast } from "../core/toast.js";

const list = document.querySelector("[data-feed-list]");

document.querySelector("[data-refresh-feed]")?.addEventListener("click", async (event) => {
  await runWithButton(event.currentTarget, "Обновляем...", async () => {
    await api("/feed/refresh", toJson("POST", {}));
    toast("Лента синхронизирована");
    await loadFeed();
  });
});

loadFeed();

async function loadFeed() {
  list.innerHTML = empty("Загружаем ленту...");
  try {
    const response = await api("/feed?page=1&limit=20");
    const items = response.items ?? response.posts ?? [];
    list.innerHTML = items.length ? items.map(renderPost).join("") : empty("Подпишитесь на сообщества, чтобы увидеть ленту.");
    bindReportButtons();
  } catch (error) {
    list.innerHTML = empty(error.message);
  }
}

function renderPost(post) {
  return `
    <article class="card">
      <div class="row">
        <h2>${escapeHtml(post.title)}</h2>
        <span class="badge">score ${post.score ?? post.likes ?? 0}</span>
      </div>
      <p>${escapeHtml(post.previewText ?? post.text ?? "")}</p>
      <div class="meta">
        <span>Пост ${shortId(post.postId ?? post.id)}</span>
        <span>Сообщество ${shortId(post.communityId)}</span>
        <span>${formatDate(post.createdAt ?? post.createdAtUtc)}</span>
      </div>
      <div class="actions">
        <button class="button danger" data-report-post="${escapeHtml(post.postId ?? post.id)}">Пожаловаться</button>
      </div>
    </article>`;
}

function bindReportButtons() {
  for (const button of document.querySelectorAll("[data-report-post]")) {
    button.addEventListener("click", async () => {
      await runWithButton(button, "Отправляем...", async () => {
        await api("/api/reports", toJson("POST", {
          targetType: "POST",
          targetId: button.dataset.reportPost,
          reason: "Спам",
          comment: "Жалоба из ленты"
        }));
        toast("Жалоба отправлена");
      });
    });
  }
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
