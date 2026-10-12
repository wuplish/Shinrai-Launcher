namespace ShinraiLauncher.Models;

public class InstalledVersionItem
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "release"; // release, snapshot, modified
    public string? InheritsFrom { get; set; }
    public string MainClass { get; set; } = "";
    public int RequiredJavaMajor { get; set; } = 8;
    public string ProfileBadge { get; set; } = "Vanilla";
    public bool HasClientJar { get; set; }

    public string DisplayName => $"{Id}  [{ProfileBadge}] (Java {RequiredJavaMajor}+)";

    public override string ToString() => DisplayName;
}

public class LocalModItem
{
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsEnabled { get; set; }
    public string SizeText { get; set; } = "";
    public string CategoryBadge { get; set; } = "Mod";
}

public class LanServerEntry
{
    public string Edition { get; set; } = "Java"; // "Java" or "Bedrock"
    public string Motd { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public int Port { get; set; }
    public string Players { get; set; } = "?/?";
    public string Version { get; set; } = "";
    public DateTime LastSeen { get; set; } = DateTime.Now;

    public string Endpoint => $"{IpAddress}:{Port}";
}
