---
description: "Экспорт .dat пакета из git-репозитория через DeploymentToolCore (dt export-package)"
---

# Export Package — Экспорт .dat из git

Создание .dat пакета напрямую из git-репозитория через DeploymentToolCore. В отличие от `zip`, выполняет полную компиляцию C# через Roslyn.

## Входные данные

- **project_path** — путь к проекту (где `source/`, `PackageInfo.xml`)
- **output_path** — куда сохранить .dat (по умолчанию: `{project_path}/{module_name}.dat`)
- **launcher_path** — путь к DirectumLauncher

## Предварительные условия

1. DirectumLauncher установлен и настроен (`config.yml`)
2. Git-репозиторий содержит исходники в `source/`
3. Платформенные сервисы запущены (PostgreSQL, WebServer) — DTC подключается к ним

## Алгоритм

### 1. Подготовь конфигурацию экспорта

Создай XML-файл конфигурации:

```bash
cat > /tmp/export-config.xml << 'EOF'
<?xml version="1.0"?>
<DevelopmentPackageInfo>
  <IsDebugPackage>false</IsDebugPackage>
  <PackageModules>
    <PackageModuleItem>
      <Id>{GUID из Module.mtd NameGuid}</Id>
      <Name>{Полное имя модуля, например DirRX.CRM}</Name>
      <Version>{Версия, например 2.0.0.0}</Version>
      <IncludeAssemblies>true</IncludeAssemblies>
      <IncludeSources>true</IncludeSources>
      <IsSolution>true</IsSolution>
      <IsPreviousLayerModule>false</IsPreviousLayerModule>
    </PackageModuleItem>
    <!-- Повторить для каждого модуля -->
  </PackageModules>
</DevelopmentPackageInfo>
EOF
```

### 2. Запусти экспорт

```bash
LAUNCHER="${LAUNCHER_PATH:-$(pwd)/дистрибутив/launcher}"

$LAUNCHER/do.sh dt export_package \
  --export_package="{output_path}" \
  --configuration="/tmp/export-config.xml" \
  --root="{git_root}" \
  --repositories="work"
```

### 3. Проверь результат

```bash
# Проверить что .dat создан
ls -la {output_path}

# Посмотреть содержимое
unzip -l {output_path} | head -30

# Проверить PackageInfo.xml внутри
unzip -p {output_path} PackageInfo.xml
```

### 4. При ошибке

| Exit-код | Значение | Действие |
|----------|----------|----------|
| 0 | Успех | .dat создан |
| 7 | Ошибка экспорта | Проверь исходники: corrupt .cs, отсутствующие .mtd |
| 1 | Pre-export error | Проверь конфигурационный XML, пути |

## Упрощённый экспорт (без DTC)

Если DeploymentTool недоступен (нет Docker, нет Launcher):

```bash
cd {project_path}
rm -f {module_name}.dat

# Собрать список файлов
find source -type f | sort > /tmp/filelist.txt
echo "PackageInfo.xml" >> /tmp/filelist.txt
[ -d settings ] && find settings -type f | sort >> /tmp/filelist.txt

# Упаковать
zip -@ "{module_name}.dat" < /tmp/filelist.txt
```

> ⚠ Упрощённый экспорт НЕ компилирует C#. Для production используй DTC.

## Merge нескольких пакетов

```bash
$LAUNCHER/do.sh dt merge_packages "{output_path}/Full.dat" \
  --packages="Base.dat;Custom.dat;CRM.dat"
```

## Версионирование

```bash
# Инкремент версии в git-репозитории
$LAUNCHER/do.sh dt increment_version --root="{git_root}" --repositories="work"

# Установить конкретную версию
$LAUNCHER/do.sh dt set_version --version="2.0.1.0" --root="{git_root}" --repositories="work"
```

## Ссылки
- `knowledge-base/guides/35_deployment_tool_internals.md` — полная документация DTC
- `.claude/skills/deploy/SKILL.md` — деплой .dat на стенд
- `.claude/skills/manage-dat-package/SKILL.md` — полный lifecycle .dat
