using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

var options = LauncherBrokerOptions.Parse(args);
using var shutdown = new CancellationTokenSource();
var accountStore = new AccountStore(Path.Combine(@"C:\MultiplayerData", "Accounts"));
var socialStore  = new SocialStore(Path.Combine(@"C:\MultiplayerData", "Social"));
var broker       = new LauncherBrokerServer(options.Port, accountStore, socialStore);

Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.Cancel(); };

Console.WriteLine($"Launcher broker starting on port {options.Port}.");
Console.WriteLine($"Account data: {accountStore.RootDirectory}");
Console.WriteLine($"Social data:  {socialStore.RootDirectory}");
Console.WriteLine("Supported: REGISTER LOGIN STATUS_SET FRIEND_LIST FRIEND_ADD FRIEND_ACCEPT FRIEND_DECLINE FRIEND_REMOVE MSG_SEND MSG_HISTORY DISCONNECT.");
Console.WriteLine("Press Ctrl+C to stop.");

try   { await broker.RunAsync(shutdown.Token); }
catch (OperationCanceledException) { }

Console.WriteLine("Launcher broker stopped.");

// ── Options ────────────────────────────────────────────────────────────────────
internal sealed class LauncherBrokerOptions
{
    public int Port { get; private init; } = 7778;

    public static LauncherBrokerOptions Parse(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if ((args[i] == "--port" || args[i] == "-p") && int.TryParse(args[i + 1], out int p))
                return new LauncherBrokerOptions { Port = p };
        return new LauncherBrokerOptions();
    }
}

// ── Server ─────────────────────────────────────────────────────────────────────
internal sealed class LauncherBrokerServer
{
    private readonly TcpListener   _listener;
    private readonly AccountStore  _accounts;
    private readonly SocialStore   _social;
    private readonly object        _lock     = new();
    private readonly Dictionary<int,    ClientSession> _sessions = new();
    private readonly Dictionary<string, ClientSession> _online   = new(StringComparer.OrdinalIgnoreCase);
    private int _nextId = 1;

