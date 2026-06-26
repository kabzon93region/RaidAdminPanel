(function () {
  const API = "/raidadminpanel/api";
  let apiKey = localStorage.getItem("raidAdminApiKey") || "";

  const el = (id) => document.getElementById(id);

  function toast(msg, isError) {
    const t = el("toast");
    t.textContent = msg;
    t.classList.toggle("hidden", false);
    t.style.borderColor = isError ? "var(--danger)" : "var(--border)";
    setTimeout(() => t.classList.add("hidden"), 4000);
  }

  async function api(path, options) {
    const headers = Object.assign(
      { "X-RaidAdmin-Key": apiKey },
      options && options.headers ? options.headers : {}
    );
    const res = await fetch(API + path, Object.assign({}, options, { headers }));
    if (!res.ok) {
      let err = res.statusText;
      try {
        const j = await res.json();
        err = j.error || JSON.stringify(j);
      } catch (_) {}
      throw new Error(err);
    }
    if (res.status === 204) return null;
    const text = await res.text();
    return text ? JSON.parse(text) : null;
  }

  function renderStatus(data) {
    const cards = [
      { label: "Профилей", value: data.profileCount },
      { label: "Активны (15 мин)", value: data.activeProfiles },
      { label: "Рейдов Fika", value: data.activeRaids },
      { label: "Fika", value: data.fikaDetected ? "да" : "нет" },
      { label: "UTC", value: new Date(data.serverTimeUtc).toLocaleTimeString() }
    ];
    el("statusCards").innerHTML = cards
      .map(
        (c) =>
          `<div class="card"><div class="value">${c.value}</div><div class="label">${c.label}</div></div>`
      )
      .join("");
  }

  function renderRaids(raids) {
    if (!raids.length) {
      el("raidsTable").innerHTML = "<p class='hint'>Нет активных Fika-рейдов (или Fika не загружен).</p>";
      return;
    }
    const rows = raids
      .map(
        (r) => `<tr>
        <td><code>${r.matchId}</code></td>
        <td>${r.location || "—"}</td>
        <td>${r.playerCount}</td>
        <td>${r.headless ? "headless" : "host"}</td>
        <td>
          <button class="btn small primary" data-extract-match="${r.matchId}">Высадка всем</button>
          <button class="btn small danger" data-end-match="${r.matchId}">Закрыть сессию Fika</button>
        </td>
      </tr>`
      )
      .join("");
    el("raidsTable").innerHTML = `<table>
      <thead><tr><th>Match ID</th><th>Локация</th><th>Игроки</th><th>Тип</th><th>Действия</th></tr></thead>
      <tbody>${rows}</tbody></table>`;
  }

  function renderPlayers(players) {
    const rows = players
      .map(
        (p) => `<tr>
        <td>${p.nickname || "—"}</td>
        <td><code>${p.profileId}</code></td>
        <td>${p.level}</td>
        <td>${p.inRaid ? `<span class="badge ok">в рейде</span> ${p.location || ""}` : p.activeRecently ? "<span class='badge warn'>онлайн</span>" : "—"}</td>
        <td>
          <button class="btn small" data-save="${p.profileId}">Сохранить</button>
          <button class="btn small primary" data-extract="${p.profileId}">Extract</button>
          <button class="btn small" data-snapshot="${p.profileId}">Снимок лута</button>
        </td>
      </tr>`
      )
      .join("");
    el("playersTable").innerHTML = `<table>
      <thead><tr><th>Ник</th><th>Profile ID</th><th>Уровень</th><th>Статус</th><th>Действия</th></tr></thead>
      <tbody>${rows}</tbody></table>`;
  }

  function renderLogs(logs) {
    el("logBox").innerHTML = (logs || [])
      .map((l) => {
        const t = new Date(l.timestampUnix * 1000).toLocaleTimeString();
        return `<div class="log-line ${l.level}">[${t}] ${l.message}${l.details ? " — " + l.details : ""}</div>`;
      })
      .join("");
  }

  async function refresh() {
    const status = await api("/status");
    renderStatus(status);
    renderLogs(status.recentLogs);
    const raids = await api("/raids");
    renderRaids(raids || []);
    const players = await api("/players?activeMinutes=120");
    renderPlayers(players || []);
  }

  async function connect() {
    apiKey = el("apiKey").value.trim();
    localStorage.setItem("raidAdminApiKey", apiKey);
    await refresh();
    el("app").classList.remove("hidden");
    toast("Подключено");
  }

  document.addEventListener("click", async (e) => {
    const t = e.target;
    if (!(t instanceof HTMLElement)) return;

    if (t.dataset.action === "refresh") {
      try { await refresh(); toast("Обновлено"); } catch (err) { toast(err.message, true); }
    }
    if (t.dataset.action === "save-all") {
      if (!confirm("Сохранить ВСЕ профили на диск?")) return;
      try {
        const r = await api("/profiles/save-all", { method: "POST" });
        toast(`Сохранено профилей: ${r.saved}`);
        await refresh();
      } catch (err) { toast(err.message, true); }
    }
    if (t.dataset.save) {
      try {
        await api(`/profiles/${t.dataset.save}/save`, { method: "POST" });
        toast("Профиль сохранён");
      } catch (err) { toast(err.message, true); }
    }
    if (t.dataset.extract) {
      if (!confirm("Отправить команду extract (survived) на клиент игрока?")) return;
      try {
        await api(`/players/${t.dataset.extract}/force-extract`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ reason: "Admin panel" })
        });
        toast("Команда extract отправлена");
      } catch (err) { toast(err.message, true); }
    }
    if (t.dataset.snapshot) {
      try {
        await api(`/players/${t.dataset.snapshot}/request-inventory-snapshot`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ reason: "Admin snapshot" })
        });
        toast("Запрос снимка отправлен");
      } catch (err) { toast(err.message, true); }
    }
    if (t.dataset.extractMatch) {
      if (!confirm("Высадка ВСЕМ игрокам в рейде (живые выполнят extract на клиенте)?")) return;
      try {
        const r = await api("/raids/force-extract-all", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ matchId: t.dataset.extractMatch, includeDead: false, reason: "Admin mass extract" })
        });
        toast(`Команд в очереди: ${r.queued}`);
      } catch (err) { toast(err.message, true); }
    }
    if (t.dataset.endMatch) {
      if (!confirm("Закрыть Fika-сессию БЕЗ полного extract? Используйте только после высадки игроков.")) return;
      try {
        const r = await api(`/raids/${t.dataset.endMatch}/end-session`, { method: "POST" });
        toast(r.message || "Сессия закрыта");
        await refresh();
      } catch (err) { toast(err.message, true); }
    }
  });

  el("btnConnect").addEventListener("click", () => connect().catch((err) => toast(err.message, true)));
  if (apiKey) {
    el("apiKey").value = apiKey;
    connect().catch(() => {});
  }

  setInterval(() => {
    if (!el("app").classList.contains("hidden")) {
      refresh().catch(() => {});
    }
  }, 15000);
})();
