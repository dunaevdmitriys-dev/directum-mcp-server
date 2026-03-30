# 24. Каталог модулей платформы Directum RX

## Обзор

В `archive/base/` содержится 29 модулей платформы Directum RX (Sungero). Все модули зависят от `Sungero.DirectumRX` (IsSolutionModule). Иерархия строится через Dependencies в Module.mtd: от фундаментальных (Shell, Commons) через организационные (Parties, Company) к прикладным (Docflow, RecordManagement, Contracts и др.).

## Справочник GUID модулей

| Модуль | NameGuid |
|--------|----------|
| Sungero.DirectumRX | `e4fe1153-919e-4732-aadc-2c8e9b5c0b5a` |
| Sungero.Shell | `fcc573ab-5f4e-4b20-88e8-7b1e11a7a59a` |
| Sungero.Commons | `459fa497-ee5b-49a4-9980-de00cada9b7a` |
| Sungero.Parties | `243b34ec-8425-4c7e-b66f-27f7b9c8f38d` |
| Sungero.Company | `d534e107-a54d-48ec-85ff-bc44d731a82f` |
| Sungero.Docflow | `df83a2ea-8d43-4ec4-a34a-2e61863014df` |
| Sungero.RecordManagement | `4e25caec-c722-4740-bcfd-c4f803840ac6` |
| Sungero.RecordManagementUI | `51247c94-981f-4bc8-819a-128704b5aa31` |
| Sungero.Contracts | `f9d15b1c-2784-4c84-8348-1e162d70b988` |
| Sungero.ContractsUI | `3c8b7d3a-187d-4445-8a8c-1d00ece44556` |
| Sungero.FinancialArchive | `59797aba-7718-45df-8ac1-5bb7a36c7a66` |
| Sungero.FinancialArchiveUI | `e99ae7e2-edb7-4904-a19a-4577f07609a4` |
| Sungero.DocflowApproval | `62cf51a2-6371-4c12-9dec-68113862d5e1` |
| Sungero.Meetings | `593dcc11-15ee-49f2-b4ef-bf4cf7867055` |
| Sungero.MeetingsUI | `6ea9a047-b597-42eb-8f90-da8c559dd057` |
| Sungero.Projects | `356e6500-45bc-482b-9791-189b5adedc28` |
| Sungero.Exchange | `cec41b99-da21-422f-9332-0fbc423e95c0` |
| Sungero.ExchangeCore | `bc0d1897-640a-4b4d-a43a-a23c5984a009` |
| Sungero.ExchangeCoreDiadoc | `30083842-5a15-4efb-9cab-0b61b1760157` |
| Sungero.ExchangeCoreSbis | `d764569f-fa35-48be-aec9-d337b185d47a` |
| Sungero.Integration1C | `f7b1d5b7-5af1-4a9f-b4d7-4e18840d7195` |
| Sungero.IntegrationDcs | `9ebe1af0-4286-4ab6-8975-68040cfa3e91` |
| Sungero.Intelligence | `e08dc659-2828-4d50-b90d-7d06408ab7cb` |
| Sungero.SmartProcessing | `bb685d97-a673-42ea-8605-66889967467f` |
| Sungero.MobileApps | `1a7ef5ec-c6f4-47df-98c1-b3eae77dabae` |
| Sungero.PowerOfAttorneyCore | `1ecb3185-14ae-422d-99c6-babcf2ab059f` |
| Sungero.PowerOfAttorneyKontur | `a37bcb31-c5ce-4052-af97-ab7cbd19bf27` |
| Sungero.InternalPolicies | `48c9a380-db0e-47ca-ae0b-4015bbced723` |
| Sungero.InternalPoliciesUI | `fc7d414c-a708-4d06-898e-e839ccd4d720` |

> **Внешние зависимости (не в archive/base):**
> - `ec7b606a-21ee-4f16-aba8-ab8c2af76d12` -- Sungero.CoreEntities (ядро платформы, встроенный)
> - `92491aa6-c4df-4f46-a807-ebdd337bda74` -- Sungero.Capture (модуль ввода, не экспортирован в base)

## Иерархия зависимостей

