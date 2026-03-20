# Changelog

## v1.0.0

### RU

- **автообновление при запуске**: приложение теперь проверяет обновления до открытия главного окна — если есть новая версия, сразу показывается окно загрузки в стиле Steam с прогресс-баром, скоростью (МБ/с) и строкой `v0.0.x → v1.0.0`; после установки приложение перезапускается автоматически
- **выбор версии DLSS**: кнопка «Откатить» заменена на «Выбор версии» — открывается окно со списком всех доступных версий из репозитория `NVIDIA/DLSS` с датами, бейджами «Актуальная» и «Установлена»; можно выбрать любую версию или указать локальный файл
- **кэш списка версий**: список версий DLSS сохраняется локально и обновляется раз в час — окно открывается мгновенно без запроса к GitHub, при rate limit используются кэшированные данные
- **прогресс обновления DLSS в отдельном окне**: при обновлении DLSS теперь открывается то же окно что и при обновлении приложения — показывает `v3.7.x → v3.8.x`, МБ/МБ и скорость
- **иконки игр из Steam CDN**: если локальный кэш Steam не содержит обложку — иконка автоматически скачивается с `cdn.akamai.steamstatic.com` и сохраняется в `icon_cache/`; при следующем запуске грузится из файла без сети
- **кастомные папки**: папки, открытые через «Обзор», теперь сохраняются в `custom_folders.json` и отображаются в списке игр при каждом запуске; папки которых больше нет на диске удаляются автоматически
- **выбор тайла при обзоре**: после ручного выбора папки через «Обзор» соответствующая карточка в списке игр автоматически выделяется
- **точка обновления**: после обновления DLSS точка на карточке игры сразу меняется с оранжевой на зелёную без перезапуска
- **единая версия**: номер версии хранится только в `.csproj` — все части приложения читают его из сборки автоматически
- **бэкапы убраны**: бэкап DLL при обновлении больше не создаётся

### EN

- **auto-update on launch**: the app now checks for updates before the main window opens — if a new version is available a Steam-style download window appears immediately with a progress bar, speed (MB/s) and `v0.0.x → v1.0.0` label; the app restarts automatically after installation
- **DLSS version picker**: the "Rollback" button is replaced by "Select version" — a window opens with a full list of available versions from the `NVIDIA/DLSS` repository with dates and "Latest" / "Installed" badges; any version can be selected or a local file can be used
- **version list cache**: the DLSS version list is saved locally and refreshed once per hour — the window opens instantly without a GitHub request; stale cache is used when rate-limited
- **DLSS update progress window**: updating DLSS now opens the same window as the app updater — shows `v3.7.x → v3.8.x`, MB/MB and download speed
- **game icons from Steam CDN**: if the local Steam cache has no cover art the icon is downloaded from `cdn.akamai.steamstatic.com` and stored in `icon_cache/`; on next launch it loads from disk without network
- **custom folders**: folders opened via "Browse" are now saved to `custom_folders.json` and appear in the game list on every launch; folders that no longer exist on disk are removed automatically
- **tile selection on browse**: after manually selecting a folder via "Browse" the matching game card is automatically highlighted
- **update dot fix**: after updating DLSS the tile dot immediately changes from orange to green without restarting
- **single version source**: the version number lives only in `.csproj` — all parts of the app read it from the assembly at runtime
- **backups removed**: no backup of the DLL is created when updating DLSS

## v0.0.4

### RU

- **индикатор обновления на тайле**: каждая карточка игры теперь показывает цветную точку в углу — оранжевая (доступно обновление DLSS), зелёная (версия актуальна); точка появляется при запуске, версии сравниваются с актуальной прямо с GitHub
- **последняя версия DLSS с GitHub**: поле «Последняя версия» теперь подтягивается с официального репозитория `NVIDIA/DLSS` в реальном времени, а не из встроенного файла; при отсутствии сети — фолбэк на локальное значение
- **выделение выбранной игры**: клик на карточку игры теперь подсвечивает её зелёной рамкой и тёмно-зелёным фоном
- **предложение создать ярлык**: при первом запуске приложение предлагает создать ярлык на рабочем столе (показывается один раз)
- **кнопка загрузки в README**: добавлена прямая ссылка на скачивание актуального `.exe` из последнего релиза
- **тёмный заголовок окна**: системная полоса заголовка теперь тёмная, соответствует теме приложения
- **прогресс обновления приложения**: при обновлении открывается отдельное окно с прогресс-баром — показывает загрузку (в %) и установку
- **прогресс обновления DLSS**: бар под кнопкой «Обновить DLSS» теперь показывает реальный процент загрузки, а не бесконечное вращение
- **цвет версии в игре**: текст «Версия в игре» становится оранжевым если DLSS устарел, зелёным если актуален
- **запоминание последней папки**: при следующем запуске путь к игре восстанавливается автоматически
- **тултип на кнопке «Обновить DLSS»**: показывает `«старая версия → новая версия»` при наведении
- **GitHub rate limit**: ошибка 403 теперь отображается как понятное сообщение вместо технического текста

### EN

