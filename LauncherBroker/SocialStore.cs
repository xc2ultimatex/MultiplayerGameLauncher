using System.Text;
using System.Text.Json;

/// <summary>
/// Persists friend relationships and message history to disk.
/// Data lives under two subdirectories:
///   {root}/friends/{username}.json  — friend list + pending requests
///   {root}/messages/{a}_{b}.json   — conversation log (up to 500 messages, rolling)
/// </summary>
internal sealed class SocialStore
{
    private const int MaxMessagesPerConversation = 500;

    private readonly object               _lock     = new();
    private readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

    public SocialStore(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        Directory.CreateDirectory(Path.Combine(rootDirectory, "friends"));
        Directory.CreateDirectory(Path.Combine(rootDirectory, "messages"));
    }

    public string RootDirectory { get; }

    // ── Read helpers ───────────────────────────────────────────────────────────

    public FriendData GetFriendData(string username)
    {
        lock (_lock) { return LoadFriendDataUnsafe(username) ?? new FriendData(); }
    }

    public List<string> GetFriends(string username)
    {
        lock (_lock) { return LoadFriendDataUnsafe(username)?.Friends ?? new List<string>(); }
    }

    public bool AreFriends(string a, string b)
    {
        lock (_lock)
        {
            var data = LoadFriendDataUnsafe(a);
            return data?.Friends.Any(f => string.Equals(f, b, StringComparison.OrdinalIgnoreCase)) == true;
        }
    }

    // ── Friend requests ────────────────────────────────────────────────────────

    /// <summary>Sends a friend request from <paramref name="from"/> to <paramref name="to"/>.
    /// Returns null on success, or an error message if the request cannot be sent.</summary>
    public string? SendFriendRequest(string from, string to)
    {
        lock (_lock)
        {
            var fromData = LoadFriendDataUnsafe(from) ?? new FriendData();
            var toData   = LoadFriendDataUnsafe(to)   ?? new FriendData();

            if (fromData.Friends.Any(f => string.Equals(f, to, StringComparison.OrdinalIgnoreCase)))
                return "You are already friends.";

            if (fromData.PendingOut.Any(f => string.Equals(f, to, StringComparison.OrdinalIgnoreCase)))
                return "Friend request already sent.";

            // If they already sent us a request, auto-accept
            if (fromData.PendingIn.Any(f => string.Equals(f, to, StringComparison.OrdinalIgnoreCase)))
            {
                AcceptFriendRequestUnsafe(from, to, fromData, toData);
                return null;
            }

            fromData.PendingOut.Add(to);
            toData.PendingIn.Add(from);
            SaveFriendDataUnsafe(from, fromData);
            SaveFriendDataUnsafe(to, toData);
        }
        return null;
    }

    /// <summary>Accepts a pending request from <paramref name="requester"/> for <paramref name="me"/>.
    /// Returns null on success, or an error message.</summary>
    public string? AcceptFriendRequest(string me, string requester)
    {
        lock (_lock)
        {
            var myData  = LoadFriendDataUnsafe(me)        ?? new FriendData();
            var reqData = LoadFriendDataUnsafe(requester) ?? new FriendData();

            if (!myData.PendingIn.Any(f => string.Equals(f, requester, StringComparison.OrdinalIgnoreCase)))
                return "No pending request from that user.";

            AcceptFriendRequestUnsafe(me, requester, myData, reqData);
        }
        return null;
    }

    public void DeclineFriendRequest(string me, string requester)
    {
        lock (_lock)
        {
            var myData  = LoadFriendDataUnsafe(me)        ?? new FriendData();
            var reqData = LoadFriendDataUnsafe(requester) ?? new FriendData();
            myData.PendingIn.RemoveAll(f   => string.Equals(f, requester, StringComparison.OrdinalIgnoreCase));
            reqData.PendingOut.RemoveAll(f => string.Equals(f, me,        StringComparison.OrdinalIgnoreCase));
            SaveFriendDataUnsafe(me,        myData);
            SaveFriendDataUnsafe(requester, reqData);
        }
    }

    public void RemoveFriend(string me, string other)
    {
        lock (_lock)
        {
            var myData    = LoadFriendDataUnsafe(me)    ?? new FriendData();
            var otherData = LoadFriendDataUnsafe(other) ?? new FriendData();
            myData.Friends.RemoveAll(f    => string.Equals(f, other, StringComparison.OrdinalIgnoreCase));
            otherData.Friends.RemoveAll(f => string.Equals(f, me,    StringComparison.OrdinalIgnoreCase));
            SaveFriendDataUnsafe(me,    myData);
            SaveFriendDataUnsafe(other, otherData);
        }
    }