```
Sungero.DirectumRX (Solution Module)
  |
  +-- Sungero.Commons --> CoreEntities (внешний)
  |
  +-- Sungero.Parties --> Commons, CoreEntities, ExchangeCore
  |
  +-- Sungero.Company --> Parties, CoreEntities
  |
  +-- Sungero.Docflow --> Company, CoreEntities, Projects
  |
  +-- Sungero.RecordManagement --> Docflow, CoreEntities
  |
  +-- Sungero.RecordManagementUI --> RecordManagement, Docflow
  |
  +-- Sungero.Contracts --> Docflow, Parties, CoreEntities
  |
  +-- Sungero.ContractsUI --> Contracts
  |
  +-- Sungero.FinancialArchive --> Docflow
  |
  +-- Sungero.FinancialArchiveUI --> FinancialArchive, Docflow
  |
  +-- Sungero.DocflowApproval --> Docflow
  |
  +-- Sungero.Projects --> Docflow
  |
  +-- Sungero.Meetings --> (только DirectumRX)
  |
  +-- Sungero.MeetingsUI --> (только DirectumRX)
  |
  +-- Sungero.ExchangeCore --> Company
  |
  +-- Sungero.Exchange --> Docflow, CoreEntities
  |
  +-- Sungero.ExchangeCoreDiadoc --> (только DirectumRX)
  |
  +-- Sungero.ExchangeCoreSbis --> (только DirectumRX)
  |
  +-- Sungero.Intelligence --> (только DirectumRX)
  |
  +-- Sungero.SmartProcessing --> Capture, CoreEntities, Docflow
  |
  +-- Sungero.Integration1C --> CoreEntities, Capture
  |
  +-- Sungero.IntegrationDcs --> (только DirectumRX)
  |
  +-- Sungero.MobileApps --> (только DirectumRX)
  |
  +-- Sungero.PowerOfAttorneyCore --> (только DirectumRX)
  |
  +-- Sungero.PowerOfAttorneyKontur --> (только DirectumRX)
  |
  +-- Sungero.InternalPolicies --> (только DirectumRX)
  |
  +-- Sungero.InternalPoliciesUI --> InternalPolicies
  |
  +-- Sungero.Shell --> Docflow, RecordManagement, CoreEntities, Capture
```

## Полная таблица зависимостей

| Модуль | Dependencies (GUID) | Dependencies (имена) |
|--------|---------------------|----------------------|
| DirectumRX | -- | Корневой модуль решения (нет зависимостей) |
| Shell | Docflow, RecordManagement, CoreEntities, Capture | Верхний UI-модуль |
| Commons | CoreEntities | Базовые справочники |
| Parties | Commons, CoreEntities, ExchangeCore | Контрагенты |
| Company | Parties, CoreEntities | Наша организация |
| Docflow | Company, CoreEntities, Projects | Документооборот |
| RecordManagement | Docflow, CoreEntities | Делопроизводство |
| RecordManagementUI | RecordManagement, Docflow | UI делопроизводства |
| Contracts | Docflow, Parties, CoreEntities | Договоры |
| ContractsUI | Contracts | UI договоров |
| FinancialArchive | Docflow | Финансовые документы |
| FinancialArchiveUI | FinancialArchive, Docflow | UI финансового архива |
| DocflowApproval | Docflow | Согласование |
| Meetings | -- | Совещания |
| MeetingsUI | -- | UI совещаний |
| Projects | Docflow | Проекты |
| Exchange | Docflow, CoreEntities | Обмен с контрагентами |
| ExchangeCore | Company | Ядро обмена |
| ExchangeCoreDiadoc | -- | Провайдер Диадок |
| ExchangeCoreSbis | -- | Провайдер СБИС |
| Integration1C | CoreEntities, Capture | Интеграция с 1С |
| IntegrationDcs | -- | Интеграция DCS |
| Intelligence | -- | Интеллектуальная обработка |
| SmartProcessing | Capture, CoreEntities, Docflow | Интеллектуальный ввод |
| MobileApps | -- | Мобильные приложения |
| PowerOfAttorneyCore | -- | МЧД (ядро) |
| PowerOfAttorneyKontur | -- | МЧД (Контур) |
| InternalPolicies | -- | Локальные нормативные акты |
| InternalPoliciesUI | InternalPolicies | UI нормативных актов |

> В столбце Dependencies указаны только прикладные зависимости. Все модули также зависят от DirectumRX (IsSolutionModule=true).

---

## Ядро платформы

### Sungero.DirectumRX

