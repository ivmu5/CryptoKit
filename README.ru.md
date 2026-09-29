# CryptoKit

<p align="right">
  <a href="README.md">English</a> | <strong>Русский</strong>
</p>

CryptoKit — библиотека .NET 10 для генерации, хранения и получения криптографического ключевого материала AES, HMAC и RSA.

Библиотека отделяет работу с ключами от конкретного способа их хранения: алгоритмические провайдеры работают через общий [`IKeyStorage`](CryptoKit/Storage/IKeyStorage.cs), а встроенный [`FileKeyStorage`](CryptoKit/Storage/FileKeyStorage.cs) предоставляет файловую реализацию. Секретный материал представлен объектами с явным временем жизни и очисткой памяти при `Dispose`.

CryptoKit управляет **ключевым материалом**, но не реализует операции шифрования, вычисления HMAC, RSA-подписи или RSA-шифрования. Полученные ключи предназначены для передачи в криптографические примитивы .NET или другой потребляющий код.

## Содержание

- [Основные возможности](#основные-возможности)
- [Архитектура](#архитектура)
- [Навигация по репозиторию](#навигация-по-репозиторию)
- [Основные потоки выполнения](#основные-потоки-выполнения)
- [Публичный API](#публичный-api)
- [Жизненный цикл и состояние](#жизненный-цикл-и-состояние)
- [Асинхронность и конкурентный доступ](#асинхронность-и-конкурентный-доступ)
- [Ограничения и важные особенности](#ограничения-и-важные-особенности)
- [Тестирование](#тестирование)
- [Точки изменения и расширения](#точки-изменения-и-расширения)
- [Зависимости](#зависимости)

## Основные возможности

- Генерация AES-ключей размером 128, 192 или 256 бит через [`AesKeyGenerator`](CryptoKit/Aes/AesKeyGenerator.cs).
- Генерация HMAC-ключей размером от 128 до 65 536 бит через [`HmacKeyGenerator`](CryptoKit/Hmac/HmacKeyGenerator.cs).
- Генерация RSA-пар размером не менее 2048 бит через [`RsaKeyGenerator`](CryptoKit/Rsa/RsaKeyGenerator.cs); значение по умолчанию — 3072 бита.
- Асинхронная загрузка существующих ключей и атомарный сценарий `get-or-create` через [`IAesKeyProvider`](CryptoKit/Aes/Provider/IAesKeyProvider.cs), [`IHmacKeyProvider`](CryptoKit/Hmac/Provider/IHmacKeyProvider.cs) и [`IRsaKeyProvider`](CryptoKit/Rsa/Provider/IRsaKeyProvider.cs).
- Алгоритм-независимое хранение бинарного ключевого материала через [`IKeyStorage`](CryptoKit/Storage/IKeyStorage.cs).
- Файловое хранилище с create-only публикацией, атомарной заменой, ограничением размера записей и owner-only Unix permissions через [`FileKeyStorage`](CryptoKit/Storage/FileKeyStorage.cs).
- Явное владение секретными буферами и их очистка через [`SecretKeyMaterial`](CryptoKit/Secrets/SecretKeyMaterial.cs).
- Регистрация встроенных компонентов через `Microsoft.Extensions.DependencyInjection`.

## Архитектура

```text
Application
    │
    ├── IAesKeyProvider ──► AesKeyProvider ───────┐
    ├── IHmacKeyProvider ─► HmacKeyProvider ──────┤
    └── IRsaKeyProvider ──► RsaKeyProvider ───────┤
                                                  │
                    ┌─────────────────────────────┘
                    ▼
               IKeyStorage
                    │
                    └──► FileKeyStorage

Generators
    ├── AesKeyGenerator
    ├── HmacKeyGenerator
    └── RsaKeyGenerator

Secret material
    ├── AesKey ─────┐
    ├── HmacKey ────┴──► SecretKeyMaterial
    └── RsaKeyPair ────► private key uses SecretKeyMaterial
```

| Подсистема | Ответственность | Ключевые типы |
|---|---|---|
| AES | Генерация, валидация, загрузка и создание AES-ключей | [`AesKey`](CryptoKit/Aes/AesKey.cs), [`AesKeyGenerator`](CryptoKit/Aes/AesKeyGenerator.cs), [`IAesKeyProvider`](CryptoKit/Aes/Provider/IAesKeyProvider.cs) |
| HMAC | Генерация, валидация, загрузка и создание HMAC-ключей | [`HmacKey`](CryptoKit/Hmac/HmacKey.cs), [`HmacKeyGenerator`](CryptoKit/Hmac/HmacKeyGenerator.cs), [`IHmacKeyProvider`](CryptoKit/Hmac/Provider/IHmacKeyProvider.cs) |
| RSA | Генерация RSA-пар, хранение private key и вывод public key | [`RsaKeyPair`](CryptoKit/Rsa/RsaKeyPair.cs), [`RsaKeyGenerator`](CryptoKit/Rsa/RsaKeyGenerator.cs), [`IRsaKeyProvider`](CryptoKit/Rsa/Provider/IRsaKeyProvider.cs) |
| Secret material | Владение секретным буфером, копирование, экспорт и детерминированная очистка | [`SecretKeyMaterial`](CryptoKit/Secrets/SecretKeyMaterial.cs) |
| Storage | Общий контракт хранения и файловая реализация | [`IKeyStorage`](CryptoKit/Storage/IKeyStorage.cs), [`FileKeyStorage`](CryptoKit/Storage/FileKeyStorage.cs) |
| Internal orchestration | Сериализация операций по одному key ID, bounded retry и общий workflow AES/HMAC | [`StoredSecretKeyProvider<TKey>`](CryptoKit/Internal/StoredSecretKeyProvider.cs), [`KeyedLock`](CryptoKit/Internal/KeyedLock.cs), [`KeyCreationRetryPolicy`](CryptoKit/Internal/KeyCreationRetryPolicy.cs) |
| DI | Регистрация storage, генераторов и singleton-провайдеров | [`StorageServiceCollectionExtensions`](CryptoKit/Storage/StorageServiceCollectionExtensions.cs), [`AesServiceCollectionExtensions`](CryptoKit/Aes/AesServiceCollectionExtensions.cs), [`HmacServiceCollectionExtensions`](CryptoKit/Hmac/HmacServiceCollectionExtensions.cs), [`RsaServiceCollectionExtensions`](CryptoKit/Rsa/RsaServiceCollectionExtensions.cs) |

AES и HMAC используют общий внутренний [`StoredSecretKeyProvider<TKey>`](CryptoKit/Internal/StoredSecretKeyProvider.cs). RSA имеет отдельную реализацию [`RsaKeyProvider`](CryptoKit/Rsa/Provider/RsaKeyProvider.cs), поскольку в хранилище помещается только private key, а public key каждый раз выводится из него.

<details>
<summary>Подробнее о разделении ответственности</summary>

### Алгоритмические типы

`AesKey`, `HmacKey` и `RsaKeyPair` отвечают за представление и проверку уже существующего ключевого материала. Генераторы отвечают только за создание нового материала.

Провайдеры объединяют генерацию с `IKeyStorage` и добавляют семантику:

- `Get...Async` — разрешена только загрузка существующего ключа;
- `GetOrCreate...Async` — при отсутствии записи разрешена генерация и атомарная публикация нового ключа.

Это разделение позволяет явно отличать сценарий использования уже подготовленного ключа от сценария, в котором приложение имеет право создать новый секрет.

### Storage

`IKeyStorage` не знает об AES, HMAC или RSA. Он работает с `string keyId` и бинарными данными. Алгоритмические провайдеры формируют собственные storage IDs:

- AES: `<keyId>.aes`;
- HMAC: `<keyId>.hmac`;
- RSA: `<keyId>.private`.

`FileKeyStorage` дополнительно преобразует полный storage ID в SHA-256 от strict UTF-8 представления и использует полученный hex как имя файла. Исходный логический идентификатор поэтому не становится именем файла.

### RSA

`RsaKeyGenerator` создаёт:

- private key в PKCS#8;
- public key в SubjectPublicKeyInfo.

`RsaKeyProvider` сохраняет только PKCS#8 private key. При запросе public key приватный материал загружается, импортируется через `RSA.ImportPkcs8PrivateKey`, после чего public key снова экспортируется в SubjectPublicKeyInfo.

</details>

## Навигация по репозиторию

```text
CryptoKit.slnx
├── CryptoKit/
│   ├── Aes/          # AES key material, generator, provider, options, DI
│   ├── Hmac/         # HMAC key material, generator, provider, options, DI
│   ├── Rsa/          # RSA pair, generator, provider, options, DI
│   ├── Secrets/      # Общая модель владения секретным материалом
│   ├── Storage/      # IKeyStorage и FileKeyStorage
│   └── Internal/     # Общая orchestration-логика и синхронизация
└── CryptoKit.Tests/
    ├── Aes/
    ├── Hmac/
    ├── Rsa/
    ├── Secrets/
    ├── Storage/
    ├── Internal/
    ├── DependencyInjection/
    └── Helpers/
```

Основная библиотека находится в [`CryptoKit`](CryptoKit/), тесты — в [`CryptoKit.Tests`](CryptoKit.Tests/). Оба проекта нацелены на `net10.0`.

## Основные потоки выполнения

### AES/HMAC: загрузить или создать ключ

```text
IAesKeyProvider.GetOrCreateKeyAsync()
    или IHmacKeyProvider.GetOrCreateKeyAsync()
        ↓
AesKeyProvider / HmacKeyProvider
        ↓
StoredSecretKeyProvider<TKey>
        ↓
KeyedLock.AcquireAsync(keyId)
        ↓
IKeyStorage.TryLoadAsync()
        ├── запись найдена ──► material validation ──► key object
        └── записи нет
                ↓
             generator
                ↓
        IKeyStorage.CreateAsync()
        ├── true  ──► созданный key object
        └── false ──► загрузка ключа победившего writer-а
```

### RSA: получить или создать public key

```text
IRsaKeyProvider.GetOrCreatePublicKeyAsync()
        ↓
RsaKeyProvider
        ↓
KeyedLock.AcquireAsync(keyId)
        ↓
IKeyStorage.TryLoadAsync(keyId + ".private")
        ├── private key найден ──► validate/import ──► derive public key
        └── записи нет
                ↓
        RsaKeyGenerator.Generate()
                ↓
        IKeyStorage.CreateAsync(private key)
                ↓
        public key возвращается без публикации отдельной public-key записи
```

### Файловая публикация записи

```text
FileKeyStorage.CreateAsync() / ReplaceAsync()
        ↓
создание temporary file в той же директории
        ↓
WriteAsync() + FlushAsync()
        ↓
закрытие temporary file
        ↓
последняя проверка CancellationToken
        ↓
File.Move(overwrite: false) или File.Replace()
        ↓
best-effort cleanup temporary file
```

<details>
<summary>Конкурентный create-or-load workflow</summary>

Локальный `KeyedLock` предотвращает дублирующую работу внутри одного экземпляра провайдера, но не является механизмом межпроцессной синхронизации.

Корректность при нескольких экземплярах провайдера или нескольких процессах основана на контракте `IKeyStorage.CreateAsync`: проверка отсутствия записи и её создание должны быть одной атомарной storage-операцией.

Если `CreateAsync` возвращает `false`, провайдер считает, что другой участник выиграл гонку, и пытается загрузить опубликованный им ключ. Если запись уже была удалена между проигранным `CreateAsync` и последующим чтением, провайдер повторяет публикацию того же кандидата.

Количество таких попыток ограничено [`KeyCreationRetryPolicy`](CryptoKit/Internal/KeyCreationRetryPolicy.cs): максимум восемь create-only попыток. После исчерпания лимита выбрасывается `InvalidOperationException`.

</details>

## Публичный API

### Регистрация через DI

Встроенный путь интеграции использует `Microsoft.Extensions.DependencyInjection`:

```csharp
using CryptoKit.Aes;
using CryptoKit.Hmac;
using CryptoKit.Rsa;
using CryptoKit.Storage;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services
    .AddCryptoKitFileStorage(options =>
    {
        options.DirectoryPath = Path.Combine(
            AppContext.BaseDirectory,
            "keys");
    })
    .AddCryptoKitAes(options =>
    {
        options.KeySize = 256;
    })
    .AddCryptoKitHmac(options =>
    {
        options.KeySize = 256;
    })
    .AddCryptoKitRsa(options =>
    {
        options.KeySize = 3072;
    });

using var serviceProvider = services.BuildServiceProvider();
```

[`AddCryptoKitFileStorage`](CryptoKit/Storage/StorageServiceCollectionExtensions.cs) регистрирует singleton `IKeyStorage`. Алгоритмические `AddCryptoKit...` методы регистрируют соответствующий generator и singleton provider. Один provider обслуживает любое количество логических key IDs.

### Получение AES-ключа

```csharp
var keys = serviceProvider.GetRequiredService<IAesKeyProvider>();

using var key = await keys.GetOrCreateKeyAsync("data-encryption");

var rawKey = key.Export();
try
{
    // Передать rawKey потребляющему криптографическому коду.
}
finally
{
    System.Security.Cryptography.CryptographicOperations.ZeroMemory(rawKey);
}
```

`GetKeyAsync` не создаёт отсутствующий ключ и выбрасывает `KeyNotFoundException`. `GetOrCreateKeyAsync` имеет право создать новый ключ.

### Получение HMAC-ключа

```csharp
var keys = serviceProvider.GetRequiredService<IHmacKeyProvider>();

using var key = await keys.GetOrCreateKeyAsync("request-signing");
```

### Получение RSA public key без выдачи private key вызывающему коду

```csharp
var keys = serviceProvider.GetRequiredService<IRsaKeyProvider>();

byte[] publicKey = await keys.GetOrCreatePublicKeyAsync("token-signing");
```

Если RSA-пары ещё нет, метод создаст пару и сохранит только private key. Возвращаемое значение содержит public key в SubjectPublicKeyInfo.

<details>
<summary>Прямая генерация без storage и DI</summary>

Все генераторы доступны напрямую:

```csharp
using CryptoKit.Aes;
using CryptoKit.Hmac;
using CryptoKit.Rsa;

var aesGenerator = new AesKeyGenerator();
using var aesKey = aesGenerator.Generate(256);

var hmacGenerator = new HmacKeyGenerator();
using var hmacKey = hmacGenerator.Generate(256);

var rsaGenerator = new RsaKeyGenerator();
using var rsaPair = rsaGenerator.Generate(3072);
```

Размеры по умолчанию:

| Алгоритм | Значение по умолчанию | Допустимые значения |
|---|---:|---|
| AES | 256 бит | 128, 192, 256 |
| HMAC | 256 бит | 128–65 536 бит, кратно 8 |
| RSA | 3072 бита | не менее 2048 бит и только размеры, поддерживаемые текущей реализацией `RSA` |

Ограничение 65 536 бит для HMAC относится только к **генерации**. Уже существующий HMAC key material может быть больше, если он не короче 128 бит и его размер кратен восьми.

</details>

<details>
<summary>Собственная реализация IKeyStorage</summary>

Для подключения secure store, keychain, KMS или другого backend необходимо реализовать [`IKeyStorage`](CryptoKit/Storage/IKeyStorage.cs) и зарегистрировать реализацию в DI вместо `FileKeyStorage`.

Критичные требования контракта:

- `TryLoadAsync` возвращает новый caller-owned `byte[]` либо `null`;
- `CreateAsync` должен быть атомарным create-only и не может быть реализован как `TryLoadAsync` + обычная запись;
- `CreateAsync` возвращает `false`, если запись уже существует, не изменяя её;
- `ReplaceAsync` заменяет только существующую запись и выбрасывает `KeyNotFoundException`, если её нет;
- `DeleteAsync` идемпотентен;
- backend не должен удерживать ссылку на переданный caller-owned buffer после завершения create/replace;
- после фактически завершённого commit операция не должна сообщать cancellation так, будто side effect не произошёл.

Эти свойства являются частью корректности алгоритмических `GetOrCreate...Async` провайдеров.

</details>

## Жизненный цикл и состояние

[`SecretKeyMaterial`](CryptoKit/Secrets/SecretKeyMaterial.cs) владеет собственным секретным `byte[]`. `AesKey` и `HmacKey` копируют переданный материал в этот буфер; внешний исходный массив не становится внутренним хранилищем объекта.

`Dispose` очищает owned buffer через `CryptographicOperations.ZeroMemory` и делает дальнейший `Export`/`CopyTo` недоступным. Повторный `Dispose` безопасен. Финализатор выполняет best-effort очистку, если объект не был освобождён явно, но не заменяет детерминированный `Dispose`.

`Export()` и `ExportPrivateKey()` создают **новые caller-owned массивы**. Их очистка после использования является ответственностью вызывающего кода.

[`RsaKeyPair`](CryptoKit/Rsa/RsaKeyPair.cs) очищает при `Dispose` private key. Public key не считается секретом и остаётся доступным через `ExportPublicKey` и `CopyPublicKeyTo` после освобождения private material.

Провайдеры, зарегистрированные стандартными DI-extension methods, имеют singleton lifetime и сохраняют снимок настроек размера ключа, сделанный во время регистрации. Последующее изменение исходного options-объекта на уже созданный provider не влияет.

<details>
<summary>Состояние FileKeyStorage</summary>

`FileKeyStorage` хранит только:

- абсолютный путь storage-директории;
- максимальный размер одной записи;
- внутренние test hooks, если экземпляр создан из тестовой сборки.

Само содержимое ключей не кэшируется в памяти между операциями.

Для каждого create/replace создаётся временный файл в той же директории. После публикации временный файл удаляется best-effort. Остаточный `.tmp` файл возможен, если cleanup физически не удаётся; ошибка cleanup не заменяет результат уже завершённой основной операции.

</details>

## Асинхронность и конкурентный доступ

Провайдеры и `IKeyStorage` используют `ValueTask` и принимают `CancellationToken` для операций хранения и ожидания блокировок.

[`KeyedLock`](CryptoKit/Internal/KeyedLock.cs) сериализует операции с одним и тем же логическим key ID внутри одного экземпляра провайдера. Разные key IDs используют разные семафоры и не блокируют друг друга. Записи блокировок reference-counted и удаляются после освобождения последнего владельца или waiter-а.

Между отдельными provider/storage instances и процессами корректность create-only гонок должна обеспечиваться самим storage backend через атомарный `IKeyStorage.CreateAsync`.

Для операций с необратимым side effect действует явная cancellation boundary: отмена проверяется до commit. После успешного commit встроенное файловое хранилище не превращает результат в `OperationCanceledException`, даже если token был отменён сразу после изменения файла.

## Ограничения и важные особенности

- `FileKeyStorage` хранит переданные бинарные данные **без дополнительного шифрования содержимого файла**. SHA-256 используется только для преобразования логического ID в имя файла и не защищает key material at rest.
- На Unix-подобных системах директория приводится к `rwx------`, а key-файлы — к `rw-------`. На Windows `FileKeyStorage` не настраивает ACL.
- Удаление файла не гарантирует физическое стирание старых байтов с носителя, snapshot-ов или резервных копий.
- Алгоритмические providers предоставляют загрузку и `get-or-create`, но не содержат публичного provider-level API для ротации, retirement или удаления ключей.
- `FileKeyStorageOptions.DirectoryPath` должен быть настроен явно; значения по умолчанию для директории нет.
- Максимальный размер одной записи `FileKeyStorage` по умолчанию — 64 KiB; лимит применяется и при чтении, и при записи.
- Key ID не может быть пустым/whitespace и должен содержать корректную UTF-16 последовательность.
- RSA private material должен быть PKCS#8, public material — SubjectPublicKeyInfo. Импортированные RSA private keys размером менее 2048 бит отклоняются.

## Тестирование

Тесты находятся в проекте [`CryptoKit.Tests`](CryptoKit.Tests/CryptoKit.Tests.csproj) и используют xUnit. Набор покрывает генераторы, key material, providers, DI, синхронизацию и файловое хранилище.

Основные проверяемые гарантии:

- корректные и некорректные размеры AES/HMAC/RSA ключей;
- копирование и очистка secret material;
- совпадение RSA private/public pair и сохранение доступности public key после `Dispose`;
- отсутствие неявной регенерации уже сохранённых ключей при изменении generation options;
- convergence нескольких providers к одной storage-записи;
- bounded retry при постоянной create/delete contention;
- атомарные create/replace semantics `FileKeyStorage`;
- cancellation непосредственно до и после commit boundary;
- hashed physical file names и ограничение размера записей;
- owner-only file modes на Unix;
- регистрация сервисов через DI.

Запуск полного набора:

```bash
dotnet test CryptoKit.slnx
```

Сбор покрытия через подключённый Coverlet collector:

```bash
dotnet test CryptoKit.slnx --collect:"XPlat Code Coverage"
```

[`FileKeyStorageTests`](CryptoKit.Tests/Storage/FileKeyStorageTests.cs) работают с реальными временными директориями локальной файловой системы. Внешние сервисы или отдельная конфигурация для тестового набора не используются; Unix-specific проверки не выполняют соответствующие assertions на Windows.

<details>
<summary>Организация тестов и test doubles</summary>

- [`Aes`](CryptoKit.Tests/Aes/) — `AesKey`, generator и provider.
- [`Hmac`](CryptoKit.Tests/Hmac/) — `HmacKey`, generator и provider.
- [`Rsa`](CryptoKit.Tests/Rsa/) — pair validation, generator и provider.
- [`Secrets`](CryptoKit.Tests/Secrets/) — общий lifetime/ownership контракт `SecretKeyMaterial`.
- [`Storage`](CryptoKit.Tests/Storage/) — файловый backend и encoding storage IDs.
- [`Internal`](CryptoKit.Tests/Internal/) — `KeyIdValidator` и `KeyedLock`.
- [`DependencyInjection`](CryptoKit.Tests/DependencyInjection/) — стандартные service collection extensions.

[`InMemoryKeyStorage`](CryptoKit.Tests/Helpers/InMemoryKeyStorage.cs) — thread-safe test implementation `IKeyStorage`, используемая для provider tests без файловой системы.

[`AlwaysContendedKeyStorage`](CryptoKit.Tests/Helpers/AlwaysContendedKeyStorage.cs) всегда проигрывает `CreateAsync` и никогда не возвращает winning record. Он фиксирует гарантию bounded retry и предотвращает появление бесконечного цикла при патологической внешней конкуренции.

[`TemporaryDirectory`](CryptoKit.Tests/Helpers/TemporaryDirectory.cs) создаёт изолированные директории для filesystem tests.

При изменении storage semantics тесты следует добавлять прежде всего в `CryptoKit.Tests/Storage`. При изменении algorithm provider workflow — в соответствующий `Aes`, `Hmac` или `Rsa` набор. Изменения общей синхронизации и key-ID validation должны сопровождаться тестами в `CryptoKit.Tests/Internal`.

</details>

## Точки изменения и расширения

| Задача | Основные точки изменения |
|---|---|
| Добавить новый storage backend | [`IKeyStorage`](CryptoKit/Storage/IKeyStorage.cs), затем DI-регистрация по аналогии с [`StorageServiceCollectionExtensions`](CryptoKit/Storage/StorageServiceCollectionExtensions.cs); тесты — [`CryptoKit.Tests/Storage`](CryptoKit.Tests/Storage/) |
| Изменить файловую atomicity, naming, permissions или size limits | [`FileKeyStorage`](CryptoKit/Storage/FileKeyStorage.cs), [`FileKeyNameEncoder`](CryptoKit/Storage/Internal/FileKeyNameEncoder.cs), [`FileKeyStorageOptions`](CryptoKit/Storage/Options/FileKeyStorageOptions.cs); тесты — [`FileKeyStorageTests`](CryptoKit.Tests/Storage/FileKeyStorageTests.cs) |
| Изменить общий AES/HMAC get-or-create workflow | [`StoredSecretKeyProvider<TKey>`](CryptoKit/Internal/StoredSecretKeyProvider.cs), [`KeyCreationRetryPolicy`](CryptoKit/Internal/KeyCreationRetryPolicy.cs), [`KeyedLock`](CryptoKit/Internal/KeyedLock.cs) |
| Изменить AES policy или generation defaults | [`AesKeyOptions`](CryptoKit/Aes/Options/AesKeyOptions.cs), [`AesKeyGenerator`](CryptoKit/Aes/AesKeyGenerator.cs), [`AesKey`](CryptoKit/Aes/AesKey.cs); тесты — [`CryptoKit.Tests/Aes`](CryptoKit.Tests/Aes/) |
| Изменить HMAC policy или generation defaults | [`HmacKeyOptions`](CryptoKit/Hmac/Options/HmacKeyOptions.cs), [`HmacKeyGenerator`](CryptoKit/Hmac/HmacKeyGenerator.cs), [`HmacKey`](CryptoKit/Hmac/HmacKey.cs); тесты — [`CryptoKit.Tests/Hmac`](CryptoKit.Tests/Hmac/) |
| Изменить RSA persistence, validation или public-key derivation | [`RsaKeyProvider`](CryptoKit/Rsa/Provider/RsaKeyProvider.cs), [`RsaKeyPair`](CryptoKit/Rsa/RsaKeyPair.cs), [`RsaKeyGenerator`](CryptoKit/Rsa/RsaKeyGenerator.cs), [`RsaKeyOptions`](CryptoKit/Rsa/Options/RsaKeyOptions.cs); тесты — [`CryptoKit.Tests/Rsa`](CryptoKit.Tests/Rsa/) |
| Изменить правила владения и очистки secret buffers | [`SecretKeyMaterial`](CryptoKit/Secrets/SecretKeyMaterial.cs); тесты — [`SecretKeyMaterialTests`](CryptoKit.Tests/Secrets/SecretKeyMaterialTests.cs) |
| Добавить новый алгоритмический provider | Использовать [`IKeyStorage`](CryptoKit/Storage/IKeyStorage.cs); для symmetric secret material переиспользовать подход [`StoredSecretKeyProvider<TKey>`](CryptoKit/Internal/StoredSecretKeyProvider.cs); DI — по образцу существующих `AddCryptoKit...` extensions |
| Изменить стандартные DI lifetimes или option snapshotting | [`KeyProviderServiceCollectionRegistration`](CryptoKit/Internal/KeyProviderServiceCollectionRegistration.cs) и соответствующие service collection extensions; тесты — [`ServiceCollectionExtensionsTests`](CryptoKit.Tests/DependencyInjection/ServiceCollectionExtensionsTests.cs) |

При изменении `IKeyStorage` необходимо сохранять его atomic create-only контракт: на нём основана межэкземплярная и межпроцессная корректность `GetOrCreate...Async`.

## Зависимости

Основная библиотека нацелена на **.NET 10** и имеет одну прямую NuGet-зависимость:

| Пакет | Версия | Назначение | Лицензия |
|---|---:|---|---|
| [`Microsoft.Extensions.DependencyInjection`](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/10.0.12) | 10.0.12 | `IServiceCollection`, singleton registrations и DI integration | MIT |

Криптографические операции генерации, импорта, экспорта и очистки памяти основаны на `System.Security.Cryptography` из .NET.

<details>
<summary>Зависимости тестового проекта</summary>

| Пакет | Версия | Назначение | Лицензия |
|---|---:|---|---|
| [`Microsoft.NET.Test.Sdk`](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/17.14.1) | 17.14.1 | .NET test platform | MIT |
| [`xunit`](https://www.nuget.org/packages/xunit/2.9.3) | 2.9.3 | Test framework | Apache-2.0 |
| [`xunit.runner.visualstudio`](https://www.nuget.org/packages/xunit.runner.visualstudio/3.1.4) | 3.1.4 | VSTest/Visual Studio adapter | Apache-2.0 |
| [`coverlet.collector`](https://www.nuget.org/packages/coverlet.collector/6.0.4) | 6.0.4 | Code coverage collector | MIT |

Эти зависимости относятся к непакуемому проекту `CryptoKit.Tests` и не являются частью публичного runtime API CryptoKit.

</details>
