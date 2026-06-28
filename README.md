# Raid Admin Panel

**GitHub:** [kabzon93region](https://github.com/kabzon93region)

**Combo-мод (клиент + сервер) для SPT 4 + Fika 2.3.** Браузерная админка для управления рейдом: принудительный extract, снимок лута, мониторинг игроков.

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

- Extract требует клиентский мод у **каждого** игрока в рейде
- Fika EndMatch не сохраняет лут — только после штатного extract
- Снимок лута v1 — item count в profileData, не полный merge инвентаря
- BossNotifier.Fika (upstream) несовместим с Fika 2.3.3 — может ломать F8/extract; client v1.0.20+ смягчает это

## Поддержать проект

Разовый донат картой РФ, СБП, ЮMoney, VK Pay:  
**[DonationAlerts → kabzon93region](https://www.donationalerts.com/r/kabzon93region)**
