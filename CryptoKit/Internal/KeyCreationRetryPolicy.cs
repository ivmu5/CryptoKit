namespace CryptoKit.Internal;

internal static class KeyCreationRetryPolicy
{
    // Обычно конфликт CreateAsync разрешается одной загрузкой победившей записи.
    // Ограничение защищает от бесконечного цикла, если внешний участник
    // постоянно создаёт и удаляет одну и ту же запись между CreateAsync и чтением.
    internal const int MaximumAttempts = 8;

    internal static InvalidOperationException CreateExhaustedException(
        string keyDescription,
        string keyId)
    {
        return new InvalidOperationException(
            $"Не удалось завершить создание ключа с идентификатором '{keyId}' " +
            $"(тип: {keyDescription}) после {MaximumAttempts} атомарных попыток. " +
            "Запись в хранилище непрерывно изменяется конкурентными операциями.");
    }
}
