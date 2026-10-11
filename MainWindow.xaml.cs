
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CrossbarChaos;

public partial class MainWindow : Window
{
    private readonly StatsApiConfiguration _statsApi = new();

    private readonly CancellationTokenSource _shutdown = new();

    private bool _randomizerEnabled;
    private DateTime _lastCrossbarEventUtc = DateTime.MinValue;
    private (double X, double Y, double Z)? _lastCrossbarLocation;

    private readonly System.Collections.Generic.List<SoundItem> _soundShuffleBag = new();
    private readonly System.Collections.Generic.List<SoundItem> _shuffleEligibleSounds = new();
    private SoundItem? _lastPlayedSound;



    public MainWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            string result = _statsApi.Enable();

            if (result.Contains("FAILED", StringComparison.OrdinalIgnoreCase) ||
                result.Contains("already running", StringComparison.OrdinalIgnoreCase) ||
                result.Contains("No Rocket League", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    this,
                    result,
                    "Stats API Setup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            _ = ListenForRocketLeagueAsync(_shutdown.Token);
        };

        Closing += (_, _) =>
        {
            _shutdown.Cancel();

            string errors = _statsApi.Restore();

            if (!string.IsNullOrEmpty(errors))
            {
                MessageBox.Show(
                    this,
                    errors,
                    "Stats API Restoration Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        };
    }

    private async Task ListenForRocketLeagueAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();

                await socket.ConnectAsync(
                    new Uri("ws://127.0.0.1:49124/"), token);

                ConnectionText.Text = "Connected to Rocket League";

                byte[] buffer = new byte[8192];
                using var message = new MemoryStream();

                while (socket.State == WebSocketState.Open &&
                       !token.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    if (result.MessageType != WebSocketMessageType.Text)
                        continue;

                    if (message.Length + result.Count > 1_048_576)
                        break;

                    message.Write(buffer, 0, result.Count);

                    if (!result.EndOfMessage)
                        continue;

                    ProcessStatsEvent(
                        Encoding.UTF8.GetString(message.ToArray()));

                    message.SetLength(0);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (WebSocketException)
            {
                // Rocket League is unavailable or disconnected.
            }
            catch (Exception)
            {
                ConnectionText.Text = "Stats API error - retrying";
            }

            if (token.IsCancellationRequested)
                return;

            ConnectionText.Text = "Waiting for Rocket League";

            try
            {
                await Task.Delay(3000, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void ProcessStatsEvent(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("Event", out var eventName) ||
                eventName.ValueKind != JsonValueKind.String ||
                eventName.GetString() != "CrossbarHit")
                return;

            if (!root.TryGetProperty("Data", out var dataProperty) ||
                dataProperty.ValueKind != JsonValueKind.String)
                return;

            string? innerJson = dataProperty.GetString();

            if (string.IsNullOrWhiteSpace(innerJson))
                return;

            using var innerDocument = JsonDocument.Parse(innerJson);
            var data = innerDocument.RootElement;

            if (!data.TryGetProperty("ImpactForce", out var impact) ||
                impact.ValueKind != JsonValueKind.Number)
                return;


            double force = impact.GetDouble();


            double speed =
                data.TryGetProperty("BallSpeed", out var speedProperty) &&
                speedProperty.ValueKind == JsonValueKind.Number
                    ? speedProperty.GetDouble()
                    : 0.0;

            // Filter repeated reports of the same nearby contact.
            if (data.TryGetProperty("BallLocation", out var location) &&
                location.ValueKind == JsonValueKind.Object &&
                location.TryGetProperty("X", out var xProperty) &&
                location.TryGetProperty("Y", out var yProperty) &&
                location.TryGetProperty("Z", out var zProperty) &&
                xProperty.TryGetDouble(out double x) &&
                yProperty.TryGetDouble(out double y) &&
                zProperty.TryGetDouble(out double z))
            {
                DateTime now = DateTime.UtcNow;
                bool duplicateContact = false;

                if (_lastCrossbarLocation is { } previous)
                {
                    double dx = x - previous.X;
                    double dy = y - previous.Y;
                    double dz = z - previous.Z;

                    duplicateContact =
                        (now - _lastCrossbarEventUtc).TotalMilliseconds < 120 &&
                        (dx * dx + dy * dy + dz * dz) < 150.0 * 150.0;
                }

                // Track every event, including discarded duplicates.
                _lastCrossbarEventUtc = now;
                _lastCrossbarLocation = (x, y, z);

                if (duplicateContact)
                    return;
            }

            ConnectionText.Text =

                $"Crossbar hit detected! Impact: {force:F1}";

            if (!_randomizerEnabled)
                return;

            var eligibleSounds =
                new System.Collections.Generic.List<SoundItem>();

            foreach (SoundItem sound in _soundLibrary.Sounds)
            {
                if (sound.IsEnabled &&
                    sound.Volume > 0 &&
                    File.Exists(sound.FilePath))
                {
                    eligibleSounds.Add(sound);
                }
            }

            if (eligibleSounds.Count == 0)
                return;

            // Reset the rotation if the eligible sound library changes.
            if (eligibleSounds.Count != _shuffleEligibleSounds.Count ||
                eligibleSounds.Exists(sound => !_shuffleEligibleSounds.Contains(sound)))
            {
                _shuffleEligibleSounds.Clear();
                _shuffleEligibleSounds.AddRange(eligibleSounds);
                _soundShuffleBag.Clear();
            }

            // Start a new randomized rotation when the bag is empty.
            if (_soundShuffleBag.Count == 0)
                _soundShuffleBag.AddRange(eligibleSounds);

            int selectedIndex = Random.Shared.Next(_soundShuffleBag.Count);

            // Prevent consecutive repeats, including across rotations.
            if (_soundShuffleBag.Count > 1 &&
                ReferenceEquals(_soundShuffleBag[selectedIndex], _lastPlayedSound))
            {
                selectedIndex =
                    (selectedIndex + 1 +
                     Random.Shared.Next(_soundShuffleBag.Count - 1))
                    % _soundShuffleBag.Count;
            }

            SoundItem selectedSound = _soundShuffleBag[selectedIndex];

            _soundShuffleBag.RemoveAt(selectedIndex);
            _lastPlayedSound = selectedSound;

            double speedMph = Math.Max(0.0, speed) * 0.62137119223733;

            double volumeMultiplier =
                0.50 + 0.50 * Math.Clamp(speedMph / 125.0, 0.0, 1.0);

            PlaySound(selectedSound, volumeMultiplier);
        }
        catch (JsonException)
        {
            // Ignore invalid JSON messages.
        }
    }

    private void RandomizerToggle_Click(
        object sender,
        RoutedEventArgs e)
    {
        _randomizerEnabled = !_randomizerEnabled;

        RandomizerToggle.Content =
            _randomizerEnabled ? "Enabled" : "Disabled";

        RandomizerToggle.Foreground =
            _randomizerEnabled
                ? System.Windows.Media.Brushes.LimeGreen
                : new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(255, 100, 100));
    }


    private void TitleBar_MouseLeftButtonDown(
        object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(
        object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(
        object sender, RoutedEventArgs e)
    {
        Close();
    }
}
