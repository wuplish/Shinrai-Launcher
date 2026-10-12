using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using ShinraiLauncher.Models;

namespace ShinraiLauncher.Services;

public sealed class LanNetworkService : IDisposable
{
    private CancellationTokenSource? _listenerCts;
    private CancellationTokenSource? _relayCts;

    public event Action<LanServerEntry>? ServerDiscovered;

    // RakNet Offline Message ID (Magic 16 bytes for Minecraft Bedrock UDP 19132 ping)
    private static readonly byte[] RakNetMagic =
    [
        0x00, 0xFF, 0xFF, 0x00, 0xFE, 0xFE, 0xFE, 0xFE,
        0xFD, 0xFD, 0xFD, 0xFD, 0x12, 0x34, 0x56, 0x78
    ];

    public static List<string> GetLocalLanIpAddresses()
    {
        var ips = new List<string>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var props = ni.GetIPProperties();
                foreach (var ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ua.Address))
                    {
                        ips.Add($"{ua.Address} ({ni.Name})");
                    }
                }
            }
        }
        catch
        {
            // Fallback
        }

        if (ips.Count == 0)
            ips.Add("127.0.0.1 (Yerel)");

        return ips;
    }

    public void StartListening()
    {
        StopListening();
        _listenerCts = new CancellationTokenSource();
        var token = _listenerCts.Token;

        // 1. Java UDP Multicast 224.0.2.60:4445 listener
        _ = Task.Run(() => ListenJavaLanMulticastAsync(token), token);

        // 2. Bedrock UDP 19132 broadcast pinger & listener
        _ = Task.Run(() => ListenBedrockLanBroadcastAsync(token), token);
    }

    public void StopListening()
    {
        try
        {
            _listenerCts?.Cancel();
            _listenerCts?.Dispose();
            _listenerCts = null;
        }
        catch
        {
            // Ignore
        }
    }

    private async Task ListenJavaLanMulticastAsync(CancellationToken ct)
    {
        try
        {
            using var udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 4445));
            udp.JoinMulticastGroup(IPAddress.Parse("224.0.2.60"));

            while (!ct.IsCancellationRequested)
            {
                var result = await udp.ReceiveAsync(ct);
                string msg = Encoding.UTF8.GetString(result.Buffer);

                var motdMatch = Regex.Match(msg, @"\[MOTD\](.*?)\[/MOTD\]");
                var adMatch = Regex.Match(msg, @"\[AD\](.*?)\[/AD\]");

                if (motdMatch.Success && adMatch.Success)
                {
                    string motd = Regex.Replace(motdMatch.Groups[1].Value, @"§[0-9a-fk-or]", "");
                    string ad = adMatch.Groups[1].Value.Trim();

                    string ip = result.RemoteEndPoint.Address.ToString();
                    int port = 25565;

                    if (ad.Contains(':'))
                    {
                        string[] parts = ad.Split(':');
                        ip = parts[0];
                        int.TryParse(parts[1], out port);
                    }
                    else
                    {
                        int.TryParse(ad, out port);
                    }

                    ServerDiscovered?.Invoke(new LanServerEntry
                    {
                        Edition = "Java",
                        Motd = motd,
                        IpAddress = ip,
                        Port = port,
                        Players = "LAN Açık",
                        Version = "Java LAN",
                        LastSeen = DateTime.Now
                    });
                }
            }
        }
        catch
        {
            // Socket closed or cancelled
        }
    }

    private async Task ListenBedrockLanBroadcastAsync(CancellationToken ct)
    {
        try
        {
            using var udp = new UdpClient(0);
            udp.EnableBroadcast = true;

            _ = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var res = await udp.ReceiveAsync(ct);
                        var parsed = TryParseBedrockPong(res.Buffer, res.RemoteEndPoint.Address.ToString(), res.RemoteEndPoint.Port);
                        if (parsed != null)
                            ServerDiscovered?.Invoke(parsed);
                    }
                    catch
                    {
                        break;
                    }
                }
            }, ct);

            while (!ct.IsCancellationRequested)
            {
                byte[] pingPacket = BuildBedrockUnconnectedPingPacket();
                await udp.SendAsync(pingPacket, pingPacket.Length, new IPEndPoint(IPAddress.Broadcast, 19132));
                await udp.SendAsync(pingPacket, pingPacket.Length, new IPEndPoint(IPAddress.Broadcast, 19133));
                await Task.Delay(3500, ct);
            }
        }
        catch
        {
            // Ignore
        }
    }

    public static byte[] BuildBedrockUnconnectedPingPacket()
    {
        byte[] packet = new byte[1 + 8 + 16 + 8];
        packet[0] = 0x01; // ID_UNCONNECTED_PING
        byte[] timeBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Environment.TickCount64));
        Buffer.BlockCopy(timeBytes, 0, packet, 1, 8);
        Buffer.BlockCopy(RakNetMagic, 0, packet, 9, 16);
        byte[] guidBytes = BitConverter.GetBytes(0x5348494E52414901L);
        Buffer.BlockCopy(guidBytes, 0, packet, 25, 8);
        return packet;
    }

    private static LanServerEntry? TryParseBedrockPong(byte[] buffer, string remoteIp, int remotePort)
    {
        // 0x1C = ID_UNCONNECTED_PONG (1 + 8 time + 8 serverGuid + 16 magic + 2 stringLen + string)
        if (buffer.Length < 35 || buffer[0] != 0x1C)
            return null;

        int strLen = (buffer[33] << 8) | buffer[34];
        if (buffer.Length < 35 + strLen)
            return null;

        string payload = Encoding.UTF8.GetString(buffer, 35, strLen);
        // Format: MCPE;MOTD;Protocol;Version;Players;MaxPlayers;ServerUniqueId;SubMotd;Gamemode;...
        string[] fields = payload.Split(';');
        if (fields.Length < 6)
            return null;

        string motd = Regex.Replace(fields[1], @"§[0-9a-fk-or]", "");
        string version = fields[3];
        string players = $"{fields[4]}/{fields[5]}";
        int port = remotePort;
        if (fields.Length >= 11 && int.TryParse(fields[10], out int ipv4Port))
            port = ipv4Port;

        return new LanServerEntry
        {
            Edition = "Bedrock",
            Motd = string.IsNullOrWhiteSpace(motd) ? "Bedrock LAN Dünyası" : motd,
            IpAddress = remoteIp,
            Port = port,
            Players = players,
            Version = $"Bedrock {version}",
            LastSeen = DateTime.Now
        };
    }

    /// <summary>
    /// Okul ağında Multicast (224.0.2.60) engelli olduğunda arkadaşının IP:Port adresini
    /// bu bilgisayarda yerel LAN dünyası olarak yayınlar, böylece oyun içinde 'Çok Oyunculu' ekranında otomatik çıkar!
    /// </summary>
    public void StartLocalLanRelay(string targetIpAndPort, string customTitle, Action<string> log)
    {
        StopLocalLanRelay();
        _relayCts = new CancellationTokenSource();
        var ct = _relayCts.Token;

        string title = string.IsNullOrWhiteSpace(customTitle) ? "Shinrai Okul LAN Sunucusu" : customTitle;
        string cleanTarget = targetIpAndPort.Trim();

        log($"[LAN Köprüsü] '{cleanTarget}' adresi yerel Çok Oyunculu listesine yansıtılıyor...");

        _ = Task.Run(async () =>
        {
            try
            {
                using var udp = new UdpClient();
                var multicastEp = new IPEndPoint(IPAddress.Parse("224.0.2.60"), 4445);
                string announcement = $"[MOTD]§c[Shinrai LAN] §f{title}[/MOTD][AD]{cleanTarget}[/AD]";
                byte[] bytes = Encoding.UTF8.GetBytes(announcement);

                while (!ct.IsCancellationRequested)
                {
                    await udp.SendAsync(bytes, bytes.Length, multicastEp);
                    await Task.Delay(1500, ct);
                }
            }
            catch
            {
                // Stopped
            }
        }, ct);
    }

    public void StopLocalLanRelay()
    {
        try
        {
            _relayCts?.Cancel();
            _relayCts?.Dispose();
            _relayCts = null;
        }
        catch
        {
            // Ignore
        }
    }

    /// <summary>
    /// Belirli bir IP:Port üzerindeki Java veya Bedrock sunucusunu doğrudan yoklar (Multicast kapalı okul ağları için).
    /// </summary>
    public async Task<LanServerEntry?> ProbeDirectEndpointAsync(string endpointText)
    {
        string ip = endpointText.Trim();
        int port = 25565;

        if (ip.Contains(':'))
        {
            string[] parts = ip.Split(':');
            ip = parts[0].Trim();
            if (!int.TryParse(parts[1].Trim(), out port))
                port = 25565;
        }

        // 1. Try TCP connect for Java server
        try
        {
            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync(ip, port);
            if (await Task.WhenAny(connectTask, Task.Delay(1200)) == connectTask && tcp.Connected)
            {
                return new LanServerEntry
                {
                    Edition = "Java",
                    Motd = $"Doğrudan Okul Sunucusu ({ip}:{port})",
                    IpAddress = ip,
                    Port = port,
                    Players = "Aktif (TCP Açık)",
                    Version = "Java LAN/Server",
                    LastSeen = DateTime.Now
                };
            }
        }
        catch
        {
            // Not TCP or offline
        }

        // 2. Try UDP Bedrock RakNet Ping
        try
        {
            using var udp = new UdpClient();
            byte[] ping = BuildBedrockUnconnectedPingPacket();
            await udp.SendAsync(ping, ping.Length, ip, port == 25565 ? 19132 : port);

            using var cts = new CancellationTokenSource(1200);
            var res = await udp.ReceiveAsync(cts.Token);
            return TryParseBedrockPong(res.Buffer, ip, res.RemoteEndPoint.Port);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        StopListening();
        StopLocalLanRelay();
    }
}
