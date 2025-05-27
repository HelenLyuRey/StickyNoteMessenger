using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace StickyNoteMessenger
{
    public partial class MainWindow : Window
    {
        private DiscordService _discordService = null!;
        private bool _isConnecting = false;
        private int _unreadCount = 0;
        private bool _isScrolledToBottom = true;
        private DispatcherTimer _statusTimer = null!;

        public MainWindow()
        {
            InitializeComponent();
            
            // Set initial position
            this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 50;
            this.Top = 50;

            // Initialize UI
            InitializeUI();
            
            // Initialize Discord service
            InitializeDiscordService();
            
            // Connect to Discord automatically
            _ = ConnectToDiscordAsync();
        }

        private void InitializeUI()
        {
            // Initialize status timer for clearing temporary messages
            _statusTimer = new DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(5);
            _statusTimer.Tick += StatusTimer_Tick;
            
            // Set initial status
            UpdateStatus("Initializing...", false);
            UpdateConnectionIndicator(false);
            
            // Handle window focus events for notification badge
            this.Activated += MainWindow_Activated;
            this.Deactivated += MainWindow_Deactivated;
        }

        private void InitializeDiscordService()
        {
            try
            {
                _discordService = new DiscordService();
                
                // Subscribe to Discord events
                _discordService.OnMessageReceived += OnDiscordMessageReceived;
                _discordService.OnConnectionStatusChanged += OnDiscordConnectionStatusChanged;
                
                UpdateStatus("Discord service initialized", false);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to initialize Discord service: {ex.Message}");
            }
        }

        private async Task ConnectToDiscordAsync()
        {
            if (_isConnecting) return;
            
            try
            {
                _isConnecting = true;
                UpdateStatus("Connecting to Discord...", false);
                UpdateConnectionIndicator(false);
                
                bool connected = await _discordService.ConnectAsync();
                
                if (connected)
                {
                    UpdateStatus("Connected to Discord", true);
                    UpdateConnectionIndicator(true);
                    EnableInput(true);
                }
                else
                {
                    ShowError("Failed to connect to Discord. Check your config.json");
                    EnableInput(false);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Discord connection error: {ex.Message}");
                EnableInput(false);
            }
            finally
            {
                _isConnecting = false;
            }
        }

        // Handle incoming Discord messages (called from Discord service)
        // Handle incoming Discord messages with proper badge logic
        private void OnDiscordMessageReceived(string message, DateTime timestamp)
        {
            // Ensure we're on the UI thread
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    AddMessageToDisplay($"TODO: {timestamp:HH:mm} - {message}", true);
                    
                    // Show notification badge ONLY if window is minimized or not active
                    if (this.WindowState == WindowState.Minimized || !this.IsActive)
                    {
                        IncrementNotificationBadge();
                    }
                }
                catch (Exception ex)
                {
                    ShowError($"Error displaying message: {ex.Message}");
                }
            });
        }

        // Handle Discord connection status changes
        private void OnDiscordConnectionStatusChanged(string status)
        {
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    bool isConnected = status.Contains("Connected") && !status.Contains("failed");
                    UpdateConnectionIndicator(isConnected);
                    UpdateStatus(status, isConnected);
                    EnableInput(isConnected && !_isConnecting);
                }
                catch (Exception ex)
                {
                    ShowError($"Status update error: {ex.Message}");
                }
            });
        }

        private void UpdateStatus(string message, bool isSuccess)
        {
            // Always use the same color as other text - no color coding
            StatusText.Text = message;
            StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5A5A5A"));
            
            // Auto-clear status after 5 seconds for temporary messages
            if (message != "Ready")
            {
                _statusTimer.Stop();
                _statusTimer.Start();
            }
        }

        private void ShowError(string errorMessage)
        {
            // Keep same subtle color, just show the error text
            StatusText.Text = errorMessage;
            StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5A5A5A"));
            UpdateConnectionIndicator(false);
            
            // Log to console for debugging
            Console.WriteLine($"[ERROR] {errorMessage}");
        }

        private void UpdateConnectionIndicator(bool isConnected)
        {
            // Subtle colors - green when connected, gray when not
            ConnectionIndicator.Fill = new SolidColorBrush(isConnected ? 
                (Color)ColorConverter.ConvertFromString("#90EE90") : 
                (Color)ColorConverter.ConvertFromString("#CCCCCC"));
            ConnectionIndicator.ToolTip = isConnected ? "Connected" : "Disconnected";
        }

        private void EnableInput(bool enabled)
        {
            InputTextBox.IsEnabled = enabled;
            if (!enabled)
            {
                InputTextBox.Text = "Connection required...";
                InputTextBox.Foreground = new SolidColorBrush(Colors.Gray);
            }
            else if (InputTextBox.Text == "Connection required...")
            {
                InputTextBox.Text = "Type action item...";
                InputTextBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C5234"));
            }
        }

        private void StatusTimer_Tick(object? sender, EventArgs e)
        {
            _statusTimer.Stop();
            UpdateStatus("Ready", true);
        }

        // Notification badge management
        private void IncrementNotificationBadge()
        {
            // Only increment if window is not visible or is minimized
            if (this.WindowState == WindowState.Minimized || !this.IsVisible || !this.IsActive)
            {
                _unreadCount++;
                UpdateNotificationBadge();
            }
        }

        private void UpdateNotificationBadge()
        {
            if (_unreadCount > 0 && (this.WindowState == WindowState.Minimized || !this.IsActive))
            {
                NotificationBadge.Visibility = Visibility.Visible;
                NotificationCount.Text = _unreadCount > 9 ? "9+" : _unreadCount.ToString();
            }
            else
            {
                NotificationBadge.Visibility = Visibility.Collapsed;
            }
        }

        private void ClearNotificationBadge()
        {
            _unreadCount = 0;
            NotificationBadge.Visibility = Visibility.Collapsed;
        }

        // Window focus events - handle showing/hiding badge properly
        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            // Clear badge when window becomes active (foreground)
            ClearNotificationBadge();
        }

        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
            // Don't do anything here - badge will show when messages arrive
        }

        // Override state changed to handle minimize/restore
        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            
            if (this.WindowState == WindowState.Normal && this.IsActive)
            {
                // Window is restored and active - clear badge
                ClearNotificationBadge();
            }
            else if (this.WindowState == WindowState.Minimized)
            {
                // Window is minimized - badge will show when new messages arrive
                UpdateNotificationBadge();
            }
        }

        private void FlashTaskbar()
        {
            // Flash the taskbar icon (Windows API)
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                // Simple implementation - you could add WinAPI calls here for more advanced flashing
            }
            catch
            {
                // Ignore if flashing fails
            }
        }

        // Scroll detection for auto-scroll behavior
        private void MessageScrollViewer_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            var scrollViewer = sender as System.Windows.Controls.ScrollViewer;
            if (scrollViewer != null)
            {
                // Check if user is at the bottom
                _isScrolledToBottom = scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 10;
            }
        }

        // Make window draggable by clicking anywhere on it
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try
                {
                    this.DragMove();
                }
                catch
                {
                    // Ignore drag errors
                }
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
            if (InputTextBox.Text == "Type action item..." || InputTextBox.Text == "Connection required...")
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
            
            if (string.IsNullOrEmpty(messageText) || 
                messageText == "Type action item..." || 
                messageText == "Connection required...")
                return;

            try
            {
                // Disable input temporarily
                InputTextBox.IsEnabled = false;
                UpdateStatus("Sending message...", false);

                // Show message in UI immediately
                AddMessageToDisplay($"ACTION ITEM: {DateTime.Now:HH:mm} - {messageText}", false);
                
                // Clear input
                InputTextBox.Text = "";

                // Send to Discord
                if (_discordService?.IsConnected == true)
                {
                    bool sent = await _discordService.SendDirectMessageAsync(messageText);
                    
                    if (sent)
                    {
                        UpdateStatus("Message sent", true);
                    }
                    else
                    {
                        ShowError("Failed to send message to Discord");
                    }
                }
                else
                {
                    ShowError("Not connected to Discord");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Send error: {ex.Message}");
            }
            finally
            {
                // Re-enable input
                InputTextBox.IsEnabled = true;
                InputTextBox.Focus();
            }
        }

        // Add message to the display area
        private void AddMessageToDisplay(string message, bool isIncoming)
        {
            try
            {
                MessagesTextBlock.Text += $"\n• {message}";
                
                // Auto-scroll to bottom only if user was already at bottom or if it's their own message
                if (_isScrolledToBottom || !isIncoming)
                {
                    MessageScrollViewer.ScrollToEnd();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Display error: {ex.Message}");
            }
        }

        // Clean up Discord connection when closing
        protected override async void OnClosed(EventArgs e)
        {
            try
            {
                _statusTimer?.Stop();
                if (_discordService != null)
                {
                    await _discordService.DisconnectAsync();
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
            base.OnClosed(e);
        }
    }
}