---
id: ds_nastroyka_repozitoriyev
module: ds
role: Developer
topic: Настройка репозиториев
breadcrumb: "Разработка > Администрирование Development Studio > Установка"
description: "При установке Development Studio на рабочем месте разработчика создается рабочая папка с локальными репозиториями Work и Base. Чтобы работать с ними в среде разработки,..."
source: webhelp/WebClient/ru-RU/ds_nastroyka_repozitoriyev.htm
---

# Настройка репозиториев

При установке Development Studio на рабочем месте разработчика создается рабочая папка с локальными репозиториями Work и Base . Чтобы работать с ними в среде разработки, необходимо указать пути до них. Для этого:

- 1. Запустите среду разработки.
- 2. На панели инструментов по кнопке откройте окно настройки конфигураций:
- 3. Укажите значения параметров:

• rootDirectory – путь до корневой папки репозитория;

• repositories – список папок репозитория. Укажите имя папки репозитория и слой разработки в параметрах folderName и type соответственно.

Пример настройки:

```csharp
{
"rootDirectory": "D:/Projects/DirectumLauncher/etc/git_repository",
"repositories": [
{
"folderName": "Base",
"type": "Base"
},
{
"folderName": "Work",
"type": "Work"
}
]
}
```
