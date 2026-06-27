# Raid Admin Panel

**GitHub:** [kabzon93region](https://github.com/kabzon93region)

Браузерная админка для **SPT 4** с клиентским компаньоном для действий в **Fika coop**-рейде: статистика, сохранение профилей, принудительная высадка (extract), снимок лута, массовая высадка рейда.

| Компонент | Путь после установки | Назначение |
|-----------|----------------------|------------|
| **Server** | `SPT/user/mods/RaidAdminPanel/` | Web UI + REST API на HTTPS-порту SPT |
| **Client** | `BepInEx/plugins/RaidAdminPanel/RaidAdminPanel.dll` | Выполняет команды extract / снимок в рейде |

**GUID (client):** `com.kabzon93region.raidadminpanel`  
**ModGuid (server):** `com.kabzon93region.raidadminpanel`

## Требования

| Компонент | Версия |
|-----------|--------|
| **EFT** | 16.9.x |
| **SPT** | 4.0.13+ |
| **Fika** | 2.3.x (список рейдов, coop extract) |
| **BepInEx** | 5.4.x |

Клиентский мод нужен **на каждом игроке**, для которого админ вызывает extract или снимок лута.

## Установка

1. Скачайте release zip `RaidAdminPanel_(server_client)_v1.0.0_*.zip`.
2. Распакуйте в корень игры (`EscapeFromTarkov.exe`, папки `BepInEx`, `SPT`).
3. Откройте `SPT/user/mods/RaidAdminPanel/config.json` и задайте `adminApiKey`.
4. Перезапустите SPT server и клиент(ы).

## Открыть админку

`https://<IP-сервера>:6969/RaidAdminPanel/index.html`

Порт и IP — из `SPT_Data/configs/http.json`. В UI введите API key (сохраняется в `localStorage` браузера).

## Возможности

- **Дашборд** — число профилей, активные игроки, активные Fika-рейды.
- **Сохранить профиль / все профили** — `SaveServer.SaveProfileAsync` (только сервер).
- **Extract (игрок / весь рейд)** — команда на клиент → `CoopGame.Extract` (как F8/Fika extract), затем штатный `match/local/end`.
- **Снимок лута** — клиент отправляет количество предметов в рейде; сервер пишет в `user/profileData/<id>/last_inventory_snapshot.json`.
- **Закрыть сессию Fika** — `MatchService.EndMatch` **без** сохранения лута; использовать только после extract.

## Ограничения (v1.0.0)

- Extract работает только для **живых** игроков в активном CoopGame (`Status == Started`).
- `EndMatch` не переносит лут в stash — только штатный extract.
- Снимок лута — метаданные (item count), не полный merge инвентаря.
- Опрос команд клиентом — только когда игрок **в рейде** (есть `MainPlayer`).

## Сборка из исходников

```powershell
cd CURSORAIMODING
python tools/pack/pack_raidadminpanel.py
```

Исходники:

- Клиент: `client-mods/RaidAdminPanel/`
- Сервер: `server-mods/RaidAdminPanel/` (в GitHub — папка `server/`)

## Проверка

**Сервер:** `[RaidAdminPanel] loaded — open /RaidAdminPanel/index.html`  
**Клиент:** `Raid Admin Panel Client v1.0.0 loaded`  
**В рейде:** админка → Extract → в логе клиента `[RaidAdminPanel] admin force extract executed`

## Аддоны

- **[Trader Services Panel](../client-mods/TraderServicesPanel/)** — страховка/ремонт торговцев в runtime + sync на клиенты (`/TraderServicesPanel/index.html`)

## Changelog

См. [CHANGELOG.md](CHANGELOG.md).

## Поддержать проект

Разовый донат картой РФ, СБП, ЮMoney, VK Pay:  

**[DonationAlerts → kabzon93region](https://www.donationalerts.com/r/kabzon93region)**
# Raid Admin Panel







**GitHub:** [kabzon93region](https://github.com/kabzon93region)







Браузерная админка для **SPT 4** с клиентским компаньоном для действий в **Fika coop**-рейде: статистика, сохранение профилей, принудительная высадка (extract), снимок лута, массовая высадка рейда.







| Компонент | Путь после установки | Назначение |



|-----------|----------------------|------------|



| **Server** | `SPT/user/mods/RaidAdminPanel/` | Web UI + REST API на HTTPS-порту SPT |



| **Client** | `BepInEx/plugins/RaidAdminPanel/RaidAdminPanel.dll` | Выполняет команды extract / снимок в рейде |







**GUID (client):** `com.kabzon93region.raidadminpanel`  



**ModGuid (server):** `com.kabzon93region.raidadminpanel`







## Требования







| Компонент | Версия |



|-----------|--------|



| **EFT** | 16.9.x |



| **SPT** | 4.0.13+ |



| **Fika** | 2.3.x (список рейдов, coop extract) |



| **BepInEx** | 5.4.x |







Клиентский мод нужен **на каждом игроке**, для которого админ вызывает extract или снимок лута.







## Установка







1. Скачайте release zip `RaidAdminPanel_(server_client)_v1.0.0_*.zip`.



2. Распакуйте в корень игры (`EscapeFromTarkov.exe`, папки `BepInEx`, `SPT`).



3. Откройте `SPT/user/mods/RaidAdminPanel/config.json` и задайте `adminApiKey`.



4. Перезапустите SPT server и клиент(ы).







## Открыть админку







`https://<IP-сервера>:6969/RaidAdminPanel/index.html`







Порт и IP — из `SPT_Data/configs/http.json`. В UI введите API key (сохраняется в `localStorage` браузера).







## Возможности







- **Дашборд** — число профилей, активные игроки, активные Fika-рейды.



- **Сохранить профиль / все профили** — `SaveServer.SaveProfileAsync` (только сервер).



- **Extract (игрок / весь рейд)** — команда на клиент → `CoopGame.Extract` (как F8/Fika extract), затем штатный `match/local/end`.



- **Снимок лута** — клиент отправляет количество предметов в рейде; сервер пишет в `user/profileData/<id>/last_inventory_snapshot.json`.



- **Закрыть сессию Fika** — `MatchService.EndMatch` **без** сохранения лута; использовать только после extract.







## Ограничения (v1.0.0)







- Extract работает только для **живых** игроков в активном CoopGame (`Status == Started`).



- `EndMatch` не переносит лут в stash — только штатный extract.



- Снимок лута — метаданные (item count), не полный merge инвентаря.



- Опрос команд клиентом — только когда игрок **в рейде** (есть `MainPlayer`).







## Сборка из исходников







```powershell



cd CURSORAIMODING



python tools/pack/pack_raidadminpanel.py



```







Исходники:







- Клиент: `client-mods/RaidAdminPanel/`



- Сервер: `server-mods/RaidAdminPanel/` (в GitHub — папка `server/`)







## Проверка







**Сервер:** `[RaidAdminPanel] loaded — open /RaidAdminPanel/index.html`  



**Клиент:** `Raid Admin Panel Client v1.0.0 loaded`  



**В рейде:** админка → Extract → в логе клиента `[RaidAdminPanel] admin force extract executed`







## Аддоны

- **[Trader Services Panel](../client-mods/TraderServicesPanel/)** — страховка/ремонт торговцев в runtime + sync на клиенты (`/TraderServicesPanel/index.html`)

## Changelog







См. [CHANGELOG.md](CHANGELOG.md).







## Поддержать проект







Разовый донат картой РФ, СБП, ЮMoney, VK Pay:  



**[DonationAlerts → kabzon93region](https://www.donationalerts.com/r/kabzon93region)**
