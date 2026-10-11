
using NAudio.CoreAudioApi;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace CrossbarChaos;

public partial class MainWindow
{
    private static readonly string AudioOutputSettingsPath =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "CrossbarChaos",
            "audio-output.txt");

    private string? _selectedAudioDeviceId;

    private void AudioOutputComboBox_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        AudioOutputComboBox.SelectionChanged -=
            AudioOutputComboBox_SelectionChanged;

        try
        {
            string? savedId = File.Exists(AudioOutputSettingsPath)
                ? File.ReadAllText(AudioOutputSettingsPath).Trim()
                : null;

            AudioOutputComboBox.Items.Clear();

            AudioOutputComboBox.Items.Add(new ComboBoxItem
            {
                Content = "Windows Default",
                Tag = ""
            });

            int selectedIndex = 0;

            using var enumerator = new MMDeviceEnumerator();

            foreach (var device in enumerator.EnumerateAudioEndPoints(
                DataFlow.Render,
                DeviceState.Active))
            {
                AudioOutputComboBox.Items.Add(new ComboBoxItem
                {
                    Content = device.FriendlyName,
                    Tag = device.ID
                });

                if (string.Equals(
                    savedId,
                    device.ID,
                    StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex =
                        AudioOutputComboBox.Items.Count - 1;
                }
            }

            AudioOutputComboBox.SelectedIndex = selectedIndex;

            _selectedAudioDeviceId =
                (AudioOutputComboBox.SelectedItem as ComboBoxItem)?
                    .Tag as string;

            if (string.IsNullOrEmpty(_selectedAudioDeviceId))
                _selectedAudioDeviceId = null;

            AudioOutputComboBox.IsEnabled = true;

            AudioOutputComboBox.SelectionChanged +=
                AudioOutputComboBox_SelectionChanged;
        }
        catch (Exception ex)
        {
            AudioOutputComboBox.IsEnabled = false;

            MessageBox.Show(
                this,
                $"Unable to load audio devices:\n\n{ex.Message}",
                "Crossbar Chaos",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void AudioOutputComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (AudioOutputComboBox.SelectedItem
            is not ComboBoxItem item)
            return;

        _selectedAudioDeviceId = item.Tag as string;

        if (string.IsNullOrEmpty(_selectedAudioDeviceId))
            _selectedAudioDeviceId = null;

        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(AudioOutputSettingsPath)!);

            File.WriteAllText(
                AudioOutputSettingsPath,
                _selectedAudioDeviceId ?? "");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Unable to save audio device:\n\n{ex.Message}",
                "Crossbar Chaos",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void AudioOutputItem_RequestBringIntoView(
        object sender,
        RequestBringIntoViewEventArgs e)
    {
        if (System.Windows.Input.Keyboard.IsKeyDown(
                System.Windows.Input.Key.Up) ||
            System.Windows.Input.Keyboard.IsKeyDown(
                System.Windows.Input.Key.Down))
        {
            return;
        }

        e.Handled = true;
    }

}