- **NameGuid:** `e4fe1153-919e-4732-aadc-2c8e9b5c0b5a`
- **Назначение:** Корневой модуль решения (Solution Module). Все остальные модули зависят от него через `IsSolutionModule: true`.
- **Зависимости:** Нет (Dependencies отсутствует в Module.mtd)
- **Сущности:** 0 (только Module.mtd)
- **Особенности:** Точка входа решения. При создании кастомного пакета он должен зависеть от DirectumRX или от конкретного прикладного модуля.

### Sungero.Shell

- **NameGuid:** `fcc573ab-5f4e-4b20-88e8-7b1e11a7a59a`
- **Назначение:** Оболочка системы. Обложки, навигация, виджеты, общие действия.
- **Зависимости:** Docflow (`df83a2ea`), RecordManagement (`4e25caec`), CoreEntities (`ec7b606a`), Capture (`92491aa6`)
- **Сущности:** 0 (собственных типов нет)
- **Особенности:** Содержит обложку (Cover), навигацию (ExplorerTreeOrder), ModuleSharedFunctions, ModuleConstants. Не определяет собственных сущностей, но предоставляет UI-функции верхнего уровня.

### Sungero.Commons

- **NameGuid:** `459fa497-ee5b-49a4-9980-de00cada9b7a`
- **Назначение:** Базовые справочники общего назначения.
- **Зависимости:** CoreEntities (`ec7b606a`)
- **Сущности:** 8 (не считая коллекций)
- **Ключевые сущности:**
  - `City` -- Города
  - `Country` -- Страны
  - `Region` -- Регионы
  - `Currency` -- Валюты
  - `VatRate` -- Ставки НДС
  - `ExternalEntityLink` -- Связи с внешними системами
  - `EntityRecognitionInfo` -- Результаты распознавания (с коллекциями Facts, AdditionalClassifiers)
  - `ClassifierTrainingSession` -- Сессии обучения классификатора
- **AsyncHandlers:** IndexEntity
- **Типы:** Справочники (Databook), без задач/документов

---

## Организационная структура

### Sungero.Parties

- **NameGuid:** `243b34ec-8425-4c7e-b66f-27f7b9c8f38d`
- **Назначение:** Контрагенты -- юридические и физические лица, контакты, банки.
- **Зависимости:** Commons (`459fa497`), CoreEntities (`ec7b606a`), ExchangeCore (`bc0d1897`)
- **Сущности:** 10 (не считая коллекций)
- **Ключевые сущности:**
  - `Counterparty` -- Базовый тип контрагента (абстрактный)
  - `Company` -- Организация-контрагент (наследник CompanyBase)
  - `CompanyBase` -- Базовый тип организации
  - `Person` -- Физическое лицо
  - `Contact` -- Контактное лицо организации
  - `Bank` -- Банк
  - `CounterpartyKind` -- Вид контрагента (справочник)
  - `DueDiligenceWebsite` -- Сайт проверки контрагентов
  - `IdentityDocumentKind` -- Вид документа, удостоверяющего личность
- **AsyncHandlers:** UpdateContactName
- **Типы:** Справочники (Databook)
- **Коллекции:** ExchangeBoxes у Counterparty, Company, CompanyBase, Person, Bank

### Sungero.Company

- **NameGuid:** `d534e107-a54d-48ec-85ff-bc44d731a82f`
- **Назначение:** Наша организационная структура -- сотрудники, подразделения, НОО, замещения.
- **Зависимости:** Parties (`243b34ec`), CoreEntities (`ec7b606a`)
- **Сущности:** 15 (не считая коллекций)
- **Ключевые сущности:**
  - `Employee` -- Сотрудник
  - `Department` -- Подразделение
  - `BusinessUnit` -- Наша организация (НОО)
  - `JobTitle` -- Должность
  - `ManagersAssistant` -- Помощник руководителя
  - `ManagersAssistantBase` -- Базовый помощник
  - `Absence` -- Отсутствие (с коллекцией NotifyList)
  - `AbsenceReason` -- Причина отсутствия
  - `AbsenceTask` -- Задача на отсутствие
  - `VisibilityRule` -- Правило видимости
  - `VisibilitySetting` -- Настройка видимости
  - `UserBase` -- Базовый пользователь
  - `AccessRightsTransferSession` -- Сессия передачи прав
  - `ExternalApp` -- Внешнее приложение
  - `SystemSubstitutionQueueItem` -- Очередь системных замещений
- **AsyncHandlers:** UpdateEmployeeName, CheckTransferSubstitutedAccessRights
- **Reports:** ResponsibilitiesReport
- **Tasks:** AbsenceTask