    // ── Messages ───────────────────────────────────────────────────────────────

    public MessageRecord StoreMessage(string from, string to, string text)
    {
        var record = new MessageRecord { From = from, Text = text, At = DateTime.UtcNow };
        lock (_lock)
        {
            string key  = ConvKey(from, to);
            var    conv = LoadConversationUnsafe(key);
            conv.Add(record);
            if (conv.Count > MaxMessagesPerConversation)
                conv.RemoveRange(0, conv.Count - MaxMessagesPerConversation);
            SaveConversationUnsafe(key, conv);
        }
        return record;
    }

    public List<MessageRecord> GetMessages(string a, string b)
    {
        lock (_lock) { return LoadConversationUnsafe(ConvKey(a, b)); }
    }

    // ── Internal helpers ───────────────────────────────────────────────────────

    private void AcceptFriendRequestUnsafe(string me, string requester, FriendData myData, FriendData reqData)
    {
        myData.PendingIn.RemoveAll(f   => string.Equals(f, requester, StringComparison.OrdinalIgnoreCase));
        reqData.PendingOut.RemoveAll(f => string.Equals(f, me,        StringComparison.OrdinalIgnoreCase));
        if (!myData.Friends.Any(f    => string.Equals(f, requester, StringComparison.OrdinalIgnoreCase)))
            myData.Friends.Add(requester);
        if (!reqData.Friends.Any(f   => string.Equals(f, me,        StringComparison.OrdinalIgnoreCase)))
            reqData.Friends.Add(me);
        SaveFriendDataUnsafe(me,        myData);
        SaveFriendDataUnsafe(requester, reqData);
    }

    private string FriendPath(string username) =>
        Path.Combine(RootDirectory, "friends", Normalize(username) + ".json");

    private string ConvPath(string key) =>
        Path.Combine(RootDirectory, "messages", key + ".json");

    /// Produces a deterministic key for a conversation between two users,
    /// regardless of argument order.
    private static string ConvKey(string a, string b)
    {
        string na = Normalize(a), nb = Normalize(b);
        return string.Compare(na, nb, StringComparison.Ordinal) <= 0
            ? $"{na}_{nb}"
            : $"{nb}_{na}";
    }

    private FriendData? LoadFriendDataUnsafe(string username)
    {
        string path = FriendPath(username);
        if (!File.Exists(path)) return null;
        try   { return JsonSerializer.Deserialize<FriendData>(File.ReadAllText(path, Encoding.UTF8)); }
        catch { return null; }
    }

    private void SaveFriendDataUnsafe(string username, FriendData data) =>
        File.WriteAllText(FriendPath(username),
            JsonSerializer.Serialize(data, _jsonOpts), Encoding.UTF8);

    private List<MessageRecord> LoadConversationUnsafe(string key)
    {
        string path = ConvPath(key);
        if (!File.Exists(path)) return new List<MessageRecord>();
        try
        {
            var conv = JsonSerializer.Deserialize<ConversationFile>(File.ReadAllText(path, Encoding.UTF8));
            return conv?.Messages ?? new List<MessageRecord>();
        }
        catch { return new List<MessageRecord>(); }
    }

    private void SaveConversationUnsafe(string key, List<MessageRecord> messages) =>
        File.WriteAllText(ConvPath(key),
            JsonSerializer.Serialize(new ConversationFile { Messages = messages }, _jsonOpts), Encoding.UTF8);

    private static string Normalize(string s) => s.Trim().ToLowerInvariant();
}

// ── Data models ────────────────────────────────────────────────────────────────

internal sealed class FriendData
{
    public List<string> Friends    { get; set; } = new();
    public List<string> PendingIn  { get; set; } = new();   // requests waiting for me to accept
    public List<string> PendingOut { get; set; } = new();   // requests I sent, waiting for them
}

internal sealed class MessageRecord
{
    public string   From { get; set; } = "";
    public string   Text { get; set; } = "";
    public DateTime At   { get; set; } = DateTime.UtcNow;
}

internal sealed class ConversationFile
{
    public List<MessageRecord> Messages { get; set; } = new();
}
