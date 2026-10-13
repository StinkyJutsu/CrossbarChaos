
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace CrossbarChaos;

public partial class MainWindow
{
    private readonly List<ActiveSound> _activeSounds = new();

    private sealed class ActiveSound : IDisposable
    {
        public WasapiOut Output { get; }
        public AudioFileReader Reader { get; }
        public MMDevice Device { get; }

        public ActiveSound(
            WasapiOut output,
            AudioFileReader reader,
            MMDevice device)
        {
            Output = output;
            Reader = reader;
            Device = device;
        }

        public void Dispose()
        {
            Output.Dispose();
            Reader.Dispose();
            Device.Dispose();
        }
    }

    private void TestSoundButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (SoundList.SelectedItem is not SoundItem sound)
        {
            MessageBox.Show(
                this,
                "Select a sound from the library first.",
                "Crossbar Chaos",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        PlaySound(sound, 1.0);
    }

    private void PlaySound(
        SoundItem sound,
        double volumeMultiplier)
    {
        AudioFileReader? reader = null;
        WasapiOut? output = null;
        MMDevice? device = null;
        ActiveSound? active = null;

        try
        {
            if (!File.Exists(sound.FilePath))
                throw new FileNotFoundException(
                    "The audio file could not be found.",
                    sound.FilePath);

            reader = new AudioFileReader(sound.FilePath);

            reader.Volume = (float)Math.Clamp(
                (sound.Volume / 100.0) * volumeMultiplier,
                0.0,
                2.0);

            using var enumerator = new MMDeviceEnumerator();

            device = string.IsNullOrEmpty(_selectedAudioDeviceId)
                ? enumerator.GetDefaultAudioEndpoint(
                    DataFlow.Render,
                    Role.Multimedia)
                : enumerator.GetDevice(_selectedAudioDeviceId);

            output = new WasapiOut(
                device,
                AudioClientShareMode.Shared,
                true,
                50);

            output.Init(reader);

            active = new ActiveSound(output, reader, device);
            ActiveSound playback = active;

            _activeSounds.Add(playback);

            output.PlaybackStopped += (_, _) =>
            {
                if (!Dispatcher.HasShutdownStarted &&
                    !Dispatcher.HasShutdownFinished)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_activeSounds.Remove(playback))
                            playback.Dispose();
                    }));
                }
            };

            output.Play();
        }
        catch (Exception ex)
        {
            if (active is not null)
            {
                _activeSounds.Remove(active);
                active.Dispose();
            }
            else
            {
                output?.Dispose();
                reader?.Dispose();
                device?.Dispose();
            }

            MessageBox.Show(
                this,
                $"Unable to play sound:\n\n{ex.Message}",
                "Crossbar Chaos",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