---

## Документооборот

### Sungero.Docflow

- **NameGuid:** `df83a2ea-8d43-4ec4-a34a-2e61863014df`
- **Назначение:** Ядро документооборота. Типы документов, регистрация, согласование, правила доступа, хранение.
- **Зависимости:** Company (`d534e107`), CoreEntities (`ec7b606a`), Projects (`356e6500`)
- **Сущности:** ~90+ (крупнейший модуль, включая коллекции)
- **Ключевые сущности:**
  - **Документы:** `OfficialDocument`, `SimpleDocument`, `Memo`, `Addendum`, `AccountingDocumentBase`, `ContractualDocumentBase`, `CounterpartyDocument`, `InternalDocumentBase`, `PowerOfAttorney`, `PowerOfAttorneyRevocation`
  - **Согласование:** `ApprovalTask`, `ApprovalRule`, `ApprovalRuleBase`, `ApprovalStage`, `ApprovalStageBase`, `ApprovalRoleBase`, `ApprovalRole`
  - **Задания согласования:** `ApprovalAssignment`, `ApprovalCheckingAssignment`, `ApprovalCheckReturnAssignment`, `ApprovalExecutionAssignment`, `ApprovalManagerAssignment`, `ApprovalPrintingAssignment`, `ApprovalRegistrationAssignment`, `ApprovalReviewAssignment`, `ApprovalReworkAssignment`, `ApprovalSendingAssignment`, `ApprovalSigningAssignment`, `ApprovalSimpleAssignment`
  - **Справочники:** `DocumentKind`, `DocumentType`, `DocumentRegister`, `DocumentGroup`, `DocumentGroupBase`, `RegistrationGroup`, `RegistrationSetting`, `SignatureSetting`, `FileRetentionPeriod`, `StoragePolicy`, `StoragePolicyBase`, `DeliveryMethod`, `CaseFile`, `CaptureSource`
  - **Правила:** `AccessRightsRule`, `Condition`, `ConditionBase`
  - **Задачи:** `ApprovalTask`, `CheckReturnTask`, `DeadlineExtensionTask`, `FreeApprovalTask`, `SimpleTask`
  - **Отчёты:** `ApprovalRuleCardReport`, `ApprovalSheetReport`, `ExchangeOrderReport`, `MailRegisterReport`, `RegistrationSettingReport`, `SkippedNumbersReport`, `EnvelopeC4Report`, `EnvelopeC65Report`, `EnvelopeDL`
- **Jobs:** SendMailNotification, SendNotificationForExpiringPowerOfAttorney, TransferDocumentsByStoragePolicy, SendSummaryMailNotifications, GrantAccessRightsToDocuments, DeleteComparisonInfos, IndexDocumentsForFullTextSearch
- **AsyncHandlers:** множество (SetDocumentStorage, GrantAccessRightsToDocument, ConvertDocumentToPdf и др.)
- **Типы:** Документы, Задачи, Задания, Справочники, Отчёты, Jobs, AsyncHandlers

### Sungero.RecordManagement

- **NameGuid:** `4e25caec-c722-4740-bcfd-c4f803840ac6`
- **Назначение:** Делопроизводство -- входящие/исходящие письма, приказы, поручения, ознакомление, рассмотрение.
- **Зависимости:** Docflow (`df83a2ea`), CoreEntities (`ec7b606a`)
- **Сущности:** ~40+ (включая коллекции)
- **Ключевые сущности:**
  - **Документы:** `IncomingLetter`, `OutgoingLetter`, `CompanyDirective`, `Order`, `OrderBase`
  - **Поручения:** `ActionItemExecutionTask`, `ActionItemExecutionAssignment`, `ActionItemSupervisorAssignment`, `ActionItemSupervisorNotification`, `ActionItemObserversNotification`
  - **Рассмотрение:** `DocumentReviewTask`, `DocumentReviewAssignment`, `ReviewManagerAssignment`, `ReviewDraftResolutionAssignment`, `PreparingDraftResolutionAssignment`
  - **Ознакомление:** `AcquaintanceTask`, `AcquaintanceAssignment`, `AcquaintanceList`, `AcquaintanceFinishAssignment`
  - **Продление сроков:** `DeadlineExtensionTask`, `DeadlineExtensionAssignment`, `DeadlineRejectionAssignment`
  - **Запрос отчёта:** `StatusReportRequestTask`, `ReportRequestAssignment`, `ReportRequestCheckAssignment`
  - **Справочники:** `RecordManagementSetting`, `AcquaintanceTaskParticipant`, `ActionItemPredictionInfo`, `ActionItemTrainQueueItem`
