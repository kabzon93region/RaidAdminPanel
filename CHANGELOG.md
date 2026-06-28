# Changelog — Raid Admin Panel

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