- **update dot on tile**: each game tile now shows a colored dot in the corner — orange (DLSS update available), green (up to date); dots appear on startup, versions are compared against the latest from GitHub
- **latest DLSS version from GitHub**: the "Latest version" field now fetches from the official `NVIDIA/DLSS` repository in real time instead of a bundled file; falls back to local value when offline
- **selected game highlight**: clicking a game tile highlights it with a green border and dark green background
- **desktop shortcut offer**: on first launch the app offers to create a desktop shortcut (shown once)
- **download button in README**: added a direct download link to the latest release `.exe`
- **dark title bar**: the system window title bar is now dark, matching the app theme
- **app update progress window**: when updating the app a separate window opens with a progress bar — shows download percentage and installation stage
- **DLSS update progress bar**: the bar under "Update DLSS" now shows real download percentage instead of an indeterminate spinner
- **game version color**: "Game version" text turns orange when DLSS is outdated, green when up to date
- **remember last folder**: the game folder path is restored automatically on next launch
- **Update DLSS tooltip**: shows `«old version → new version»` on hover
- **GitHub rate limit**: 403 error is now shown as a readable message instead of a technical exception

## v0.0.3

### RU

- **автообнаружение игр**: при запуске приложение сканирует Steam, Epic Games и GOG — найденные игры с DLSS отображаются плитками с иконками прямо в интерфейсе; клик на плитку автоматически подставляет путь и запускает сканирование
- **статус DLSSTweaks**: кнопка «Проверить загрузку» заменена на автоматический цветной индикатор — зелёный (загружен), жёлтый (запустите игру для проверки), красный (нужен NVIDIA Override); кнопка «Включить Override» появляется только когда требуется
- **иконка приложения**: добавлена кастомная иконка с чипом GPU
- **исправлено**: окно теперь фиксированной ширины, ручное изменение размера отключено
- **исправлено**: дублирование игр в списке при нескольких библиотеках Steam
- **SmartScreen**: в README добавлен раздел с объяснением предупреждения Windows и инструкцией как запустить

### EN

- **game auto-detection**: on startup the app scans Steam, Epic Games, and GOG — games with DLSS are shown as tiles with icons directly in the UI; clicking a tile fills in the path and triggers a scan automatically
- **DLSSTweaks status**: the "Check load" button is replaced by an automatic colored indicator — green (loaded), yellow (launch the game to verify), red (NVIDIA Override required); the "Enable Override" button only appears when needed
- **app icon**: added a custom GPU chip icon
- **fix**: window is now fixed-width, manual resizing disabled
- **fix**: duplicate game entries when Steam has multiple library folders
- **SmartScreen**: added a README section explaining the Windows warning and how to run the app

## v0.0.2

### RU

- **исправлено**: встроенные архивы `DLSS.zip` и `DLSSTweaks.zip` не попадали в папку при публикации — приложение просило выбрать файл вручную
- **обновление приложения**: теперь скачивает и применяет обновление прямо на месте — перезаписывает файлы рядом с `.exe` и перезапускается автоматически, без ручного скачивания
- **после обновления**: показывает окно с изменениями текущей версии
- **источник DLSS**: теперь сначала скачивает актуальный `nvngx_dlss.dll` с официального репозитория `NVIDIA/DLSS` на GitHub, при неудаче — использует встроенный архив
- **исправлено**: `OnRollback` падал с необработанным исключением если файл был заблокирован (игра запущена)
- **исправлено**: сканирование папки теперь выполняется в фоне, не блокирует интерфейс
- **исправлено**: `DlssScanner` падал с `UnauthorizedAccessException` при попытке обхода защищённых вложенных папок

### EN

- **fix**: bundled `DLSS.zip` and `DLSSTweaks.zip` were missing from the publish output — the app was asking the user to pick the file manually
- **app auto-update**: now downloads and applies the update in-place — overwrites files next to the `.exe` and restarts automatically, no manual download needed
- **post-update changelog**: shows a window with changes for the current version after an update
- **DLSS source**: now attempts to download the latest `nvngx_dlss.dll` from the official `NVIDIA/DLSS` GitHub repository first, falls back to the bundled archive on failure
- **fix**: `OnRollback` crashed with an unhandled exception if the DLL file was locked (game running)
- **fix**: folder scan now runs in the background and no longer blocks the UI
- **fix**: `DlssScanner` crashed with `UnauthorizedAccessException` when traversing protected subfolders

## v0.0.1

### RU

- проверка `nvngx_dlss*.dll` в выбранной папке игры
- отображение версии DLSS в игре и последней доступной версии
- обновление DLSS из встроенного архива с созданием бэкапа
- откат DLSS из созданного бэкапа
- установка и удаление `DLSSTweaks`
- готовые пресеты для RTX 4000/5000 и RTX 3000
- проверка загрузки `DLSSTweaks` по `dlsstweaks.log`
- предложение включить `NVIDIA signature override`, если это может потребоваться
- кнопка GitHub в шапке приложения
- проверка обновлений приложения через GitHub Releases
- переход по прямой ссылке на скачивание asset-файла из latest release

### EN

- checks `nvngx_dlss*.dll` inside the selected game folder
- shows the DLSS version used by the game and the latest available version
- updates DLSS from the bundled archive and creates a backup
- rolls DLSS back from the created backup
- installs and removes `DLSSTweaks`
- includes ready presets for RTX 4000/5000 and RTX 3000
- verifies `DLSSTweaks` loading by checking `dlsstweaks.log`
- offers to enable `NVIDIA signature override` when it may be needed
- adds a GitHub button in the app header
- checks for app updates through GitHub Releases
- opens the direct asset download link from the latest release
