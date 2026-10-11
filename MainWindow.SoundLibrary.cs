
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

        PreviewMouseDown -= ClearSoundListVisualFocus;
        PreviewMouseDown += ClearSoundListVisualFocus;
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
            foreach (string filePath in dialog.FileNames)
            {
                _soundLibrary.AddSound(filePath);
            }
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
