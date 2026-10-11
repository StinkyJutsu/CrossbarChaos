
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace CrossbarChaos;

internal static class RocketLeagueInstallationDetector
{
    public static List<(string Platform, string Path)> Find()
    {
        var found = new List<(string, string)>();

        FindSteam(found);
        FindEpic(found);

        return found;
    }

    private static void AddInstallation(
        List<(string Platform, string Path)> found,
        string platform,
        string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return;

        try
        {
            string root = Path.GetFullPath(folder);

            bool valid =
                Directory.Exists(Path.Combine(root, "TAGame", "Config")) &&
                (File.Exists(Path.Combine(root, "Binaries", "Win64", "RocketLeague.exe")) ||
                 File.Exists(Path.Combine(root, "Binaries", "Win64", "RocketLeague_EAC.exe")));

            if (!valid)
                return;

            foreach (var entry in found)
            {
                if (entry.Platform == platform &&
                    string.Equals(entry.Path, root, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            found.Add((platform, root));
        }
        catch (Exception ex) when (
            ex is ArgumentException ||
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is NotSupportedException)
        {
            // Ignore invalid or inaccessible installations.
        }
    }

    private static string? RegistryPath(string key, string name)
    {
        try
        {
            return Registry.GetValue(key, name, null) as string;
        }
        catch
        {
            return null;
        }
    }

    private static void FindSteam(List<(string Platform, string Path)> found)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string? registryRoot = RegistryPath(
            @"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath");

        string? machineRoot = RegistryPath(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");

        if (!string.IsNullOrWhiteSpace(registryRoot))
            roots.Add(registryRoot);

        if (!string.IsNullOrWhiteSpace(machineRoot))
            roots.Add(machineRoot);

        roots.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam"));

        foreach (string steamRoot in roots)
        {
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                steamRoot
            };

            try
            {
                string libraryFile = Path.Combine(
                    steamRoot, "steamapps", "libraryfolders.vdf");

                if (File.Exists(libraryFile))
                {
                    string text = File.ReadAllText(libraryFile);

                    // Current Steam library format.
                    foreach (Match match in Regex.Matches(
                        text, @"""path""\s*""(?<value>[^""]+)""",
                        RegexOptions.IgnoreCase))
                    {
                        libraries.Add(match.Groups["value"].Value.Replace(@"\\", @"\"));
                    }

                    // Older Steam library format.
                    foreach (Match match in Regex.Matches(
                        text, @"(?m)^\s*""\d+""\s*""(?<value>[A-Za-z]:[^""]+)"""))
                    {
                        libraries.Add(match.Groups["value"].Value.Replace(@"\\", @"\"));
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            foreach (string library in libraries)
            {
                try
                {
                    string steamApps = Path.Combine(library, "steamapps");
                    string manifest = Path.Combine(steamApps, "appmanifest_252950.acf");

                    if (!File.Exists(manifest))
                        continue;

                    string text = File.ReadAllText(manifest);

                    Match match = Regex.Match(
                        text,
                        @"""installdir""\s*""(?<value>[^""]+)""",
                        RegexOptions.IgnoreCase);

                    if (!match.Success)
                        continue;

                    string installDir = match.Groups["value"].Value;

                    AddInstallation(found, "Steam",
                        Path.Combine(steamApps, "common", installDir));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (ArgumentException) { }
            }
        }
    }

    private static void FindEpic(List<(string Platform, string Path)> found)
    {
        string manifests = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");

        if (!Directory.Exists(manifests))
            return;

        try
        {
            foreach (string file in Directory.EnumerateFiles(manifests, "*.item"))
            {
                try
                {
                    using JsonDocument document =
                        JsonDocument.Parse(File.ReadAllText(file));

                    JsonElement root = document.RootElement;

                    string name = ReadString(root, "DisplayName");
                    string executable = ReadString(root, "LaunchExecutable");
                    string location = ReadString(root, "InstallLocation");

                    bool rocketLeague =
                        name.Contains("Rocket League", StringComparison.OrdinalIgnoreCase) ||
                        executable.Contains("RocketLeague", StringComparison.OrdinalIgnoreCase);

                    if (!rocketLeague)
                        continue;

                    AddInstallation(found, "Epic Games", location);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (JsonException) { }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }
}