- **Reports:** AcquaintanceFormReport, AcquaintanceReport, ActionItemPrintReport, ActionItemsExecutionReport, DocumentReturnReport, DraftResolutionReport, IncomingDocumentsProcessingReport, IncomingDocumentsReport, InternalDocumentsReport, OutgoingDocumentsReport
- **Типы:** Документы, Задачи, Задания, Справочники, Отчёты

### Sungero.RecordManagementUI

- **NameGuid:** `51247c94-981f-4bc8-819a-128704b5aa31`
- **Назначение:** UI-часть делопроизводства (виджеты, обложки, клиентские функции).
- **Зависимости:** RecordManagement (`4e25caec`), Docflow (`df83a2ea`)
- **Сущности:** UI-виджеты и действия (отдельных .mtd-сущностей нет или минимум)

### Sungero.Contracts

- **NameGuid:** `f9d15b1c-2784-4c84-8348-1e162d70b988`
- **Назначение:** Договорная работа -- договоры, доп. соглашения, счета.
- **Зависимости:** Docflow (`df83a2ea`), Parties (`243b34ec`), CoreEntities (`ec7b606a`)
- **Сущности:** ~15 (не считая коллекций)
- **Ключевые сущности:**
  - **Документы:** `ContractBase`, `Contract`, `SupAgreement`, `ContractualDocument`, `IncomingInvoice`, `OutgoingInvoice`
  - **Согласование:** `ContractsApprovalRule`, `ContractCondition`, `ContractApprovalRole`, `ApprovalIncInvoicePaidStage`
  - **Справочники:** `ContractCategory`
- **Коллекции:** Stages, Tracking, Versions, Milestones у документов; BusinessUnits, Conditions, Departments и др. у правил
- **Типы:** Документы, Справочники, Правила согласования

### Sungero.ContractsUI

- **NameGuid:** `3c8b7d3a-187d-4445-8a8c-1d00ece44556`
- **Назначение:** UI-часть договоров (обложки, виджеты, клиентские функции).
- **Зависимости:** Contracts (`f9d15b1c`)
- **Сущности:** UI-виджеты и обложки

### Sungero.FinancialArchive

- **NameGuid:** `59797aba-7718-45df-8ac1-5bb7a36c7a66`
- **Назначение:** Финансовый архив -- акты, накладные, счета-фактуры, платёжные поручения, УПД.
- **Зависимости:** Docflow (`df83a2ea`)
- **Сущности:** ~15 (не считая коллекций)
- **Ключевые сущности:**
  - `ContractStatement` -- Акт выполненных работ
  - `Waybill` -- Товарная накладная
  - `IncomingTaxInvoice` -- Входящий счёт-фактура
  - `OutgoingTaxInvoice` -- Исходящий счёт-фактура
  - `UniversalTransferDocument` -- УПД (универсальный передаточный документ)
  - `IncomingPaymentOrder` -- Входящее платёжное поручение
  - `OutgoingPaymentOrder` -- Исходящее платёжное поручение
  - `InternalAccountingDocumentBase` -- Базовый внутренний бухгалтерский документ
  - `FixedAssetDocument` -- Документ ОС
  - `AssetTransferDocument` -- Акт передачи ОС
  - `InventoryDocument` -- Инвентаризационная опись
  - `GeneralAccountingDocument` -- Общий бухгалтерский документ
  - `ReconciliationStatement` -- Акт сверки
- **Reports:** FinArchiveExportReport
- **Коллекции:** Tracking, Versions, CommissionMembers у документов
- **Типы:** Документы, Отчёты

### Sungero.FinancialArchiveUI

- **NameGuid:** `e99ae7e2-edb7-4904-a19a-4577f07609a4`
- **Назначение:** UI-часть финансового архива.
- **Зависимости:** FinancialArchive (`59797aba`), Docflow (`df83a2ea`)

---

## Согласование

### Sungero.DocflowApproval

