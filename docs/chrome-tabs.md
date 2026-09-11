# Chrome tab mixer / Микшер вкладок Chrome

The Chrome companion is experimental in 0.2.0. Its envelope and audio routing logic have automated tests; real Chrome tab capture still needs a manual listening check on your machine. No Chrome Web Store listing is included.

## Русский

1. Распакуй весь архив. Закрой старую версию программы через трей и запусти новую.
2. В Chrome открой `chrome://extensions`, включи **Режим разработчика**, нажми **Загрузить распакованное расширение**. Выбери папку `chrome-extension` из архива программы. В отдельном архиве расширения выбери распакованную папку с `manifest.json`.
3. Закрепи значок Background Ferry через меню расширений.
4. На вкладке с музыкой нажми значок расширения → **Музыка**.
5. На вкладке с видео нажми значок расширения → **Приоритет**. Можно назначить несколько приоритетных вкладок.
6. Нажми **Включить микс** в расширении. Закрытие его окна оставляет микс включённым.
7. **Приостановить** возвращает полный уровень музыки, сохраняя захват вкладок. **Отключить все вкладки** освобождает захват и возвращает обычный маршрут звука Chrome.

Язык RU/EN выбирается отдельно в расширении. Наведи мышь на **i** или перейди к ней клавишей Tab для пояснения. Если вкладка закрылась или захват завершился после перехода на другую страницу, назначь её заново.

Расширение работает самостоятельно: приложение Windows не обязательно держать открытым. **Не применяй микшер Windows к Chrome одновременно с микшером вкладок** — иначе приглушение наложится дважды. В программе раздел «Вкладки Chrome» содержит установку; сам микс управляется через значок расширения.

## English

Open `chrome://extensions`, enable **Developer mode**, choose **Load unpacked**, then select the included `chrome-extension` folder. Pin its icon. Open the action popup on your music tab and choose **Music**, then do the same on your video tab and choose **Priority**. Click **Start mixing** in the extension. One music tab and multiple priority tabs are supported.

Closing the popup keeps the mix running. **Pause mixing** restores unity gain while retaining capture. **Release all tabs** stops the streams and releases the playback route. Reassign a tab if navigation or closing it ends capture. Language and mix preferences persist in Chrome; selected captures do not resume automatically after a browser restart.

The extension works independently of the Windows app and keeps separate settings. Do not also duck Chrome through the desktop mixer.

The favicon permission can show a broader Chrome permission warning. The implementation requests icons only for assigned tabs, holds their URLs only in the live capture state, and makes no requests to third-party icon services.

## Scope, permissions, and privacy

- Chrome 116+ / Manifest V3. Protected/DRM players may not provide capturable audio; compatibility with individual streaming services is not certified.
- The extension captures only tabs explicitly assigned through its action. It does not access the microphone, record files, contact servers, request service credentials, or scan browsing history.
- `tabCapture` reads selected tab audio; `activeTab` provides temporary access after clicking the extension; `offscreen` hosts the local Web Audio graph; `storage` saves settings; `favicon` provides Chrome-managed site icons for explicitly selected tabs. No broad host permission is requested.
- Audio is streamed through a local Web Audio graph. Capture can add latency; check video synchronization with your device.
- Changes inside a music site's own volume control remain effective. The extension multiplies the resulting signal; it does not overwrite the site's slider. Priority detection is sound-level based, not speech recognition.
- Muting a captured tab can behave differently from muting its in-page player. Use the player's controls or release the tab. Normal playback restoration after a browser/audio-device failure needs a real-device check.
- To uninstall: release the tabs, then remove the extension on `chrome://extensions`.

Implementation follows the official [tabCapture API](https://developer.chrome.com/docs/extensions/reference/api/tabCapture), [offscreen API](https://developer.chrome.com/docs/extensions/reference/api/offscreen), and [tab capture guide](https://developer.chrome.com/docs/extensions/how-to/web-platform/screen-capture). Chrome requires an extension invocation for capture; a desktop Windows volume controller alone cannot provide reliable tab isolation.
