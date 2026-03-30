---
id: ds_nevizual_export_dtc
module: ds
role: Developer
topic: Невизуальный экспорт через DeploymentToolCore
breadcrumb: "Разработка > Процесс разработки > Экспорт разработки"
description: "Пакет разработки можно создать в невизуальном режиме с помощью утилиты DeploymentToolCore. Для этого используются команды инструмента Directum Launcher."
source: webhelp/WebClient/ru-RU/ds_nevizual_export_dtc.htm
---

# Невизуальный экспорт через DeploymentToolCore

Пакет разработки можно создать в невизуальном режиме с помощью утилиты DeploymentToolCore . Для этого используются команды инструмента Directum Launcher.

ПРИМЕЧАНИЕ. При необходимости вы можете выполнить экспорт с помощью DeploymentToolCore без использования Directum Launcher, запустив утилиту через Docker .

В зависимости от операционной системы способ вызова команды отличается:

./do.sh <команда>

do <команда>

Далее приведены команды для выполнения на компьютере с операционной системой Linux.

Чтобы создать пакет разработки:

- 1. Получите файлы с исходными кодами.
- 2. Если необходимо экспортировать исходные коды или передать пакет как отладочный, создайте XML-файл конфигурации пакета разработки. Если в пакет нужно включить только исполняемые файлы, пропустите этот шаг.
- Структура файла:

```csharp
<?xml version="1.0"?>
<DevelopmentPackageInfo
xmlns:xsd="http://www.w3.org/2001/XMLSchema"
xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
<IsDebugPackage>{Передать как отладочный пакет}
</IsDebugPackage>
<PackageModules>
<PackageModuleItem>
<Id>{Идентификатор экспортируемого решения}
</Id>
<Name>{Код компании.Имя решения}
</Name>
<Version>{Версия решения}</Version>
<IncludeAssemblies>{Включить в пакет исполняемые файлы}
</IncludeAssemblies>
<IncludeSources>{Включить в пакет исходные коды}
</IncludeSources>
<IsSolution>{Является решением}
</IsSolution>
<IsPreviousLayerModule>{Передать как базовые решения}
</IsPreviousLayerModule>
</PackageModuleItem>
<!--Список модулей для экспорта -->
<PackageModuleItem>
<Id>{Идентификатор экспортируемого решения}
</Id>
<SolutionId>{Идентификатор экспортируемого модуля}
</SolutionId>
<Name>{Код компании.Имя модуля}
</Name>
<Version>{Версия модуля}</Version>
<IncludeAssemblies>{Включить в пакет исполняемые файлы}
</IncludeAssemblies>
<IncludeSources>{Включить в пакет исходные коды}
</IncludeSources>
<IsSolution>{Является решением}
</IsSolution>
<IsPreviousLayerModule>{Передать как базовые решения}
</IsPreviousLayerModule>
</PackageModuleItem>
</PackageModules>
…
</DevelopmentPackageInfo>
```

- Пример:

```csharp
<!--Экспорт решения, которое состоит из одного модуля -->
<?xml version="1.0"?>
<DevelopmentPackageInfo
xmlns:xsd="http://www.w3.org/2001/XMLSchema"
xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
<IsDebugPackage>false</IsDebugPackage>
<PackageModules>
<PackageModuleItem>
<Id>ccaf5fa7-4108-422d-bed4-1d4ea46488af</Id>
<Name>DEV.RentSolution</Name>
<Version>0.0.1.0</Version>
<IncludeAssemblies>true</IncludeAssemblies>
<IncludeSources>false</IncludeSources>
<IsSolution>true</IsSolution>
<IsPreviousLayerModule>false
</IsPreviousLayerModule>
</PackageModuleItem>
<!--Данные модуля -->
<PackageModuleItem>
<Id>9dc3d9e2-9698-4643-ad95-d72cb55a2bb8</Id>
<SolutionId>ccaf5fa7-4108-422d-bed4-1d4ea46488af</SolutionId>
<Name>DEV.RentModule</Name>
<Version>0.0.1.0</Version>
<IncludeAssemblies>true
</IncludeAssemblies>
<IncludeSources>false
</IncludeSources>
<IsSolution>false
</IsSolution>
<IsPreviousLayerModule>false
</IsPreviousLayerModule>
</PackageModuleItem>
</PackageModules>
</DevelopmentPackageInfo>
```

- 3. В командной строке перейдите в папку с Directum Launcher и выполните команду:

/do.sh dt export-package --export_package <dev_package> [--configuration <configuration> ] --root <path> [--repositories <paths> ]

- Где:
- -export-package <dev_package> – путь к создаваемому файлу с пакетом разработки. Обязательный параметр;
- -configuration <configuration> – путь к созданному XML-файлу конфигурации пакета разработки. Необязательный параметр;
- -root <path> – путь до корневой папки, в которой хранятся репозитории. Обязательный параметр;
- -repositories <paths> или --work <paths> – имена папок с репозиториями базового и рабочего слоя соответственно. Должен быть задан хотя бы один из параметров.
- Пример команды:

./do.sh dt export-package --export_package /home/user/CustomDev/DevRX.dat --configuration /home/user/CustomDev/DevRX.xml --root /home/user --repositories Base

- 4. При необходимости измените номер версии модулей и решений из указанных репозиториев. Для этого выполните одну из команд:
- Увеличить номер версии

./do.sh dt increment_version --root <path> --repositories <paths>

- Пример команды:

./do.sh dt increment_version --root /home/user --repositories Base

- Задать номер версии

./do.sh dt set_version --version <version> --root <path> --repositories <paths>

- Где --version <version> - номер, который будет присвоен версии.
- Пример команды:

./do.sh dt set_version --version 0.0.0.1 --root /home/user --repositories Base

В результате создадутся:

- файл пакета разработки с расширением *.dat;
- XML-файл с именем пакета разработки, в котором содержится информация о пакете.
