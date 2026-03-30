---
id: ds_integratciia_ide
module: ds
role: Developer
topic: Интеграция со сторонним редактором кода
breadcrumb: "Разработка > Администрирование Development Studio"
description: "Для работы среды разработки с редактором кода: Установите VS Code Настройте среду разработки Настройте VS Code Установка VS Code Visual Studio Code (VS Code) – кроссплатформен"
source: webhelp/WebClient/ru-RU/ds_integratciia_ide.htm
---

# Интеграция со сторонним редактором кода

Для работы среды разработки с редактором кода:

- 1. Установите VS Code
- 2. Настройте среду разработки
- 3. Настройте VS Code

## Установка VS Code

Visual Studio Code (VS Code) – кроссплатформенный редактор кода от компании Microsoft. Является программным обеспечением с открытым исходным кодом. VS Code поставляется со встроенной поддержкой языков программирования JavaScript, TypeScript и платформы Node.js и имеет большое количество расширений для других языков, например C#.

Программу для установки VS Code вы можете скачать с официального сайта .

ВАЖНО. Минимальная поддерживаемая версия VS Code 1.89.1.

## Настройка среды разработки

По умолчанию среда разработки настроена для работы с VS Code на операционной системе Microsoft Windows. При использовании Linux дополнительно настройте среду разработки с помощью встроенного инструмента DevTools :

- 1. Запустите среду разработки.
- 2. С помощью клавиш CTRL+SHIFT+I откройте окно DevTools :
- 3. Перейдите на вкладку Application и в настройке openFileCommandTemplate укажите параметры для открытия VS Code:

• $(solutiondirectory) – полное имя папки, в которой находится файл решения SungeroDevelopment.sln;

• $(solutionname) – имя файла решения SungeroDevelopment.sln;

• $(filepath) – полное имя файла с исходным кодом, который нужно открыть в редакторе кода.

Пример:

```csharp
{
"openFileCommandTemplate":"\"bash\" -c \"code \"$(solutiondirectory)/$(solutionname).code-workspace\" -g \"$(filepath):$(linenumber)\"\""
}
```

## Настройка VS Code

- 1. Перейдите в папку .ds.
- ПРИМЕЧАНИЕ. Папка .ds создается при первом запуске среды разработки и является скрытой папкой. Чтобы в нее перейти вы можете воспользоваться адресной строкой проводника.
- 2. Создайте файл SungeroDevelopment.code-workspace и заполните в нем секции:

• folders – папки, которые необходимо включить в рабочее пространство. Укажите путь до папки с локальным репозиторием Work и текущей директории;

• extensions – расширения для VS Code, которые нужно установить;

Список расширений

Обязательные расширения: • C# Dev Kit – включает инструменты для разработки на языке C#, например отладку или интеграцию с .NET; • ResXpress – расширение для работы с ресурсными файлами проекта, которое упрощает редактирование и добавление ресурсов, например строк или изображений; • vscode-mssql – расширение для работы с Microsoft SQL Server. С помощью него из VSCode можно подключаться к базам данных SQL Server, выполнять запросы, просматривать результаты и управлять схемами базы данных; • vscode-postgres – расширение для работы с PostgreSQL. С помощью него из VSCode можно подключаться к базам данных PostgreSQL, выполнять запросы, просматривать результаты и управлять схемами базы данных; Дополнительно вы можете установить CSharpier – расширение для форматирования кода на языке C#, который позволяет поддерживать код проекта в едином стиле. Это расширение является необязательным.

• settings – настройки редактора, например автоматическое сохранение файлов или форматирование кода.

Рекомендуемые настройки

