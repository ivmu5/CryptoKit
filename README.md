# CryptoKit

<p align="right">
  <strong>English</strong> | <a href="README.ru.md">Русский</a>
</p>

CryptoKit is a .NET 10 library for generating, storing, and retrieving AES, HMAC, and RSA cryptographic key material.

The library separates key-management logic from the concrete storage mechanism: algorithm-specific providers operate through the shared [`IKeyStorage`](CryptoKit/Storage/IKeyStorage.cs) abstraction, while the built-in [`FileKeyStorage`](CryptoKit/Storage/FileKeyStorage.cs) provides a file-system implementation. Secret material is represented by objects with explicit lifetime management and memory clearing on `Dispose`.

CryptoKit manages **key material**, but does not implement encryption operations, HMAC computation, RSA signing, or RSA encryption. Retrieved keys are intended to be passed to .NET cryptographic primitives or other consuming code.

## Contents

- [Core capabilities](#core-capabilities)
- [Architecture](#architecture)
- [Repository navigation](#repository-navigation)
- [Main execution flows](#main-execution-flows)
- [Public API](#public-api)
- [Lifecycle and state](#lifecycle-and-state)
- [Asynchrony and concurrent access](#asynchrony-and-concurrent-access)
- [Limitations and important behavior](#limitations-and-important-behavior)
- [Testing](#testing)
- [Modification and extension points](#modification-and-extension-points)
- [Dependencies](#dependencies)

## Core capabilities

- Generation of 128-, 192-, or 256-bit AES keys through [`AesKeyGenerator`](CryptoKit/Aes/AesKeyGenerator.cs).
- Generation of HMAC keys from 128 to 65,536 bits through [`HmacKeyGenerator`](CryptoKit/Hmac/HmacKeyGenerator.cs).
- Generation of RSA key pairs of at least 2048 bits through [`RsaKeyGenerator`](CryptoKit/Rsa/RsaKeyGenerator.cs); the default is 3072 bits.
- Asynchronous loading of existing keys and atomic `get-or-create` behavior through [`IAesKeyProvider`](CryptoKit/Aes/Provider/IAesKeyProvider.cs), [`IHmacKeyProvider`](CryptoKit/Hmac/Provider/IHmacKeyProvider.cs), and [`IRsaKeyProvider`](CryptoKit/Rsa/Provider/IRsaKeyProvider.cs).
- Algorithm-agnostic storage of binary key material through [`IKeyStorage`](CryptoKit/Storage/IKeyStorage.cs).
- File-based storage with create-only publication, atomic replacement, entry-size limits, and owner-only Unix permissions through [`FileKeyStorage`](CryptoKit/Storage/FileKeyStorage.cs).
- Explicit ownership and clearing of secret buffers through [`SecretKeyMaterial`](CryptoKit/Secrets/SecretKeyMaterial.cs).
- Registration of built-in components through `Microsoft.Extensions.DependencyInjection`.

## Architecture

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

| Subsystem | Responsibility | Key types |
|---|---|---|
| AES | Generation, validation, loading, and creation of AES keys | [`AesKey`](CryptoKit/Aes/AesKey.cs), [`AesKeyGenerator`](CryptoKit/Aes/AesKeyGenerator.cs), [`IAesKeyProvider`](CryptoKit/Aes/Provider/IAesKeyProvider.cs) |
| HMAC | Generation, validation, loading, and creation of HMAC keys | [`HmacKey`](CryptoKit/Hmac/HmacKey.cs), [`HmacKeyGenerator`](CryptoKit/Hmac/HmacKeyGenerator.cs), [`IHmacKeyProvider`](CryptoKit/Hmac/Provider/IHmacKeyProvider.cs) |
| RSA | RSA key-pair generation, private-key persistence, and public-key derivation | [`RsaKeyPair`](CryptoKit/Rsa/RsaKeyPair.cs), [`RsaKeyGenerator`](CryptoKit/Rsa/RsaKeyGenerator.cs), [`IRsaKeyProvider`](CryptoKit/Rsa/Provider/IRsaKeyProvider.cs) |
| Secret material | Ownership, copying, export, and deterministic clearing of secret buffers | [`SecretKeyMaterial`](CryptoKit/Secrets/SecretKeyMaterial.cs) |
| Storage | Shared storage contract and file-system implementation | [`IKeyStorage`](CryptoKit/Storage/IKeyStorage.cs), [`FileKeyStorage`](CryptoKit/Storage/FileKeyStorage.cs) |
| Internal orchestration | Serialization of operations per key ID, bounded retry, and shared AES/HMAC workflow | [`StoredSecretKeyProvider<TKey>`](CryptoKit/Internal/StoredSecretKeyProvider.cs), [`KeyedLock`](CryptoKit/Internal/KeyedLock.cs), [`KeyCreationRetryPolicy`](CryptoKit/Internal/KeyCreationRetryPolicy.cs) |
| DI | Registration of storage, generators, and singleton providers | [`StorageServiceCollectionExtensions`](CryptoKit/Storage/StorageServiceCollectionExtensions.cs), [`AesServiceCollectionExtensions`](CryptoKit/Aes/AesServiceCollectionExtensions.cs), [`HmacServiceCollectionExtensions`](CryptoKit/Hmac/HmacServiceCollectionExtensions.cs), [`RsaServiceCollectionExtensions`](CryptoKit/Rsa/RsaServiceCollectionExtensions.cs) |

AES and HMAC share the internal [`StoredSecretKeyProvider<TKey>`](CryptoKit/Internal/StoredSecretKeyProvider.cs). RSA uses a separate [`RsaKeyProvider`](CryptoKit/Rsa/Provider/RsaKeyProvider.cs) because only the private key is persisted, while the public key is derived from it when needed.

<details>
<summary>Responsibility boundaries in more detail</summary>

### Algorithm-specific types

`AesKey`, `HmacKey`, and `RsaKeyPair` represent and validate already existing key material. Generators are responsible only for creating new material.

Providers combine generation with `IKeyStorage` and add the following semantics:

- `Get...Async` — only loading an existing key is allowed;
- `GetOrCreate...Async` — if no entry exists, generation and atomic publication of a new key are allowed.

This separation makes it explicit whether a caller is restricted to using a pre-existing key or is allowed to create a new secret.

### Storage

`IKeyStorage` is unaware of AES, HMAC, or RSA. It works with a `string keyId` and binary data. Algorithm-specific providers derive their own storage IDs:

- AES: `<keyId>.aes`;
- HMAC: `<keyId>.hmac`;
- RSA: `<keyId>.private`.

`FileKeyStorage` additionally converts the full storage ID to SHA-256 over its strict UTF-8 representation and uses the resulting hex string as the file name. The original logical identifier therefore does not become the physical file name.

### RSA

`RsaKeyGenerator` creates:

- the private key in PKCS#8 format;
- the public key in SubjectPublicKeyInfo format.

`RsaKeyProvider` stores only the PKCS#8 private key. When a public key is requested, the private material is loaded, imported through `RSA.ImportPkcs8PrivateKey`, and the public key is then exported again as SubjectPublicKeyInfo.

</details>

## Repository navigation

```text
CryptoKit.slnx
├── CryptoKit/
│   ├── Aes/          # AES key material, generator, provider, options, DI
│   ├── Hmac/         # HMAC key material, generator, provider, options, DI
│   ├── Rsa/          # RSA pair, generator, provider, options, DI
│   ├── Secrets/      # Shared ownership model for secret material
│   ├── Storage/      # IKeyStorage and FileKeyStorage
│   └── Internal/     # Shared orchestration logic and synchronization
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

The main library is located in [`CryptoKit`](CryptoKit/), and tests are in [`CryptoKit.Tests`](CryptoKit.Tests/). Both projects target `net10.0`.

## Main execution flows

### AES/HMAC: load or create a key

```text
IAesKeyProvider.GetOrCreateKeyAsync()
    or IHmacKeyProvider.GetOrCreateKeyAsync()
        ↓
AesKeyProvider / HmacKeyProvider
        ↓
StoredSecretKeyProvider<TKey>
        ↓
KeyedLock.AcquireAsync(keyId)
        ↓
IKeyStorage.TryLoadAsync()
        ├── entry found ──► material validation ──► key object
        └── entry missing
                ↓
             generator
                ↓
        IKeyStorage.CreateAsync()
        ├── true  ──► generated key object
        └── false ──► load the key published by the winning writer
```

### RSA: get or create a public key

```text
IRsaKeyProvider.GetOrCreatePublicKeyAsync()
        ↓
RsaKeyProvider
        ↓
KeyedLock.AcquireAsync(keyId)
        ↓
IKeyStorage.TryLoadAsync(keyId + ".private")
        ├── private key found ──► validate/import ──► derive public key
        └── entry missing
                ↓
        RsaKeyGenerator.Generate()
                ↓
        IKeyStorage.CreateAsync(private key)
                ↓
        public key is returned without publishing a separate public-key entry
```

### File entry publication

```text
FileKeyStorage.CreateAsync() / ReplaceAsync()
        ↓
create a temporary file in the same directory
        ↓
WriteAsync() + FlushAsync()
        ↓
close the temporary file
        ↓
final CancellationToken check
        ↓
File.Move(overwrite: false) or File.Replace()
        ↓
best-effort cleanup of the temporary file
```

<details>
<summary>Concurrent create-or-load workflow</summary>

The local `KeyedLock` prevents duplicated work inside a single provider instance, but it is not an inter-process synchronization mechanism.

Correctness across multiple provider instances or processes relies on the `IKeyStorage.CreateAsync` contract: checking that an entry does not exist and creating it must be a single atomic storage operation.

If `CreateAsync` returns `false`, the provider assumes that another participant won the race and attempts to load the key it published. If that entry was deleted between the failed `CreateAsync` and the following read, the provider retries publication of the same candidate.

The number of such attempts is bounded by [`KeyCreationRetryPolicy`](CryptoKit/Internal/KeyCreationRetryPolicy.cs): at most eight create-only attempts. After the limit is exhausted, `InvalidOperationException` is thrown.

</details>

## Public API

### Registration through DI

The built-in integration path uses `Microsoft.Extensions.DependencyInjection`:

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

[`AddCryptoKitFileStorage`](CryptoKit/Storage/StorageServiceCollectionExtensions.cs) registers a singleton `IKeyStorage`. The algorithm-specific `AddCryptoKit...` methods register the corresponding generator and singleton provider. A single provider can serve any number of logical key IDs.

### Retrieving an AES key

```csharp
var keys = serviceProvider.GetRequiredService<IAesKeyProvider>();

using var key = await keys.GetOrCreateKeyAsync("data-encryption");

var rawKey = key.Export();
try
{
    // Pass rawKey to consuming cryptographic code.
}
finally
{
    System.Security.Cryptography.CryptographicOperations.ZeroMemory(rawKey);
}
```

`GetKeyAsync` does not create a missing key and throws `KeyNotFoundException`. `GetOrCreateKeyAsync` is allowed to create a new key.

### Retrieving an HMAC key

```csharp
var keys = serviceProvider.GetRequiredService<IHmacKeyProvider>();

using var key = await keys.GetOrCreateKeyAsync("request-signing");
```

### Retrieving an RSA public key without exposing the private key to caller code

```csharp
var keys = serviceProvider.GetRequiredService<IRsaKeyProvider>();

byte[] publicKey = await keys.GetOrCreatePublicKeyAsync("token-signing");
```

If the RSA pair does not yet exist, the method creates it and persists only the private key. The returned value contains the public key in SubjectPublicKeyInfo format.

<details>
<summary>Direct generation without storage or DI</summary>

All generators can be used directly:

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

Default sizes:

| Algorithm | Default | Allowed values |
|---|---:|---|
| AES | 256 bits | 128, 192, 256 |
| HMAC | 256 bits | 128–65,536 bits, divisible by 8 |
| RSA | 3072 bits | at least 2048 bits and only sizes supported by the current `RSA` implementation |

The 65,536-bit HMAC limit applies only to **generation**. Existing HMAC key material may be larger as long as it is at least 128 bits and its size is divisible by eight.

</details>

<details>
<summary>Custom IKeyStorage implementation</summary>

To integrate a secure store, keychain, KMS, or another backend, implement [`IKeyStorage`](CryptoKit/Storage/IKeyStorage.cs) and register that implementation in DI instead of `FileKeyStorage`.

Critical contract requirements:

- `TryLoadAsync` returns a new caller-owned `byte[]` or `null`;
- `CreateAsync` must be an atomic create-only operation and must not be implemented as `TryLoadAsync` followed by an ordinary write;
- `CreateAsync` returns `false` if an entry already exists and must not modify it;
- `ReplaceAsync` replaces only an existing entry and throws `KeyNotFoundException` if none exists;
- `DeleteAsync` is idempotent;
- the backend must not retain a reference to the caller-owned buffer passed to create/replace after the operation completes;
- after a commit has actually completed, the operation must not report cancellation as though the side effect had not occurred.

These properties are part of the correctness requirements of the algorithm-specific `GetOrCreate...Async` providers.

</details>

## Lifecycle and state

[`SecretKeyMaterial`](CryptoKit/Secrets/SecretKeyMaterial.cs) owns its internal secret `byte[]`. `AesKey` and `HmacKey` copy the supplied material into that buffer; the original external array does not become the object's internal storage.

`Dispose` clears the owned buffer with `CryptographicOperations.ZeroMemory` and makes further `Export`/`CopyTo` operations unavailable. Repeated `Dispose` calls are safe. A finalizer performs best-effort cleanup if the object was not disposed explicitly, but it does not replace deterministic disposal.

`Export()` and `ExportPrivateKey()` create **new caller-owned arrays**. Clearing those arrays after use is the responsibility of the caller.

[`RsaKeyPair`](CryptoKit/Rsa/RsaKeyPair.cs) clears the private key on `Dispose`. The public key is not treated as secret and remains available through `ExportPublicKey` and `CopyPublicKeyTo` after the private material has been disposed.

Providers registered through the standard DI extension methods have singleton lifetime and retain a snapshot of the configured key size taken during registration. Later changes to the original options object do not affect an already created provider.

<details>
<summary>FileKeyStorage state</summary>

`FileKeyStorage` stores only:

- the absolute path to the storage directory;
- the maximum size of a single entry;
- internal test hooks when an instance is created from the test assembly.

Key contents are not cached in memory between operations.

For every create/replace operation, a temporary file is created in the same directory. After publication, the temporary file is deleted on a best-effort basis. A leftover `.tmp` file is possible if cleanup physically fails; a cleanup failure does not replace the result of the already completed primary operation.

</details>

## Asynchrony and concurrent access

Providers and `IKeyStorage` use `ValueTask` and accept `CancellationToken` for storage operations and lock acquisition.

[`KeyedLock`](CryptoKit/Internal/KeyedLock.cs) serializes operations for the same logical key ID within one provider instance. Different key IDs use different semaphores and do not block each other. Lock entries are reference-counted and removed after the final owner or waiter releases them.

Across separate provider/storage instances and processes, correctness of create-only races must be provided by the storage backend itself through atomic `IKeyStorage.CreateAsync` semantics.

Operations with irreversible side effects use an explicit cancellation boundary: cancellation is checked before commit. After a successful commit, the built-in file storage does not convert the result into `OperationCanceledException` even if the token is cancelled immediately after the file-system change.

## Limitations and important behavior

- `FileKeyStorage` stores the supplied binary data **without additional encryption of file contents**. SHA-256 is used only to transform the logical ID into a file name and does not protect key material at rest.
- On Unix-like systems, the directory is set to `rwx------` and key files to `rw-------`. On Windows, `FileKeyStorage` does not configure ACLs.
- Deleting a file does not guarantee physical erasure of old bytes from storage media, snapshots, or backups.
- Algorithm-specific providers expose loading and `get-or-create`, but do not provide a public provider-level API for key rotation, retirement, or deletion.
- `FileKeyStorageOptions.DirectoryPath` must be configured explicitly; there is no default directory.
- The default maximum size of one `FileKeyStorage` entry is 64 KiB; the limit is enforced for both reading and writing.
- A key ID must not be empty or whitespace and must contain a valid UTF-16 sequence.
- RSA private material must be PKCS#8, and public material must be SubjectPublicKeyInfo. Imported RSA private keys smaller than 2048 bits are rejected.

## Testing

Tests are located in the [`CryptoKit.Tests`](CryptoKit.Tests/CryptoKit.Tests.csproj) project and use xUnit. The suite covers generators, key material, providers, DI, synchronization, and file storage.

Main verified guarantees include:

- valid and invalid AES/HMAC/RSA key sizes;
- copying and clearing of secret material;
- consistency of RSA private/public pairs and continued public-key availability after `Dispose`;
- no implicit regeneration of already stored keys after generation options are changed;
- convergence of multiple providers on a single storage entry;
- bounded retry under persistent create/delete contention;
- atomic create/replace semantics of `FileKeyStorage`;
- cancellation immediately before and after the commit boundary;
- hashed physical file names and entry-size limits;
- owner-only file modes on Unix;
- service registration through DI.

Run the full test suite with:

```bash
dotnet test CryptoKit.slnx
```

Collect coverage through the configured Coverlet collector with:

```bash
dotnet test CryptoKit.slnx --collect:"XPlat Code Coverage"
```

[`FileKeyStorageTests`](CryptoKit.Tests/Storage/FileKeyStorageTests.cs) use real temporary directories on the local file system. No external services or separate configuration are required for the test suite; Unix-specific checks skip the corresponding assertions on Windows.

<details>
<summary>Test organization and test doubles</summary>

- [`Aes`](CryptoKit.Tests/Aes/) — `AesKey`, generator, and provider tests.
- [`Hmac`](CryptoKit.Tests/Hmac/) — `HmacKey`, generator, and provider tests.
- [`Rsa`](CryptoKit.Tests/Rsa/) — pair validation, generator, and provider tests.
- [`Secrets`](CryptoKit.Tests/Secrets/) — shared lifetime/ownership contract of `SecretKeyMaterial`.
- [`Storage`](CryptoKit.Tests/Storage/) — file backend and storage-ID encoding.
- [`Internal`](CryptoKit.Tests/Internal/) — `KeyIdValidator` and `KeyedLock`.
- [`DependencyInjection`](CryptoKit.Tests/DependencyInjection/) — standard service collection extensions.

[`InMemoryKeyStorage`](CryptoKit.Tests/Helpers/InMemoryKeyStorage.cs) is a thread-safe test implementation of `IKeyStorage` used by provider tests without the file system.

[`AlwaysContendedKeyStorage`](CryptoKit.Tests/Helpers/AlwaysContendedKeyStorage.cs) always loses `CreateAsync` and never returns a winning record. It fixes the bounded-retry guarantee and prevents an infinite loop from being introduced under pathological external contention.

[`TemporaryDirectory`](CryptoKit.Tests/Helpers/TemporaryDirectory.cs) creates isolated directories for file-system tests.

When changing storage semantics, tests should primarily be added to `CryptoKit.Tests/Storage`. Changes to an algorithm provider workflow belong in the corresponding `Aes`, `Hmac`, or `Rsa` suite. Changes to shared synchronization or key-ID validation should be accompanied by tests in `CryptoKit.Tests/Internal`.

</details>

## Modification and extension points

| Task | Main modification points |
|---|---|
| Add a new storage backend | [`IKeyStorage`](CryptoKit/Storage/IKeyStorage.cs), then DI registration following [`StorageServiceCollectionExtensions`](CryptoKit/Storage/StorageServiceCollectionExtensions.cs); tests — [`CryptoKit.Tests/Storage`](CryptoKit.Tests/Storage/) |
| Change file atomicity, naming, permissions, or size limits | [`FileKeyStorage`](CryptoKit/Storage/FileKeyStorage.cs), [`FileKeyNameEncoder`](CryptoKit/Storage/Internal/FileKeyNameEncoder.cs), [`FileKeyStorageOptions`](CryptoKit/Storage/Options/FileKeyStorageOptions.cs); tests — [`FileKeyStorageTests`](CryptoKit.Tests/Storage/FileKeyStorageTests.cs) |
| Change the shared AES/HMAC get-or-create workflow | [`StoredSecretKeyProvider<TKey>`](CryptoKit/Internal/StoredSecretKeyProvider.cs), [`KeyCreationRetryPolicy`](CryptoKit/Internal/KeyCreationRetryPolicy.cs), [`KeyedLock`](CryptoKit/Internal/KeyedLock.cs) |
| Change AES policy or generation defaults | [`AesKeyOptions`](CryptoKit/Aes/Options/AesKeyOptions.cs), [`AesKeyGenerator`](CryptoKit/Aes/AesKeyGenerator.cs), [`AesKey`](CryptoKit/Aes/AesKey.cs); tests — [`CryptoKit.Tests/Aes`](CryptoKit.Tests/Aes/) |
| Change HMAC policy or generation defaults | [`HmacKeyOptions`](CryptoKit/Hmac/Options/HmacKeyOptions.cs), [`HmacKeyGenerator`](CryptoKit/Hmac/HmacKeyGenerator.cs), [`HmacKey`](CryptoKit/Hmac/HmacKey.cs); tests — [`CryptoKit.Tests/Hmac`](CryptoKit.Tests/Hmac/) |
| Change RSA persistence, validation, or public-key derivation | [`RsaKeyProvider`](CryptoKit/Rsa/Provider/RsaKeyProvider.cs), [`RsaKeyPair`](CryptoKit/Rsa/RsaKeyPair.cs), [`RsaKeyGenerator`](CryptoKit/Rsa/RsaKeyGenerator.cs), [`RsaKeyOptions`](CryptoKit/Rsa/Options/RsaKeyOptions.cs); tests — [`CryptoKit.Tests/Rsa`](CryptoKit.Tests/Rsa/) |
| Change ownership and clearing rules for secret buffers | [`SecretKeyMaterial`](CryptoKit/Secrets/SecretKeyMaterial.cs); tests — [`SecretKeyMaterialTests`](CryptoKit.Tests/Secrets/SecretKeyMaterialTests.cs) |
| Add a new algorithm-specific provider | Use [`IKeyStorage`](CryptoKit/Storage/IKeyStorage.cs); for symmetric secret material, reuse the approach from [`StoredSecretKeyProvider<TKey>`](CryptoKit/Internal/StoredSecretKeyProvider.cs); DI — follow the existing `AddCryptoKit...` extensions |
| Change default DI lifetimes or options snapshotting | [`KeyProviderServiceCollectionRegistration`](CryptoKit/Internal/KeyProviderServiceCollectionRegistration.cs) and the corresponding service collection extensions; tests — [`ServiceCollectionExtensionsTests`](CryptoKit.Tests/DependencyInjection/ServiceCollectionExtensionsTests.cs) |

When changing `IKeyStorage`, preserve its atomic create-only contract: cross-instance and cross-process correctness of `GetOrCreate...Async` depends on it.

## Dependencies

The main library targets **.NET 10** and has one direct NuGet dependency:

| Package | Version | Purpose | License |
|---|---:|---|---|
| [`Microsoft.Extensions.DependencyInjection`](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/10.0.12) | 10.0.12 | `IServiceCollection`, singleton registrations, and DI integration | MIT |

Cryptographic key generation, import, export, and memory clearing are based on `System.Security.Cryptography` from .NET.

<details>
<summary>Test-project dependencies</summary>

| Package | Version | Purpose | License |
|---|---:|---|---|
| [`Microsoft.NET.Test.Sdk`](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/17.14.1) | 17.14.1 | .NET test platform | MIT |
| [`xunit`](https://www.nuget.org/packages/xunit/2.9.3) | 2.9.3 | Test framework | Apache-2.0 |
| [`xunit.runner.visualstudio`](https://www.nuget.org/packages/xunit.runner.visualstudio/3.1.4) | 3.1.4 | VSTest/Visual Studio adapter | Apache-2.0 |
| [`coverlet.collector`](https://www.nuget.org/packages/coverlet.collector/6.0.4) | 6.0.4 | Code coverage collector | MIT |

These dependencies belong to the non-packable `CryptoKit.Tests` project and are not part of CryptoKit's public runtime API.

</details>
