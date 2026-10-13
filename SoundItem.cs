
using System;
using System.ComponentModel;
using System.IO;

namespace CrossbarChaos;

public sealed class SoundItem : INotifyPropertyChanged
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FilePath { get; set; } = string.Empty;

    public string FileName => Path.GetFileName(FilePath);

    private int _volume = 100;

    public int Volume
    {
        get => _volume;
        set
        {
            int newVolume = Math.Clamp(value, 0, 200);

            if (_volume == newVolume)
                return;

            _volume = newVolume;
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(Volume)));
        }
    }

    private bool _isEnabled = true;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
                return;

            _isEnabled = value;
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(IsEnabled)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