files.autoSave – укажите значение onFocusChange , чтобы изменения файла сохранялись автоматически при переключении на другой файл или окно; files.autoSaveWhenNoErrors – укажите значение true , чтобы файл сохранялся автоматически, только если в нем нет ошибок; files.autoSaveWorkspaceFilesOnly – укажите значение true , чтобы автоматически сохранялись только файлы, относящиеся к текущему проекту; files.readonlyInclude – укажите, какие файлы, необходимо открывать в режиме только для чтения. Например, чтобы файлы с расширением *.g.cs открывались только на чтение, укажите "**/*.g.cs": true ; files.associations – установите соответствие между файлами и языком программирования или расширением. Например, если указать "**/*_postgres.sql": "postgres" , то все файлы, заканчивающиеся на _postgres.sql, будут ассоциированы с языком PostgreSQL; editor.defaultFormatter – укажите csharpier.csharpier-vscode , чтобы при форматировании кода C# использовалось расширение CSharpier; editor.formatOnSave – укажите значение true , чтобы при сохранении файла код автоматически форматировался; dotnet.defaultSolution – укажите решение, которое будет открываться по умолчанию при открытии проекта, например SungeroDevelopment.sln; workbench.editor.closeOnFileDelete – укажите значение true , чтобы при удалении файла в редакторе кода закрывалась его вкладка; dotnet.automaticallySyncWithActiveItem – укажите значение true , чтобы при удалении файла в редакторе кода закрывалась его вкладка.

• launch – конфигурации для отладки, включая параметры для подключения к процессам в docker-контейнерах и .NET приложениях. Укажите значения секции из примера ниже.

- Пример заполнения файла SungeroDevelopment.code-workspace:

```csharp
{
"folders": [
{
"path": "../work"
},
{
"path": "."
},
],
"extensions": {
"recommendations": [
"ms-dotnettools.csdevkit",
"PrateekMahendrakar.resxpress",
"ms-mssql.mssql",
"ckolkman.vscode-postgres",
"csharpier.csharpier-vscode",
]
},
"settings": {
"files.autoSave": "onFocusChange",
"files.autoSaveWhenNoErrors": true,
"files.autoSaveWorkspaceFilesOnly": true,
"files.readonlyInclude": {
"**/*.g.cs": true,
"**/*.mtd": true
},
"files.associations": {
"**/*_postgres.sql": "postgres",
},
"[csharp]": {
"editor.defaultFormatter": "csharpier.csharpier-vscode",
"editor.formatOnSave": true
},
"dotnet.defaultSolution": "SungeroDevelopment.sln",
"workbench.editor.closeOnFileDelete": true,
"dotnet.automaticallySyncWithActiveItem": true
},
"launch": {
"version": "0.2.0",
"configurations": [
{
"name": ".NET Core Attach",
"type": "coreclr",
"request": "attach"
},
{
"name": "WebServer (docker attach)",
"type": "docker",
"request": "attach",
"platform": "netCore",
"containerName": "sungerowebserver",
"processName": "Sungero.WebServer.Host",
"sourceFileMap": {
"/": "~",
}
},
{
"name": "GenericService (docker attach)",
"type": "docker",
"request": "attach",
"platform": "netCore",
"containerName": "genericservice",
"processName": "Sungero.GenericService.Host",
"sourceFileMap": {
"/": "~",
}
},
{
"name": "IntegrationService (docker attach)",
"type": "docker",
"request": "attach",
"platform": "netCore",
"containerName": "integrationservice",
"processName": "Sungero.IntegrationService.Host",
"sourceFileMap": {
"/": "~",
}
}
]
},
}
```

- 3. В папке с локальными репозиториями Work и Base создайте файл .editorconfig и заполните в нем:

root = true [*.cs] indent_style = space indent_size = 2

- 4. Проверьте, что VS Code открывается и указанные выше настройки, например подсветка синтаксиса, применились. Для этого в среде разработки перейдите в редактор модуля или типа сущности и нажмите на ссылку с типом функции, например, Серверные .

## Настройка отладки

Установите расширение «Docker». Подробнее про работу с расширениями см. в документации Visual Studio Code статью Extension Marketplace .
