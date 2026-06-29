# Changelog — Raid Admin Panel

## 1.0.23 — 2026-06-29

### Hotfix — F8 diagnostics disabled by default

- Добавлена опция `Debug > EnableF8Diagnostics` (по умолчанию `false`).
- Harmony-патч `CoopHandlerExtractPatch` (рефлексия на `Fika.Core.Main.Components.CoopHandler.ProcessQuitting`) **не применяется** при выключенной опции.
- Это устраняет лаги, которые давала F8-диагностика (добавлена в 1.0.21 для массового extract).
- Индивидуальный extract работает без этой диагностики. Включайте `EnableF8Diagnostics = true` только при проблемах с массовым выходом.

## 1.0.22 — 2026-06-29

### Hotfix — PollLoop diagnostic toggle

- Добавлена опция `Debug > PollEnabled` (по умолчанию `true`).
- При `false` — `AdminCommandPoller.PollLoop` (HTTP poll `/raidadminpanel/client/commands/poll` каждые 3 сек + verbose логи) **не запускается**.
- Используется для временной диагностики лагов: если FPS восстанавливается — проблема в poll-цикле RaidAdminPanel.

## 1.0.21 — 2026-06-28

### Клиент — F8 / extract diagnostics

- Harmony-патч `CoopHandler.ProcessQuitting`: лог `[RaidAdminPanel.Extract]` при нажатии F8 с `quitState`, `requestQuit`, `extractedWaiting`, причиной блокировки.

### Сервер — PitFire compat

- Stub-маршрут `/singleplayer/pitfireteam/teammate/raid-outcomes` (err=0) — убирает 404 при выходе из рейда с PitFire Team.

## 1.0.20 — 2026-06-28

### Клиент — усиленный force extract (Fika coop)

- Учитывает **экран ожидания F8**: берёт `ExitLocation` / `ExitStatus` из `CoopGame`, а не случайный exfil.
- Перед `Stop()` — **безопасный Dispose ботов** (ошибки модов вроде BossNotifier.Fika не роняют extract).
- **Emergency fallback**: если `Stop()` всё равно упал — вызов `method_15` (сохранение рейда + переход в меню).
- Подробные логи: `[RaidAdminPanel.Extract]` с inner exception.

## 1.0.0 — 2026-06-12

- Первый публичный релиз: server Web UI + REST API + client command poller
- Дашборд: профили, активность, список Fika-рейдов
- Действия: save profile(s), force extract, inventory snapshot, mass extract, Fika EndMatch
- Fika bridge через reflection (без жёсткой зависимости на FikaServer.dll)
- Конфиг через `ModHelper.GetAbsolutePathToModFolder` + `config.json`
- Исправлен разбор `FikaMatch.Players` (ключи словаря = profileId)
- Клиент опрашивает команды только в рейде
- GitHub release zip: `BepInEx/plugins/RaidAdminPanel/` + `SPT/user/mods/RaidAdminPanel/`