    public LauncherBrokerServer(int port, AccountStore accounts, SocialStore social)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _accounts = accounts;
        _social   = social;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _listener.Start();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient tcp = await _listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleClientAsync(tcp, ct), CancellationToken.None);
            }
        }
        finally
        {
            _listener.Stop();
            lock (_lock)
            {
                foreach (var s in _sessions.Values) s.Dispose();
                _sessions.Clear();
                _online.Clear();
            }
        }
    }

    // ── Client lifecycle ───────────────────────────────────────────────────────
    private async Task HandleClientAsync(TcpClient tcp, CancellationToken ct)
    {
        ClientSession? session = null;
        try
        {
            tcp.NoDelay = true;
            var stream = tcp.GetStream();
            session    = new ClientSession(NextId(), tcp, stream);
            lock (_lock) { _sessions[session.Id] = session; }
            Log($"Client {session.Id} connected from {tcp.Client.RemoteEndPoint}.");
            await session.SendAsync($"ASSIGN|{session.Id}", ct);

            while (!ct.IsCancellationRequested)
            {
                string? line = await session.Reader.ReadLineAsync(ct);
                if (line == null) break;
                await ProcessMessageAsync(session, line, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException)                { }
        catch (SocketException)            { }
        catch (Exception ex)               { Log($"Client {session?.Id} error: {ex.Message}"); }
        finally
        {
            if (session != null) await DisconnectAsync(session, ct);
            tcp.Dispose();
        }
    }

    private async Task DisconnectAsync(ClientSession session, CancellationToken ct)
    {
        string? username;
        lock (_lock)
        {
            if (!_sessions.ContainsKey(session.Id)) return; // already removed
            _sessions.Remove(session.Id);
            username = session.Username;
            if (username != null) _online.Remove(username);
        }
        session.Dispose();
        Log($"Client {session.Id} ({username ?? "unauthenticated"}) disconnected.");

        if (username != null)
            await PushPresenceToFriendsAsync(username, "Offline", ct);
    }

    // ── Message dispatch ───────────────────────────────────────────────────────
    private async Task ProcessMessageAsync(ClientSession session, string line, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        string[] p = line.Split('|');

        switch (p[0])
        {
            case "REGISTER":       await HandleRegisterAsync(session, p, ct);       break;
            case "LOGIN":          await HandleLoginAsync(session, p, ct);           break;
            case "STATUS_SET":     await HandleStatusSetAsync(session, p, ct);       break;
            case "FRIEND_LIST":    await HandleFriendListAsync(session, ct);         break;
            case "FRIEND_ADD":     await HandleFriendAddAsync(session, p, ct);       break;
            case "FRIEND_ACCEPT":  await HandleFriendAcceptAsync(session, p, ct);    break;
            case "FRIEND_DECLINE": await HandleFriendDeclineAsync(session, p, ct);   break;
            case "FRIEND_REMOVE":  await HandleFriendRemoveAsync(session, p, ct);    break;
            case "MSG_SEND":       await HandleMsgSendAsync(session, p, ct);         break;
            case "MSG_HISTORY":    await HandleMsgHistoryAsync(session, p, ct);      break;
            case "DISCONNECT":
                Log($"Client {session.Id} requested disconnect.");
                await DisconnectAsync(session, ct);
                break;
            default:
                Log($"Ignored unknown command '{p[0]}' from client {session.Id}.");
                break;
        }
    }

    // ── Auth ───────────────────────────────────────────────────────────────────
    private async Task HandleRegisterAsync(ClientSession session, string[] p, CancellationToken ct)
    {
        if (p.Length < 3) { await session.SendAsync($"AUTH_FAIL|{Enc("Username and password required.")}", ct); return; }

        string user = Dec(p[1]);
        string pass = Dec(p[2]);

        if (!_accounts.RegisterAccount(user, pass, out string msg, out AccountRecord account))
        {
            Log($"Register failed for client {session.Id}: {msg}");
            await session.SendAsync($"AUTH_FAIL|{Enc(msg)}", ct);
            return;
        }

        Log($"Registered '{account.Username}' (client {session.Id}).");
        await AuthorizeAsync(session, account.Username, ct);
    }

    private async Task HandleLoginAsync(ClientSession session, string[] p, CancellationToken ct)
    {
        if (p.Length < 3) { await session.SendAsync($"AUTH_FAIL|{Enc("Username and password required.")}", ct); return; }

        string user = Dec(p[1]);
        string pass = Dec(p[2]);

        if (!_accounts.TryAuthenticate(user, pass, out string msg, out AccountRecord account))
        {
            Log($"Login failed for client {session.Id}: {msg}");
            await session.SendAsync($"AUTH_FAIL|{Enc(msg)}", ct);
            return;
        }

        Log($"Authenticated '{account.Username}' (client {session.Id}).");
        await AuthorizeAsync(session, account.Username, ct);
    }

    private async Task AuthorizeAsync(ClientSession session, string username, CancellationToken ct)
    {
        // Kick existing session for this username (one active session per account)
        ClientSession? existing;
        lock (_lock)
        {
            _online.TryGetValue(username, out existing);
            session.Username = username;
            session.Status   = "Online";
            _online[username] = session;
        }

        if (existing != null && existing.Id != session.Id)
        {
            await TrySendAsync(existing, $"AUTH_FAIL|{Enc("Logged in from another location.")}", ct);
            await DisconnectAsync(existing, ct);
        }

        await session.SendAsync($"AUTH_OK|{Enc(username)}", ct);

        // Send initial friend list then push presence both ways
        await SendFriendListAsync(session, ct);
        await PushFriendStatusesToClientAsync(session, ct);
        await PushPresenceToFriendsAsync(username, "Online", ct);
    }

    // ── Presence ───────────────────────────────────────────────────────────────
    private async Task HandleStatusSetAsync(ClientSession session, string[] p, CancellationToken ct)
    {
        if (!await RequireAuthAsync(session, ct)) return;

        string status = p.Length >= 2 ? Dec(p[1]) : "Online";
        if (string.IsNullOrWhiteSpace(status)) status = "Online";
        if (status.Length > 64) status = status[..64];

        lock (_lock) { session.Status = status; }
        Log($"'{session.Username}' status: {status}");
        await PushPresenceToFriendsAsync(session.Username!, status, ct);
    }

    private async Task PushPresenceToFriendsAsync(string username, string status, CancellationToken ct)
    {
        var friends = _social.GetFriends(username);
        List<ClientSession> targets;
        lock (_lock)
        {
            targets = friends
                .Where(f => _online.ContainsKey(f))
                .Select(f => _online[f])
                .ToList();
        }

        string msg = $"PRESENCE|{Enc(username)}|{Enc(status)}";
        foreach (var target in targets)
            await TrySendAsync(target, msg, ct);
    }

    private async Task PushFriendStatusesToClientAsync(ClientSession session, CancellationToken ct)
    {
        var friends = _social.GetFriends(session.Username!);
        List<(string username, string status)> online;
        lock (_lock)
        {
            online = friends
                .Where(f => _online.ContainsKey(f))
                .Select(f => (f, _online[f].Status ?? "Online"))
                .ToList();
        }

        foreach (var (u, s) in online)
            await TrySendAsync(session, $"PRESENCE|{Enc(u)}|{Enc(s)}", ct);
    }

    // ── Friends ────────────────────────────────────────────────────────────────
    private async Task HandleFriendListAsync(ClientSession session, CancellationToken ct)
    {
        if (!await RequireAuthAsync(session, ct)) return;
        await SendFriendListAsync(session, ct);
    }

    private async Task SendFriendListAsync(ClientSession session, CancellationToken ct)
    {
        var data    = _social.GetFriendData(session.Username!);
        var payload = new FriendListPayload();

        lock (_lock)
        {
            foreach (string f in data.Friends)
            {
                payload.Friends.Add(new FriendEntry
                {
                    Username = f,
                    Status   = _online.TryGetValue(f, out var s) ? (s.Status ?? "Online") : "Offline"
                });
            }
        }

        payload.PendingIn  = data.PendingIn.ToList();
        payload.PendingOut = data.PendingOut.ToList();

        string json = JsonSerializer.Serialize(payload);
        await session.SendAsync($"FRIEND_LIST|{Enc(json)}", ct);
    }

    private async Task HandleFriendAddAsync(ClientSession session, string[] p, CancellationToken ct)
    {
        if (!await RequireAuthAsync(session, ct)) return;
        if (p.Length < 2) { await session.SendAsync($"FRIEND_ERROR|{Enc("Username required.")}", ct); return; }

        string target = Dec(p[1]);

        if (string.Equals(target, session.Username, StringComparison.OrdinalIgnoreCase))
        { await session.SendAsync($"FRIEND_ERROR|{Enc("You cannot add yourself.")}", ct); return; }

        if (!_accounts.AccountExists(target))
        { await session.SendAsync($"FRIEND_ERROR|{Enc("User not found.")}", ct); return; }

        string? error = _social.SendFriendRequest(session.Username!, target);
        if (error != null) { await session.SendAsync($"FRIEND_ERROR|{Enc(error)}", ct); return; }

        Log($"'{session.Username}' sent friend request to '{target}'.");
        await session.SendAsync($"FRIEND_REQUEST_SENT|{Enc(target)}", ct);

        // Notify target if online
        ClientSession? targetSession;
        lock (_lock) { _online.TryGetValue(target, out targetSession); }
        if (targetSession != null)
            await TrySendAsync(targetSession, $"FRIEND_REQUEST|{Enc(session.Username!)}", ct);
    }

    private async Task HandleFriendAcceptAsync(ClientSession session, string[] p, CancellationToken ct)
    {
        if (!await RequireAuthAsync(session, ct)) return;
        if (p.Length < 2) { await session.SendAsync($"FRIEND_ERROR|{Enc("Username required.")}", ct); return; }

        string requester = Dec(p[1]);
        string? error = _social.AcceptFriendRequest(session.Username!, requester);
        if (error != null) { await session.SendAsync($"FRIEND_ERROR|{Enc(error)}", ct); return; }

        Log($"'{session.Username}' accepted friend request from '{requester}'.");
        await session.SendAsync($"FRIEND_ACCEPTED|{Enc(requester)}", ct);
        await SendFriendListAsync(session, ct);

        // Notify requester if online
        ClientSession? reqSession;
        lock (_lock) { _online.TryGetValue(requester, out reqSession); }
        if (reqSession != null)
        {
            await TrySendAsync(reqSession, $"FRIEND_ACCEPTED|{Enc(session.Username!)}", ct);
            await SendFriendListAsync(reqSession, ct);
        }

        // Exchange presence both ways now that they're friends
        await PushPresenceToFriendsAsync(session.Username!, session.Status ?? "Online", ct);
        await PushPresenceToFriendsAsync(requester, GetOnlineStatus(requester), ct);
    }

    private async Task HandleFriendDeclineAsync(ClientSession session, string[] p, CancellationToken ct)
    {
        if (!await RequireAuthAsync(session, ct)) return;
        if (p.Length < 2) return;

        string requester = Dec(p[1]);
        _social.DeclineFriendRequest(session.Username!, requester);
        Log($"'{session.Username}' declined friend request from '{requester}'.");
        // No response needed — silently discarded
    }

    private async Task HandleFriendRemoveAsync(ClientSession session, string[] p, CancellationToken ct)
    {
        if (!await RequireAuthAsync(session, ct)) return;
        if (p.Length < 2) { await session.SendAsync($"FRIEND_ERROR|{Enc("Username required.")}", ct); return; }

        string target = Dec(p[1]);
        _social.RemoveFriend(session.Username!, target);
        Log($"'{session.Username}' removed friend '{target}'.");
        await session.SendAsync($"FRIEND_REMOVED|{Enc(target)}", ct);
        await SendFriendListAsync(session, ct);

        // Notify target if online
        ClientSession? targetSession;
        lock (_lock) { _online.TryGetValue(target, out targetSession); }
        if (targetSession != null)
        {
            await TrySendAsync(targetSession, $"FRIEND_REMOVED|{Enc(session.Username!)}", ct);
            await SendFriendListAsync(targetSession, ct);
        }
    }

    // ── Messaging ──────────────────────────────────────────────────────────────
    private async Task HandleMsgSendAsync(ClientSession session, string[] p, CancellationToken ct)
    {
        if (!await RequireAuthAsync(session, ct)) return;
        if (p.Length < 3) { await session.SendAsync($"MSG_ERROR|{Enc("Recipient and text required.")}", ct); return; }

        string to   = Dec(p[1]);
        string text = Dec(p[2]);

        if (string.IsNullOrWhiteSpace(text)) { await session.SendAsync($"MSG_ERROR|{Enc("Message cannot be empty.")}", ct); return; }
        if (text.Length > 512) text = text[..512];

        if (!_social.AreFriends(session.Username!, to))
        { await session.SendAsync($"MSG_ERROR|{Enc("You are not friends with that user.")}", ct); return; }

        var record = _social.StoreMessage(session.Username!, to, text);
        string ts  = record.At.ToString("o");
        await session.SendAsync($"MSG_SENT|{Enc(to)}|{Enc(ts)}", ct);

        // Deliver to recipient if online
        ClientSession? toSession;
        lock (_lock) { _online.TryGetValue(to, out toSession); }
        if (toSession != null)
            await TrySendAsync(toSession, $"MSG_RECV|{Enc(session.Username!)}|{Enc(text)}|{Enc(ts)}", ct);
    }

    private async Task HandleMsgHistoryAsync(ClientSession session, string[] p, CancellationToken ct)
    {
        if (!await RequireAuthAsync(session, ct)) return;
        if (p.Length < 2) { await session.SendAsync($"MSG_ERROR|{Enc("Username required.")}", ct); return; }

        string other    = Dec(p[1]);
        var    messages = _social.GetMessages(session.Username!, other);
        string json     = JsonSerializer.Serialize(new MessageHistoryPayload { Messages = messages });
        await session.SendAsync($"MSG_HISTORY|{Enc(other)}|{Enc(json)}", ct);
    }

    // ── Utilities ──────────────────────────────────────────────────────────────
    private async Task<bool> RequireAuthAsync(ClientSession session, CancellationToken ct)
    {
        if (session.Username != null) return true;
        await session.SendAsync($"AUTH_REQUIRED|{Enc("Log in first.")}", ct);
        return false;
    }

    private async Task TrySendAsync(ClientSession session, string message, CancellationToken ct)
    {
        try   { await session.SendAsync(message, ct); }
        catch { await DisconnectAsync(session, ct); }
    }

    private string GetOnlineStatus(string username)
    {
        lock (_lock)
            return _online.TryGetValue(username, out var s) ? (s.Status ?? "Online") : "Offline";
    }

    private int NextId() { lock (_lock) { return _nextId++; } }

    private static void Log(string msg) =>
        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {msg}");

    private static string Enc(string v) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(v ?? ""));

    private static string Dec(string v)
    {
        try   { return Encoding.UTF8.GetString(Convert.FromBase64String(v)); }
        catch { return string.Empty; }
    }
}

