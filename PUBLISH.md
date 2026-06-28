# Publish to GitHub — Raid Admin Panel

**Статус:** `published`  
**GitHub:** Release + zip  
**Версия:** `1.0.20`  
**Deployment:** `(server_client)`

## 1. Подготовка (уже сделано этим скриптом)

Папка: `github-repos/RaidAdminPanel/`

## 2. Создать репозиторий и запушить

```powershell
cd github-repos/RaidAdminPanel
git init
git add .
git commit -m "Source backup Raid Admin Panel v1.0.20"
git branch -M main
git remote add origin https://github.com/kabzon93region/RaidAdminPanel.git
git push -u origin main
```

Или автоматически:

```powershell
python CURSORAIMODING/tools/publish/publish_github_release.py RaidAdminPanel --create-repo
```

## 3. GitHub Release

Прикрепить zip (только игровые файлы, без INSTALL.md):

`\\Servant\data\Games\EscapeFromTarkov4\CURSORAIMODING\releases\RaidAdminPanel_(server_client)_v1.0.20_2026-06-28.zip`

```powershell
gh release create v1.0.20 "\\Servant\data\Games\EscapeFromTarkov4\CURSORAIMODING\releases\RaidAdminPanel_(server_client)_v1.0.20_2026-06-28.zip" ^
  --title "Raid Admin Panel v1.0.20" ^
  --notes-file CHANGELOG.md
```

## Описание репозитория (suggested)

Браузерная админка SPT 4 + клиентский компаньон для extract и снимка лута в Fika coop.

SPT 4.0 + Fika 2.3 headless stack. Deployment: `(server_client)`.