- **NameGuid:** `62cf51a2-6371-4c12-9dec-68113862d5e1`
- **Назначение:** Процесс согласования документов -- задачи, задания, этапы. Расширяет базовое согласование из Docflow.
- **Зависимости:** Docflow (`df83a2ea`)
- **Сущности:** ~10 (не считая коллекций)
- **Ключевые сущности:**
  - `DocumentFlowTask` -- Задача на обработку документа (с коллекциями AddApprovers, Addressees, Observers, RevokedDocumentsRights)
  - `SimpleProcessTask` -- Задача на простую обработку
  - `EntityApprovalAssignment` -- Задание на согласование
  - `EntityReworkAssignment` -- Задание на доработку
  - `SigningAssignment` -- Задание на подписание
  - `AdvancedAssignment` -- Расширенное задание
  - `DocumentProcessingAssignment` -- Задание на обработку документа
  - `CheckReturnFromCounterpartyAssignment` -- Задание на проверку возврата
- **Типы:** Задачи, Задания

---

## Совещания и проекты

### Sungero.Meetings

- **NameGuid:** `593dcc11-15ee-49f2-b4ef-bf4cf7867055`
- **Назначение:** Совещания, повестки, протоколы.
- **Зависимости:** только DirectumRX
- **Сущности:** 3 (+ коллекции)
- **Ключевые сущности:**
  - `Meeting` -- Совещание (с коллекцией Members)
  - `Agenda` -- Повестка (документ, с Tracking, Versions)
  - `Minutes` -- Протокол (документ, с Tracking, Versions)
- **Типы:** Справочник (Meeting), Документы (Agenda, Minutes)

### Sungero.MeetingsUI

- **NameGuid:** `6ea9a047-b597-42eb-8f90-da8c559dd057`
- **Назначение:** UI совещаний.
- **Зависимости:** только DirectumRX

### Sungero.Projects

- **NameGuid:** `356e6500-45bc-482b-9791-189b5adedc28`
- **Назначение:** Управление проектами -- проекты, команды, проектные документы.
- **Зависимости:** Docflow (`df83a2ea`)
- **Сущности:** ~10 (не считая коллекций)
- **Ключевые сущности:**
  - `Project` -- Проект (с коллекциями TeamMembers, Classifier)
  - `ProjectCore` -- Базовый проект
  - `ProjectTeam` -- Команда проекта (группа)
  - `ProjectDocument` -- Проектный документ (с Tracking, Versions)
  - `ProjectKind` -- Вид проекта
  - `ProjectApprovalRole` -- Роль согласования для проектов
  - `ProjectQueueItemBase`, `ProjectRightsQueueItem`, `ProjectDocumentRightsQueueItem`, `ProjectMemberRightsQueueItem` -- Очереди прав
- **Типы:** Справочники, Документы, Группы

---

## Обмен и интеграция

### Sungero.Exchange

- **NameGuid:** `cec41b99-da21-422f-9332-0fbc423e95c0`
- **Назначение:** Обмен документами с контрагентами через операторов ЭДО.
- **Зависимости:** Docflow (`df83a2ea`), CoreEntities (`ec7b606a`)
- **Сущности:** ~7 (не считая коллекций)
- **Ключевые сущности:**
  - `ExchangeDocumentInfo` -- Информация об обмене (с коллекцией ServiceDocuments)
  - `ExchangeDocumentProcessingTask` -- Задача обработки входящего пакета
  - `ExchangeDocumentProcessingAssignment` -- Задание на обработку
  - `ReceiptNotificationSendingTask` -- Задача отправки извещений о получении
  - `ReceiptNotificationSendingAssignment` -- Задание на отправку извещений
  - `CancellationAgreement` -- Соглашение об аннулировании (документ)
- **Jobs:** BodyConverterJob, GetMessages, SendReceiptNotificationTasks, CreateReceiptNotifications, SendSignedReceiptNotifications, GetHistoricalMessages
- **Типы:** Справочники, Задачи, Задания, Документы, Jobs

### Sungero.ExchangeCore

