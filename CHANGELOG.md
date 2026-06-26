# Changelog — Raid Admin Panel

## 1.0.0 — 2026-06-12

- Первый публичный релиз: server Web UI + REST API + client command poller
- Дашборд: профили, активность, список Fika-рейдов
- Действия: save profile(s), force extract, inventory snapshot, mass extract, Fika EndMatch
- Fika bridge через reflection (без жёсткой зависимости на FikaServer.dll)
- Конфиг через `ModHelper.GetAbsolutePathToModFolder` + `config.json`
- Исправлен разбор `FikaMatch.Players` (ключи словаря = profileId)
- Клиент опрашивает команды только в рейде
- GitHub release zip: `BepInEx/plugins/RaidAdminPanel/` + `SPT/user/mods/RaidAdminPanel/`
