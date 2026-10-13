
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CrossbarChaos;

internal sealed class StatsApiConfiguration
{
    private readonly string _backupFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CrossbarChaos", "StatsApiRecovery");

    private sealed class BackupRecord
    {
        public string FilePath { get; set; } = "";
        public bool Existed { get; set; }
        public string Original { get; set; } = "";
        public string Enabled { get; set; } = "";
    }

    public string Enable()
    {
        var results = new List<string>();
        var installations = RocketLeagueInstallationDetector.Find();

        foreach (var game in installations)
        {
            string journal = Path.Combine(
                _backupFolder,
                game.Platform == "Steam" ? "Steam.json" : "Epic.json");

            try
            {
                // Preserve the original backup until uninstall.
                if (File.Exists(journal))
                {
                    BackupRecord saved = JsonSerializer.Deserialize<BackupRecord>(
                        File.ReadAllText(journal))
                        ?? throw new InvalidDataException("Invalid recovery record.");

                    if (string.IsNullOrWhiteSpace(saved.FilePath) ||
                        !File.Exists(saved.FilePath) ||
                        !File.ReadAllBytes(saved.FilePath).SequenceEqual(
                            Convert.FromBase64String(saved.Enabled)))
                    {
                        throw new IOException(
                            "Saved API configuration no longer matches the game file. Setup skipped to protect the original backup.");
                    }

                    results.Add($"{game.Platform}: API already configured.");
                    continue;
                }

                string configFolder = Path.Combine(game.Path, "TAGame", "Config");
                string custom = Path.Combine(configFolder, "TAStatsAPI.ini");
                string defaults = Path.Combine(configFolder, "DefaultStatsAPI.ini");

                string config = File.Exists(custom) ? custom : defaults;
                bool existed = File.Exists(config);

                byte[] original = existed
                    ? File.ReadAllBytes(config)
                    : Array.Empty<byte>();

                byte[] enabled = EnableRate(original, out bool alreadyEnabled);

                if (alreadyEnabled)
                {
                    results.Add($"{game.Platform}: API already enabled (left unchanged).");
                    continue;
                }

                Directory.CreateDirectory(_backupFolder);

                var backup = new BackupRecord
                {
                    FilePath = config,
                    Existed = existed,
                    Original = Convert.ToBase64String(original),
                    Enabled = Convert.ToBase64String(enabled)
                };

                SaveJournal(journal, backup);
                WriteAtomic(config, enabled);

                results.Add($"{game.Platform}: API enabled successfully.");
            }
            catch (Exception ex)
            {
                results.Add($"{game.Platform}: FAILED - {ex.Message}");
            }
        }

        return results.Count == 0
            ? "No Rocket League installations found."
            : string.Join("\n", results);
    }

    public string Restore()
    {
        var errors = new List<string>();

        var folders = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            _backupFolder
        };

