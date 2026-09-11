export const text={
 title:["Tab mixer","Микшер вкладок"],tagline:["One browser. Separate sound.","Один браузер. Разный звук."],
 assign:["Use this tab as","Назначить эту вкладку"],music:["Music","Музыка"],priority:["Priority","Приоритет"],
 assignTip:["Open this popup on each tab you want to use. Chrome requires a separate click for each tab. Only selected audio is processed, locally; nothing is recorded.","Открой расширение на каждой нужной вкладке. Chrome требует отдельное нажатие для каждой. Звук обрабатывается локально, без записи."],
 mode:["Mode","Режим"],modeTip:["Steady holds one quiet level; Adaptive follows the priority signal within a small quiet range.","Постоянный режим держит один тихий уровень; адаптивный слегка меняет фон по уровню приоритетного звука."],
 steady:["Steady","Постоянный"],adaptive:["Adaptive","Адаптивный"],
 duck:["Music remaining","Уровень фона"],duckTip:["Percentage of the music tab's current signal. Your player volume remains unchanged. Music returns to 100% when the priority tabs go quiet.","Доля текущего звука музыкальной вкладки. Громкость плеера не меняется. В тишине приоритетных вкладок фон возвращается к 100%."],
 threshold:["Signal threshold","Порог звука"],thresholdTip:["Lower values detect quieter audio. This detects sound, not speech.","Меньшее значение учитывает более тихий звук. Это определение звука, а не распознавание речи."],
 attack:["Fade down","Приглушение"],hold:["Silence hold","Пауза в тишине"],release:["Fade back","Возвращение"],
 attackTip:["Time to complete most of the fade down.","Время, за которое музыка почти полностью приглушается."],holdTip:["Wait in silence before returning music, avoiding jumps between words.","Ожидание тишины перед возвращением музыки, чтобы избежать скачков между словами."],releaseTip:["Time for the music to return gently.","Время плавного возвращения музыки."],
 start:["Start mixing","Включить микс"],stop:["Pause mixing","Приостановить"],releaseAll:["Release all tabs","Отключить все вкладки"],
 releaseAllTip:["Stop audio processing and restore Chrome's normal playback route. Closing this popup keeps mixing active.","Остановить обработку и вернуть обычный звук Chrome. Закрытие этого окна не останавливает микс."],
 empty:["Choose a music tab and a priority tab.","Выбери вкладку музыки и приоритетную вкладку."],
 ready:["Ready","Готово"],ducking:["Making room","Приглушение"],holding:["Holding","Ждём тишины"],returning:["Returning music","Возвращаем музыку"],
 remove:["Release tab","Отключить вкладку"],settings:["Timing","Время переходов"],language:["Language","Язык"],
 OPEN_WEB_TAB:["Open a regular website tab, then click the extension icon again.","Открой обычную страницу сайта, затем снова нажми значок расширения."],
 SELECT_SOURCES:["Select one Music tab and at least one Priority tab first.","Сначала выбери одну вкладку музыки и хотя бы одну приоритетную вкладку."],
 captureError:["Could not access this tab's audio. Click the extension icon on the tab again. Protected players may not support capture.","Не удалось получить звук вкладки. Снова нажми значок расширения на этой вкладке. Защищённые плееры могут не поддерживать захват."],
 separate:["Tab settings are independent from the desktop mixer.","Настройки вкладок независимы от микшера приложений."]
};
export const t=(key,language)=>text[key]?.[language==="en"?0:1]||key;
