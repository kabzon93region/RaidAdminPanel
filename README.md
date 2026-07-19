# Raid Admin Panel

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Release](https://img.shields.io/badge/release-v1.0.20-blue)](https://github.com/kabzon93region/RaidAdminPanel/releases/tag/v1.0.20)
[![Download zip](https://img.shields.io/badge/download-zip-brightgreen)](https://github.com/kabzon93region/RaidAdminPanel/releases/tag/v1.0.20)
[![EFT](https://img.shields.io/badge/EFT-16%2E9-orange)](https://www.escapefromtarkov.com/)
[![SPT](https://img.shields.io/badge/SPT-4.0.13-blue)](https://sp-tarkov.com/)
[![Fika](https://img.shields.io/badge/Fika-2%2E3%2Ex-purple)](https://github.com/project-fika/Fika-Plugin)
[![BepInEx](https://img.shields.io/badge/BepInEx-5%2E4%2Ex-yellow)](https://github.com/BepInEx/BepInEx)
![Deployment](https://img.shields.io/badge/deployment-server_client-lightgrey)

Браузерная админка SPT 4 + клиентский компаньон для extract и снимка лута в Fika coop.

| | |
|---|---|
| **Разработчик** | [kabzon93region](https://github.com/kabzon93region) |
| **Версия** | 1.0.20 |
| **GitHub** | [RaidAdminPanel](https://github.com/kabzon93region/RaidAdminPanel) |
| **Deployment** | `(server_client)` |
| **Тип** | combo (client + server) |

## Возможности

- **Веб-интерфейс** — открывается в браузере, не требует отдельного UI в игре
- **Принудительный extract** — массовая эвакуация всех игроков из рейда
- **Снимок лута** — дамп инвентаря всех игроков в profileData
- **Мониторинг** — статус игроков, время рейда, состояние сервера
- **Fika coop** — работает с headless-хостом, клиентский компаньон обязателен

## Установка

### Серверная часть

1. Скопировать `RaidAdminPanel/` в `SPT/user/mods/`
2. Перезапустить SPT сервер

### Клиентская часть

1. Скопировать `RaidAdminPanel.dll` в `BepInEx/plugins/RaidAdminPanel/`
2. Клиентский мод **обязателен** у каждого игрока в рейде для extract

### Веб-интерфейс

- Открыть `https://<server-ip>:6969/RaidAdminPanel/index.html` в браузере
- Порт и IP — из `SPT_Data/configs/http.json`
| Компонент | Путь |
|-----------|------|
| Сервер | `SPT/user/mods/RaidAdminPanel/` |
| Клиент | `BepInEx/plugins/RaidAdminPanel/RaidAdminPanel.dll` |
| Deployment | `(server_client)` |

## Требования

- **SPT**: 4.0.x
- **Fika**: 2.3.x с headless-хостом
- **BepInEx**: 5.4.x

## Известные проблемы

- Extract требует клиентский мод у **каждому** игрока в рейде
- Fika EndMatch не сохраняет лут — только после штатного extract
- Снимок лута v1 — item count в profileData, не полный merge инвентаря
- BossNotifier.Fika (upstream) несовместим с Fika 2.3.3 — может ломать F8/extract; client v1.0.20+ смягчает это
- **Performance:** `Debug > EnableF8Diagnostics = true` вызывает сильные лаги/FPS drop (Harmony prefix на `Fika.Core.Main.Components.CoopHandler.ProcessQuitting` + тяжёлая рефлексия). По умолчанию **выключено**. Включайте только при проблемах с массовым extract.

## Поддержать проект

Разовый донат картой РФ, СБП, ЮMoney, VK Pay:
**[DonationAlerts → kabzon93region](https://www.donationalerts.com/r/kabzon93region)**

---

*Мод разработан при поддержке [Cursor AI](https://cursor.sh/) и Xiomi MiMo.*
