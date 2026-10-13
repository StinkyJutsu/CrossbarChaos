
using Microsoft.Win32;
using System;
using System.Windows;

namespace CrossbarChaos;

public partial class MainWindow
{
    private readonly SoundLibrary _soundLibrary = new();


    private void SoundList_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        SoundList.ItemsSource = _soundLibrary.Sounds;

        var view = System.Windows.Data.CollectionViewSource
            .GetDefaultView(_soundLibrary.Sounds);

        view.Filter = item =>
            item is SoundItem sound &&
            (string.IsNullOrWhiteSpace(SoundSearchBox.Text) ||
             sound.FileName.Contains(
                 SoundSearchBox.Text,
                 StringComparison.OrdinalIgnoreCase));

        SoundSearchBox.TextChanged -= SoundSearchBox_TextChanged;
        SoundSearchBox.TextChanged += SoundSearchBox_TextChanged;

        PreviewMouseDown -= ClearSoundListVisualFocus;
        PreviewMouseDown += ClearSoundListVisualFocus;
    }

    private void SoundSearchBox_TextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        System.Windows.Data.CollectionViewSource
            .GetDefaultView(_soundLibrary.Sounds)
            .Refresh();
    }

    private void SoundVolumeSlider_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Slider slider)
            UpdateSoundVolumeSliderColor(slider);
    }

    private void SoundVolumeSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is System.Windows.Controls.Slider slider && slider.IsLoaded)
            UpdateSoundVolumeSliderColor(slider);
    }

    private static void UpdateSoundVolumeSliderColor(
        System.Windows.Controls.Slider slider)
    {
        slider.ApplyTemplate();

        if (slider.Template.FindName("PART_Track", slider)
            is not System.Windows.Controls.Primitives.Track track)
            return;

        bool amplified = slider.Value > 100;

        track.DecreaseRepeatButton.Background =
            new System.Windows.Media.SolidColorBrush(
                amplified
                    ? System.Windows.Media.Color.FromRgb(207, 63, 68)
                    : System.Windows.Media.Color.FromRgb(96, 127, 157));

        track.IncreaseRepeatButton.Background =
            new System.Windows.Media.SolidColorBrush(
                amplified
                    ? System.Windows.Media.Color.FromRgb(106, 41, 50)
                    : System.Windows.Media.Color.FromRgb(48, 67, 88));
    }

    private void RefreshSoundListButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            var options = new System.IO.EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true
            };

            var files = new System.Collections.Generic.List<string>();

            foreach (string folder in _soundLibrary.TrackedFolders)
            {
                if (!System.IO.Directory.Exists(folder))
                    continue;

                foreach (string file in System.IO.Directory.EnumerateFiles(
                    folder, "*", options))
                {
                    string extension = System.IO.Path.GetExtension(file);

                    if (!extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) &&
                        !extension.Equals(".wav", StringComparison.OrdinalIgnoreCase))
                        continue;

                    files.Add(file);
                }
            }

            _soundLibrary.AddSounds(files);

            System.Windows.Data.CollectionViewSource
                .GetDefaultView(_soundLibrary.Sounds)
                .Refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Unable to refresh sound folders:\n\n{ex.Message}",
                "Crossbar Chaos",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ClearSoundListVisualFocus(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        // Don't interfere with the Audio Output dropdown.
        if (AudioOutputComboBox.IsDropDownOpen)
            return;

        if (e.OriginalSource is DependencyObject source &&
            System.Windows.Controls.ItemsControl.ContainerFromElement(
                SoundList, source) is System.Windows.Controls.ListBoxItem)
        {
            return;
        }

        System.Windows.Input.Keyboard.ClearFocus();
    }


    private void AddSoundsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add Sounds to Crossbar Chaos",
            Filter = "Audio Files (*.wav;*.mp3)|*.wav;*.mp3",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            _soundLibrary.AddSounds(dialog.FileNames);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to add sounds:\n\n{ex.Message}",
                "Crossbar Chaos",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void AddFolderButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Add Sound Folders to Crossbar Chaos",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            _soundLibrary.RegisterFolders(dialog.FolderNames);

            var options = new System.IO.EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true
            };

            var files = new System.Collections.Generic.List<string>();

            foreach (string folder in dialog.FolderNames)
            {
                foreach (string file in System.IO.Directory.EnumerateFiles(
                    folder, "*", options))
                {
                    string extension = System.IO.Path.GetExtension(file);

                    if (!extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) &&
                        !extension.Equals(".wav", StringComparison.OrdinalIgnoreCase))
                        continue;

                    files.Add(file);
                }
            }

            _soundLibrary.AddSounds(files);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Unable to finish importing sounds:\n\n{ex.Message}",
                "Crossbar Chaos",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void RemoveSelectedButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (SoundList.SelectedItem is not SoundItem sound)
        {
            MessageBox.Show(
                this,
                "Select a sound to remove.",
                "Crossbar Chaos",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            _soundLibrary.RemoveSound(sound);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Unable to remove sound:\n\n{ex.Message}",
                "Crossbar Chaos",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
  
}
