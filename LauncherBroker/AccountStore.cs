using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal sealed class AccountStore
{
    private const int PasswordIterations = 100_000;
    private const int SaltLength = 16;
    private const int HashLength = 32;
    private const int MinUsernameLength = 3;
    private const int MaxUsernameLength = 24;
    private const int MinPasswordLength = 6;
    private const int MaxPasswordLength = 64;
    private const int MaxCharacterNameLength = 16;
    private const int MaxWorldNameLength = 24;
    private static readonly string[] AllowedWorldDifficulties = { "Easy", "Normal", "Hard" };

    private readonly object syncRoot = new();
    private readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

    public AccountStore(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        Directory.CreateDirectory(RootDirectory);
    }

    public string RootDirectory { get; }

    public bool RegisterAccount(string username, string password, out string message, out AccountRecord account)
    {
        account = null!;
        string sanitizedUsername = SanitizeUsername(username);

        if (!TryValidateUsername(sanitizedUsername, out message))
        {
            return false;
        }

        if (!TryValidatePassword(password, out message))
        {
            return false;
        }

        string accountPath = GetAccountPath(sanitizedUsername);

        lock (syncRoot)
        {
            if (File.Exists(accountPath))
            {
                message = "That username already exists.";
                return false;
            }

            byte[] salt = RandomNumberGenerator.GetBytes(SaltLength);
            byte[] hash = HashPassword(password, salt);
            account = new AccountRecord
            {
                Username = sanitizedUsername,
                PasswordSalt = Convert.ToBase64String(salt),
                PasswordHash = Convert.ToBase64String(hash),
                Characters = new List<AccountCharacterRecord>(),
                Worlds = new List<AccountWorldRecord>()
            };

            SaveAccountUnsafe(accountPath, account);
        }

        message = "Account created.";
        return true;
    }

    public bool AccountExists(string username)
    {
        string sanitized = SanitizeUsername(username);
        if (string.IsNullOrWhiteSpace(sanitized)) return false;
        lock (syncRoot) { return File.Exists(GetAccountPath(sanitized)); }
    }

    public bool TryAuthenticate(string username, string password, out string message, out AccountRecord account)
    {
        account = null!;
        string sanitizedUsername = SanitizeUsername(username);

        if (!TryValidateUsername(sanitizedUsername, out message))
        {
            return false;
        }

        lock (syncRoot)
        {
            string accountPath = GetAccountPath(sanitizedUsername);
            AccountRecord? loadedAccount = LoadAccountUnsafe(accountPath);
            if (loadedAccount == null)
            {
                message = "Username or password is incorrect.";
                return false;
            }

            byte[] salt = Convert.FromBase64String(loadedAccount.PasswordSalt);
            byte[] expectedHash = Convert.FromBase64String(loadedAccount.PasswordHash);
            byte[] providedHash = HashPassword(password, salt);
            if (!CryptographicOperations.FixedTimeEquals(expectedHash, providedHash))
            {
                message = "Username or password is incorrect.";
                account = null!;
                return false;
            }

            account = loadedAccount;
        }

        message = "Authenticated.";
        return true;
    }

    public List<AccountCharacterRecord> GetCharacters(string username)
    {
        lock (syncRoot)
        {
            AccountRecord? account = LoadAccountUnsafe(GetAccountPath(username));
            if (account == null)
            {
                return new List<AccountCharacterRecord>();
            }

            return account.Characters.Select(CloneCharacter).ToList();
        }
    }

    public bool TryCreateCharacter(string username, string displayName, int colorIndex, out string message, out AccountCharacterRecord character)
    {
        character = null!;
        string sanitizedName = SanitizeCharacterName(displayName);
        if (string.IsNullOrEmpty(sanitizedName))
        {
            message = "Character name is required.";
            return false;
        }

        lock (syncRoot)
        {
            string accountPath = GetAccountPath(username);
            AccountRecord? account = LoadAccountUnsafe(accountPath);
            if (account == null)
            {
                message = "Account not found.";
                return false;
            }

            if (account.Characters.Any(existing => string.Equals(existing.Name, sanitizedName, StringComparison.OrdinalIgnoreCase)))
            {
                message = "A character with that name already exists on this account.";
                return false;
            }

            character = new AccountCharacterRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = sanitizedName,
                ColorIndex = Math.Clamp(colorIndex, 0, 5),
                Level = 1
            };

            account.Characters.Add(character);
            SaveAccountUnsafe(accountPath, account);
            character = CloneCharacter(character);
        }

        message = "Character created.";
        return true;
    }

    public bool TryGetCharacter(string username, string characterId, out string message, out AccountCharacterRecord character)
    {
        character = null!;
        if (string.IsNullOrWhiteSpace(characterId))
        {
            message = "Character id is required.";
            return false;
        }

        lock (syncRoot)
        {
            AccountRecord? account = LoadAccountUnsafe(GetAccountPath(username));
            if (account == null)
            {
                message = "Account not found.";
                return false;
            }

            AccountCharacterRecord? existing = account.Characters.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, characterId, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                message = "Character not found on this account.";
                return false;
            }

            character = CloneCharacter(existing);
        }

        message = "Character selected.";
        return true;
    }

    public List<AccountWorldRecord> GetWorlds(string username)
    {
        lock (syncRoot)
        {
            AccountRecord? account = LoadAccountUnsafe(GetAccountPath(username));
            if (account == null)
            {
                return new List<AccountWorldRecord>();
            }

            return account.Worlds.Select(CloneWorld).ToList();
        }
    }

    public bool TryCreateWorld(string username, string worldName, string difficulty, out string message, out AccountWorldRecord world)
    {
        world = null!;
        string sanitizedWorldName = SanitizeWorldName(worldName);
        if (string.IsNullOrEmpty(sanitizedWorldName))
        {
            message = "World name is required.";
            return false;
        }

        string sanitizedDifficulty = SanitizeWorldDifficulty(difficulty);

        lock (syncRoot)
        {
            string accountPath = GetAccountPath(username);
            AccountRecord? account = LoadAccountUnsafe(accountPath);
            if (account == null)
            {
                message = "Account not found.";
                return false;
            }

            if (account.Worlds.Any(existing => string.Equals(existing.Name, sanitizedWorldName, StringComparison.OrdinalIgnoreCase)))
            {
                message = "A world with that name already exists on this account.";
                return false;
            }

            world = new AccountWorldRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = sanitizedWorldName,
                Difficulty = sanitizedDifficulty
            };

            account.Worlds.Add(world);
            SaveAccountUnsafe(accountPath, account);
            world = CloneWorld(world);
        }

        message = "World created.";
        return true;
    }

    public bool TryGetWorld(string username, string worldId, out string message, out AccountWorldRecord world)
    {
        world = null!;
        if (string.IsNullOrWhiteSpace(worldId))
        {
            message = "World id is required.";
            return false;
        }

        lock (syncRoot)
        {
            AccountRecord? account = LoadAccountUnsafe(GetAccountPath(username));
            if (account == null)
            {
                message = "Account not found.";
                return false;
            }

            AccountWorldRecord? existing = account.Worlds.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, worldId, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                message = "World not found on this account.";
                return false;
            }

            world = CloneWorld(existing);
        }

        message = "World selected.";
        return true;
    }

    public bool TryDeleteWorld(string username, string worldId, out string message)
    {
        if (string.IsNullOrWhiteSpace(worldId))
        {
            message = "World id is required.";
            return false;
        }

        lock (syncRoot)
        {
            string accountPath = GetAccountPath(username);
            AccountRecord? account = LoadAccountUnsafe(accountPath);
            if (account == null)
            {
                message = "Account not found.";
                return false;
            }

            int removed = account.Worlds.RemoveAll(w =>
                string.Equals(w.Id, worldId, StringComparison.OrdinalIgnoreCase));

            if (removed == 0)
            {
                message = "World not found on this account.";
                return false;
            }

            SaveAccountUnsafe(accountPath, account);
        }

        message = "World deleted.";
        return true;
    }

    private string GetAccountPath(string username)
    {
        return Path.Combine(RootDirectory, NormalizeUsername(username) + ".json");
    }

    private AccountRecord? LoadAccountUnsafe(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        string json = File.ReadAllText(path, Encoding.UTF8);
        return JsonSerializer.Deserialize<AccountRecord>(json);
    }

    private void SaveAccountUnsafe(string path, AccountRecord account)
    {
        string json = JsonSerializer.Serialize(account, jsonOptions);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    private static byte[] HashPassword(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password ?? string.Empty),
            salt,
            PasswordIterations,
            HashAlgorithmName.SHA256,
            HashLength);
    }

    private static bool TryValidateUsername(string username, out string message)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            message = "Username is required.";
            return false;
        }

        if (username.Length < MinUsernameLength || username.Length > MaxUsernameLength)
        {
            message = $"Username must be between {MinUsernameLength} and {MaxUsernameLength} characters.";
            return false;
        }

        for (int i = 0; i < username.Length; i++)
        {
            char current = username[i];
            if (char.IsLetterOrDigit(current) || current == '_' || current == '-' || current == '.')
            {
                continue;
            }

            message = "Username can only use letters, numbers, '.', '-', and '_'.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool TryValidatePassword(string password, out string message)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            message = "Password is required.";
            return false;
        }

        if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
        {
            message = $"Password must be between {MinPasswordLength} and {MaxPasswordLength} characters.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static string SanitizeUsername(string rawUsername)
    {
        return (rawUsername ?? string.Empty).Trim();
    }

    private static string NormalizeUsername(string username)
    {
        return SanitizeUsername(username).ToLowerInvariant();
    }

    private static string SanitizeCharacterName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return string.Empty;
        }

        string trimmed = rawName.Trim();
        if (trimmed.Length > MaxCharacterNameLength)
        {
            trimmed = trimmed[..MaxCharacterNameLength];
        }

        return trimmed;
    }

    private static AccountCharacterRecord CloneCharacter(AccountCharacterRecord source)
    {
        return new AccountCharacterRecord
        {
            Id = source.Id,
            Name = source.Name,
            ColorIndex = source.ColorIndex,
            Level = source.Level
        };
    }

    private static string SanitizeWorldName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return string.Empty;
        }

        string trimmed = rawName.Trim();
        if (trimmed.Length > MaxWorldNameLength)
        {
            trimmed = trimmed[..MaxWorldNameLength];
        }

        return trimmed;
    }

    private static string SanitizeWorldDifficulty(string difficulty)
    {
        for (int i = 0; i < AllowedWorldDifficulties.Length; i++)
        {
            if (string.Equals(AllowedWorldDifficulties[i], difficulty, StringComparison.OrdinalIgnoreCase))
            {
                return AllowedWorldDifficulties[i];
            }
        }

        return "Normal";
    }

    private static AccountWorldRecord CloneWorld(AccountWorldRecord source)
    {
        return new AccountWorldRecord
        {
            Id = source.Id,
            Name = source.Name,
            Difficulty = source.Difficulty
        };
    }
}

internal sealed class AccountRecord
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public List<AccountCharacterRecord> Characters { get; set; } = new();
    public List<AccountWorldRecord> Worlds { get; set; } = new();
}

internal sealed class AccountCharacterRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "Player";
    public int ColorIndex { get; set; }
    public int Level { get; set; } = 1;
}

internal sealed class BrokerCharacterListPayload
{
    public List<AccountCharacterRecord> Characters { get; set; } = new();
}

internal sealed class AccountWorldRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "New World";
    public string Difficulty { get; set; } = "Normal";
}

internal sealed class BrokerWorldListPayload
{
    public List<AccountWorldRecord> Worlds { get; set; } = new();
}
