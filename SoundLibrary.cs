
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CrossbarChaos;

public sealed class SoundLibrary
{
    private static readonly string LibraryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CrossbarChaos",
        "sounds.json");

    public ObservableCollection<SoundItem> Sounds { get; } = new();

    private static readonly string FoldersPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CrossbarChaos",
        "folders.json");

    private readonly List<string> _trackedFolders = new();

    public IReadOnlyList<string> TrackedFolders => _trackedFolders;

    public SoundLibrary()
    {
        Load();
        LoadTrackedFolders();
    }

    public void RegisterFolders(IEnumerable<string> folders)
    {
        bool changed = false;

        foreach (string folder in folders)
        {
            string fullPath = Path.GetFullPath(folder);

            if (!Directory.Exists(fullPath))
                continue;

            if (_trackedFolders.Contains(
                fullPath, StringComparer.OrdinalIgnoreCase))
                continue;

            _trackedFolders.Add(fullPath);
            changed = true;
        }

        if (changed)
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(FoldersPath)!);

            File.WriteAllText(
                FoldersPath,
                JsonSerializer.Serialize(_trackedFolders));
        }
    }

    private void LoadTrackedFolders()
    {
        if (!File.Exists(FoldersPath))
            return;

        var folders = JsonSerializer.Deserialize<List<string>>(
            File.ReadAllText(FoldersPath));

        if (folders == null)
            return;

        foreach (string folder in folders)
        {
            if (string.IsNullOrWhiteSpace(folder))
                continue;

            string fullPath = Path.GetFullPath(folder);

            if (!_trackedFolders.Contains(
                fullPath, StringComparer.OrdinalIgnoreCase))
            {
                _trackedFolders.Add(fullPath);
            }
        }
    }

    public bool AddSound(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        string fullPath = Path.GetFullPath(filePath);

        if (Sounds.Any(sound =>
            string.Equals(sound.FilePath, fullPath,
                StringComparison.OrdinalIgnoreCase)))
            return false;

        var soundItem = new SoundItem
        {
            FilePath = fullPath,
            Volume = 100,
            IsEnabled = true
        };

        soundItem.PropertyChanged += Sound_PropertyChanged;
        Sounds.Add(soundItem);

        Save();
        return true;
    }

    public int AddSounds(IEnumerable<string> filePaths)
    {
        var existingNames = new HashSet<string>(
            Sounds.Select(sound =>
                Path.GetFileNameWithoutExtension(sound.FilePath)),
            StringComparer.OrdinalIgnoreCase);

        int added = 0;

        try
        {
            foreach (string filePath in filePaths)
            {
                if (!File.Exists(filePath))
                    continue;

                string fullPath = Path.GetFullPath(filePath);
                string soundName = Path.GetFileNameWithoutExtension(fullPath);

                if (string.IsNullOrWhiteSpace(soundName))
                    continue;

                if (!existingNames.Add(soundName))
                    continue;

                var soundItem = new SoundItem
                {
                    FilePath = fullPath,
                    Volume = 100,
                    IsEnabled = true
                };

                soundItem.PropertyChanged += Sound_PropertyChanged;
                Sounds.Add(soundItem);
                added++;
            }
        }
        finally
        {
            if (added > 0)
                Save();
        }

        return added;
    }

    public bool RemoveSound(SoundItem sound)
    {
        if (!Sounds.Remove(sound))
            return false;

        sound.PropertyChanged -= Sound_PropertyChanged;

        Save();
        return true;
    }

    public void Save()
    {
        string? folder = Path.GetDirectoryName(LibraryPath);

        if (folder is not null)
            Directory.CreateDirectory(folder);

        string json = JsonSerializer.Serialize(
            Sounds,
            new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(LibraryPath, json);
    }

    private void Load()
    {
        if (!File.Exists(LibraryPath))
            return;

        string json = File.ReadAllText(LibraryPath);

        var savedSounds =
            JsonSerializer.Deserialize<List<SoundItem>>(json);

        if (savedSounds is null)
            return;

        foreach (SoundItem sound in savedSounds)
        {
            sound.PropertyChanged += Sound_PropertyChanged;
            Sounds.Add(sound);
        }
    }

    private void Sound_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        Save();
    }
}
