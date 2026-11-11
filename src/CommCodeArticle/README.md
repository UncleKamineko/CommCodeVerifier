# CommCodeArticle

Отдельная программа раздела «Артикул»: генерация артикула цилиндра (SRF10) из коммерческого кода.
Оформление — как в CommCodeVerifier. Программа не зависит от CommCodeVerifier.

Сборка: `dotnet build src/CommCodeArticle/CommCodeArticle.csproj`

Конфигурация — рядом с exe, в `Файлы конфигурации\Файлы конфигурации артикул\Цилиндры\`:
`ALEN_карта.xlsx`, `MID_карта.xlsx`, `Доп_код_карта.xlsx`. Файлы копируются при сборке из `Assets\`.
