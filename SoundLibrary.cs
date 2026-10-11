
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

    public SoundLibrary()
    {
        Load();
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
