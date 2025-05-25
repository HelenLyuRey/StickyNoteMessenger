using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace StickyNoteMessenger
{
    public partial class MainWindow : Window
    {
        private DiscordService _discordService = null!;
        private bool _isConnecting = false;

        public MainWindow()
        {
            InitializeComponent();
            
            // Set initial position
            this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 50;
            this.Top = 50;

            // Initialize Discord service
            InitializeDiscordService();
            
            // Connect to Discord automatically
            _ = ConnectToDiscordAsync();
        }

        private void InitializeDiscordService()
        {
            _discordService = new DiscordService();
            
            // Subscribe to Discord events
            _discordService.OnMessageReceived += OnDiscordMessageReceived;
            _discordService.OnConnectionStatusChanged += OnDiscordConnectionStatusChanged;
        }

        private async Task ConnectToDiscordAsync()
        {
            if (_isConnecting) return;
            
            _isConnecting = true;
            UpdateConnectionStatus("Connecting to Discord...");
            
            bool connected = await _discordService.ConnectAsync();
            
            if (!connected)
            {
                UpdateConnectionStatus("Discord connection failed");
            }
            
            _isConnecting = false;
        }

        // Handle incoming Discord messages (called from Discord service)
        private void OnDiscordMessageReceived(string message, DateTime timestamp)
        {
            // Ensure we're on the UI thread
            Dispatcher.BeginInvoke(() =>
            {
                AddMessageToDisplay($"TODO: {timestamp:HH:mm} - {message}");
            });
        }

        // Handle Discord connection status changes
        private void OnDiscordConnectionStatusChanged(string status)
        {
            Dispatcher.BeginInvoke(() =>
            {
                UpdateConnectionStatus(status);
            });
        }

        private void UpdateConnectionStatus(string status)
        {
            // Update the header to show connection status
            HeaderText.Text = $"Project Meeting Notes - Dec 2024 ({status})";
        }

        // Make window draggable by clicking anywhere on it
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        // Handle ESC key to hide window
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.WindowState = WindowState.Minimized;
            }
        }

        // Handle input text box focus (clear placeholder text)
        private void InputTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (InputTextBox.Text == "Type action item...")
            {
                InputTextBox.Text = "";
                InputTextBox.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#2C5234"));
            }
        }

        // Handle Ctrl+Enter to send message
        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _ = SendMessageAsync();
                e.Handled = true; // Prevent line break
            }
        }

        // Send message via Discord
        private async Task SendMessageAsync()
        {
            string messageText = InputTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(messageText) || messageText == "Type action item...")
                return;

            // Show message in UI immediately
            AddMessageToDisplay($"ACTION ITEM: {DateTime.Now:HH:mm} - {messageText}");
            
            // Clear input
            InputTextBox.Text = "";

            // Send to Discord
            if (_discordService.IsConnected)
            {
                bool sent = await _discordService.SendDirectMessageAsync(messageText);
                
                if (!sent)
                {
                    AddMessageToDisplay($"• ERROR: Failed to send message");
                }
            }
            else
            {
                AddMessageToDisplay($"• ERROR: Not connected to Discord");
            }
        }

        // Add message to the display area
        private void AddMessageToDisplay(string message)
        {
            MessagesTextBlock.Text += $"\n• {message}";
            
            // Auto-scroll to bottom
            MessageScrollViewer.ScrollToEnd();
        }

        // Clean up Discord connection when closing
        protected override async void OnClosed(EventArgs e)
        {
            if (_discordService != null)
            {
                await _discordService.DisconnectAsync();
            }
            base.OnClosed(e);
        }
    }
}