- **NameGuid:** `bc0d1897-640a-4b4d-a43a-a23c5984a009`
- **Назначение:** Ядро обмена -- абонентские ящики, сервисы, очереди сообщений.
- **Зависимости:** Company (`d534e107`)
- **Сущности:** ~15 (не считая коллекций)
- **Ключевые сущности:**
  - `BoxBase` -- Базовый абонентский ящик
  - `BusinessUnitBox` -- Ящик нашей организации (с коллекциями ExchangeServiceCertificates, FormalizedPoAInfos)
  - `DepartmentBox` -- Ящик подразделения
  - `CounterpartyDepartmentBox` -- Ящик подразделения контрагента
  - `ExchangeService` -- Оператор ЭДО
  - `ExchangeDocumentType` -- Тип документа в обмене
  - `MessageQueueItem` -- Элемент очереди сообщений
  - `CounterpartyQueueItem` -- Элемент очереди контрагентов
  - `QueueItemBase` -- Базовый элемент очереди
  - `BodyConverterQueueItem` -- Очередь конвертации тел
  - `HistoricalMessagesDownloadSession` -- Сессия загрузки исторических сообщений
  - `IncomingInvitationTask` -- Задача входящего приглашения
  - `IncomingInvitationAssignment` -- Задание входящего приглашения
  - `CounterpartyConflictProcessingTask` -- Задача обработки конфликта контрагентов
  - `MessagesFilteringSetting` -- Настройка фильтрации сообщений
- **Типы:** Справочники, Задачи, Задания, QueueItems

### Sungero.ExchangeCoreDiadoc

- **NameGuid:** `30083842-5a15-4efb-9cab-0b61b1760157`
- **Назначение:** Провайдер обмена Диадок (Контур). Изолированный модуль.
- **Зависимости:** только DirectumRX
- **Сущности:** 0 (только серверная/изолированная логика)
- **IsVisible:** false, **IsLicensed:** true

### Sungero.ExchangeCoreSbis

- **NameGuid:** `d764569f-fa35-48be-aec9-d337b185d47a`
- **Назначение:** Провайдер обмена СБИС. Изолированный модуль.
- **Зависимости:** только DirectumRX
- **Сущности:** 0
- **IsVisible:** false, **IsLicensed:** true

### Sungero.Integration1C

- **NameGuid:** `f7b1d5b7-5af1-4a9f-b4d7-4e18840d7195`
- **Назначение:** Интеграция с 1С -- синхронизация документов и справочников.
- **Зависимости:** CoreEntities (`ec7b606a`), Capture (`92491aa6`)
- **Сущности:** 0 (только Module.mtd)
- **IsVisible:** false, **IsLicensed:** true
- **Особенности:** Содержит серверную и изолированную логику без собственных типов.

### Sungero.IntegrationDcs

- **NameGuid:** `9ebe1af0-4286-4ab6-8975-68040cfa3e91`
- **Назначение:** Интеграция с DCS (Directum Cloud Services).
- **Зависимости:** только DirectumRX
- **Сущности:** 0
- **IsVisible:** false, **IsLicensed:** true

---

## Интеллектуальная обработка

### Sungero.Intelligence

- **NameGuid:** `e08dc659-2828-4d50-b90d-7d06408ab7cb`
- **Назначение:** Интеллектуальная обработка -- AI-помощники, классификация.
- **Зависимости:** только DirectumRX
- **Сущности:** 1 (+ коллекция)
- **Ключевые сущности:**
  - `AIManagersAssistant` -- AI-помощник руководителя (с коллекцией Classifiers)
- **IsLicensed:** true

### Sungero.SmartProcessing

- **NameGuid:** `bb685d97-a673-42ea-8605-66889967467f`
- **Назначение:** Интеллектуальная обработка документов -- распознавание, верификация, перекомплектация.
- **Зависимости:** Capture (`92491aa6`), CoreEntities (`ec7b606a`), Docflow (`df83a2ea`)
- **Сущности:** ~7 (не считая коллекций)
- **Ключевые сущности:**
  - `Blob` -- Бинарный объект
  - `BlobPackage` -- Пакет бинарных объектов (с коллекциями Blobs, To, CC)
  - `ExtractTextQueueItem` -- Очередь извлечения текста
  - `RepackingSession` -- Сессия перекомплектации (с коллекциями OriginalDocuments, NewDocuments, Errors)
  - `VerificationTask` -- Задача верификации
  - `VerificationAssignment` -- Задание верификации
- **Типы:** Задачи, Задания, QueueItems

---

## Прочие модули

### Sungero.MobileApps

- **NameGuid:** `1a7ef5ec-c6f4-47df-98c1-b3eae77dabae`
- **Назначение:** Настройки мобильных приложений.
- **Зависимости:** только DirectumRX
- **Сущности:** ~2 (исходя из ExplorerTreeOrder)

### Sungero.PowerOfAttorneyCore