// ── Client session ─────────────────────────────────────────────────────────────
internal sealed class ClientSession : IDisposable
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public ClientSession(int id, TcpClient tcp, NetworkStream stream)
    {
        Id     = id;
        Socket = tcp;
        Reader = new StreamReader(stream, Encoding.UTF8, false, leaveOpen: true);
        Writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
    }

    public int          Id       { get; }
    public TcpClient    Socket   { get; }
    public StreamReader Reader   { get; }
    public StreamWriter Writer   { get; }
    public string?      Username { get; set; }
    public string?      Status   { get; set; } = "Online";

    public async Task SendAsync(string message, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct);
        try   { await Writer.WriteLineAsync(message.AsMemory(), ct); }
        finally { _sendLock.Release(); }
    }

    public void Dispose()
    {
        try { Socket.Close(); } catch { }
        Reader.Dispose();
        Writer.Dispose();
        _sendLock.Dispose();
    }
}

// ── JSON payloads ──────────────────────────────────────────────────────────────
internal sealed class FriendListPayload
{
    public List<FriendEntry> Friends    { get; set; } = new();
    public List<string>      PendingIn  { get; set; } = new();  // sent to me, waiting my accept
    public List<string>      PendingOut { get; set; } = new();  // sent by me, waiting their accept
}

internal sealed class FriendEntry
{
    public string Username { get; set; } = "";
    public string Status   { get; set; } = "Offline";
}

internal sealed class MessageHistoryPayload
{
    public List<MessageRecord> Messages { get; set; } = new();
}