        try
        {
            using var profiles =
                Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList");

            if (profiles != null)
            {
                foreach (string sid in profiles.GetSubKeyNames())
                {
                    using var profile = profiles.OpenSubKey(sid);

                    if (profile?.GetValue("ProfileImagePath")
                        is not string root ||
                        string.IsNullOrWhiteSpace(root))
                        continue;

                    string path =
                        Environment.ExpandEnvironmentVariables(root);

                    folders.Add(Path.Combine(
                        path,
                        "AppData",
                        "Local",
                        "CrossbarChaos",
                        "StatsApiRecovery"));
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add(
                $"Windows profile lookup failed: {ex.Message}");
        }

        foreach (string folder in folders)
        {
            foreach (string name in new[] { "Steam.json", "Epic.json" })
            {
                string journal = Path.Combine(folder, name);

                if (!File.Exists(journal))
                    continue;

                try
                {
                    RestoreOne(journal);
                }
                catch (Exception ex)
                {
                    errors.Add($"{journal}: {ex.Message}");
                }
            }
        }

        return string.Join("\n", errors);
    }

    private static void RestoreOne(string journal)
    {
        var backup = JsonSerializer.Deserialize<BackupRecord>(
            File.ReadAllText(journal))
            ?? throw new InvalidDataException("Invalid recovery record.");

        if (string.IsNullOrWhiteSpace(backup.FilePath))
            throw new InvalidDataException("Missing recovery path.");

        string targetPath = Path.GetFullPath(backup.FilePath);

        string configFolder = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidDataException("Invalid configuration path.");

        string gameDataFolder = Path.GetDirectoryName(configFolder)
            ?? throw new InvalidDataException("Invalid configuration path.");

        string gameFolder = Path.GetDirectoryName(gameDataFolder)
            ?? throw new InvalidDataException("Invalid installation path.");

        bool validTarget =
            (string.Equals(
                Path.GetFileName(targetPath),
                "TAStatsAPI.ini",
                StringComparison.OrdinalIgnoreCase) ||
             string.Equals(
                Path.GetFileName(targetPath),
                "DefaultStatsAPI.ini",
                StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(
                Path.GetFileName(configFolder),
                "Config",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                Path.GetFileName(gameDataFolder),
                "TAGame",
                StringComparison.OrdinalIgnoreCase) &&
            (File.Exists(Path.Combine(
                gameFolder, "Binaries", "Win64", "RocketLeague.exe")) ||
             File.Exists(Path.Combine(
                gameFolder, "Binaries", "Win64", "RocketLeague_EAC.exe")));

        if (!validTarget)
            throw new InvalidDataException(
                "Recovery target is not a recognized Rocket League Stats API configuration.");

        byte[] original = Convert.FromBase64String(backup.Original);
        byte[] enabled = Convert.FromBase64String(backup.Enabled);

        byte[] expectedEnabled = EnableRate(original, out bool alreadyEnabled);

        if (alreadyEnabled || !enabled.SequenceEqual(expectedEnabled))
        {
            throw new InvalidDataException(
                "Recovery data does not match the expected Stats API modification.");
        }

        if (!File.Exists(backup.FilePath))
        {
            if (backup.Existed)
                throw new IOException("Original configuration file is missing.");

            File.Delete(journal);
            return;
        }

        byte[] current = File.ReadAllBytes(backup.FilePath);

        if (current.SequenceEqual(enabled))
        {
            if (backup.Existed)
                WriteAtomic(backup.FilePath, original);
            else
                File.Delete(backup.FilePath);
        }
        else if (!current.SequenceEqual(original))
        {
            throw new IOException(
                "Configuration changed externally. Restoration skipped to protect those changes.");
        }

        File.Delete(journal);
    }

    private static byte[] EnableRate(byte[] original, out bool alreadyEnabled)
    {
        alreadyEnabled = false;

        // Do not risk corrupting an unsupported UTF-16 configuration.
        if (original.Length >= 2 &&
            ((original[0] == 0xFF && original[1] == 0xFE) ||
             (original[0] == 0xFE && original[1] == 0xFF)))
        {
            throw new InvalidDataException("Unsupported UTF-16 configuration.");
        }

        // Latin1 provides a byte-for-byte reversible representation.
        string text = Encoding.Latin1.GetString(original);
        string newline = text.Contains("\r\n") ? "\r\n" : "\n";

        Match section = Regex.Match(
            text,
            @"(?mi)^(?:\xEF\xBB\xBF)?[ \t]*\[TAGame\.MatchStatsExporter_TA\][ \t]*\r?$");

        if (!section.Success)
        {
            string prefix = text.Length > 0 && !text.EndsWith('\n')
                ? newline
                : "";

            string addition =
                $"{prefix}[TAGame.MatchStatsExporter_TA]{newline}PacketSendRate=1{newline}";

            return Encoding.Latin1.GetBytes(text + addition);
        }

        int lineEnd = text.IndexOf('\n', section.Index + section.Length);
        int bodyStart = lineEnd < 0 ? text.Length : lineEnd + 1;

        Match nextSection = new Regex(
            @"(?m)^[ \t]*\[[^\r\n]+\]").Match(text, bodyStart);

        int bodyEnd = nextSection.Success
            ? nextSection.Index
            : text.Length;

        string body = text.Substring(bodyStart, bodyEnd - bodyStart);

        MatchCollection settings = Regex.Matches(
            body,
            @"(?mi)^[ \t]*PacketSendRate[ \t]*=[ \t]*(?<value>[^;\r\n]*)");

        if (settings.Count > 1)
            throw new InvalidDataException("Duplicate PacketSendRate settings.");

        if (settings.Count == 0)
        {
            string insertion =
                (lineEnd < 0 ? newline : "") +
                "PacketSendRate=1" + newline;

            return Encoding.Latin1.GetBytes(
                text.Insert(bodyStart, insertion));
        }

        Group value = settings[0].Groups["value"];

        if (!double.TryParse(
            value.Value.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double rate) || rate < 0 || double.IsNaN(rate))
        {
            throw new InvalidDataException("Invalid PacketSendRate value.");
        }

        if (rate > 0)
        {
            alreadyEnabled = true;
            return original;
        }

        int position = bodyStart + value.Index;

        string updated = text.Remove(position, value.Length)
                             .Insert(position, "1");

        return Encoding.Latin1.GetBytes(updated);
    }

    private static void SaveJournal(string path, BackupRecord backup)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(backup));
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static void WriteAtomic(string path, byte[] content)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            File.WriteAllBytes(temporary, content);

            if (File.Exists(path))
                File.Replace(temporary, path, null);
            else
                File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