- **NameGuid:** `1ecb3185-14ae-422d-99c6-babcf2ab059f`
- **Назначение:** Ядро машиночитаемых доверенностей (МЧД).
- **Зависимости:** только DirectumRX
- **Сущности:** ~3

### Sungero.PowerOfAttorneyKontur

- **NameGuid:** `a37bcb31-c5ce-4052-af97-ab7cbd19bf27`
- **Назначение:** Провайдер МЧД через сервис Контур.
- **Зависимости:** только DirectumRX
- **Сущности:** 0
- **IsVisible:** false, **IsLicensed:** true

### Sungero.InternalPolicies

- **NameGuid:** `48c9a380-db0e-47ca-ae0b-4015bbced723`
- **Назначение:** Локальные нормативные акты (ЛНА) -- ознакомление сотрудников с политиками.
- **Зависимости:** только DirectumRX
- **Сущности:** ~5

### Sungero.InternalPoliciesUI

- **NameGuid:** `fc7d414c-a708-4d06-898e-e839ccd4d720`
- **Назначение:** UI для локальных нормативных актов.
- **Зависимости:** InternalPolicies (`48c9a380`)
- **IsVisible:** false, **IsLicensed:** true

---

## Рекомендации по наследованию

При создании кастомных пакетов разработки выбирайте зависимости исходя из задачи:

### Документооборот / Делопроизводство

```
Dependencies: [Docflow, RecordManagement]
```

Когда нужно: новые типы документов, кастомные правила регистрации, расширение рассмотрения/поручений.

### Договоры

```
Dependencies: [Contracts, Docflow]
```

Когда нужно: новые типы договорных документов, кастомные правила согласования договоров, этапы.

### Финансовые документы

```
Dependencies: [FinancialArchive, Docflow]
```

Когда нужно: новые типы финансовых документов, кастомная обработка бухгалтерских документов.

### Организационная структура

```
Dependencies: [Company]
```

Когда нужно: расширение карточки сотрудника, новые справочники оргструктуры.

### Контрагенты

```
Dependencies: [Parties]
```

Когда нужно: расширение карточки контрагента, новые виды контрагентов.

### Минимальный модуль (справочники, простые задачи)

```
Dependencies: [Commons] или [Docflow]
```

Когда нужно: изолированный справочник или простая задача без привязки к документообороту.

### Модуль с обменом

```
Dependencies: [Exchange, ExchangeCore, Docflow]
```

Когда нужно: кастомная обработка входящих пакетов ЭДО, расширение логики обмена.

### Совещания

```
Dependencies: [Meetings]
```

Когда нужно: расширение совещаний, кастомные типы протоколов.

### Проекты

```
Dependencies: [Projects, Docflow]
```

Когда нужно: расширение проектов, кастомные проектные документы.

---

## Паттерны модулей-пар (Base + UI)

Ряд модулей разделён на серверную (Base) и UI-часть:

| Base-модуль | UI-модуль |
|-------------|-----------|
| Sungero.RecordManagement | Sungero.RecordManagementUI |
| Sungero.Contracts | Sungero.ContractsUI |
| Sungero.FinancialArchive | Sungero.FinancialArchiveUI |
| Sungero.Meetings | Sungero.MeetingsUI |
| Sungero.InternalPolicies | Sungero.InternalPoliciesUI |

**Правило:** UI-модуль зависит от Base-модуля. Если вы перекрываете сущность из Base-модуля, добавляйте зависимость от Base. Если нужно перекрыть UI (формы, действия, клиентские функции) -- также от UI-модуля.

---

## Важные замечания

1. **DirectumRX всегда в Dependencies.** Все модули имеют `"IsSolutionModule": true` на зависимость от DirectumRX.

2. **CoreEntities и Capture -- внешние.** Модули `ec7b606a` (CoreEntities) и `92491aa6` (Capture) не представлены в `archive/base/` как отдельные папки. Это встроенные модули платформы.

3. **IsVisible: false** у ряда модулей (ExchangeCoreDiadoc, ExchangeCoreSbis, Integration1C, IntegrationDcs, PowerOfAttorneyKontur, InternalPoliciesUI). Эти модули скрыты из навигации и работают как провайдеры/плагины.

4. **IsLicensed: true** указывает на лицензируемые модули. Они доступны только при наличии соответствующей лицензии.

5. **При определении зависимостей для нового пакета** берите минимально необходимый набор. Не нужно зависеть от Shell (это верхний навигационный модуль). Зависьте от конкретных прикладных модулей.
