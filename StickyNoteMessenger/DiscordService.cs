using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace StickyNoteMessenger
{
    public class DiscordService
    {
        private DiscordSocketClient _client = null!;
        private Config _config = null!;
        private bool _isConnected = false;
        
        // Event to notify UI of new messages
        public event Action<string, DateTime>? OnMessageReceived;
        public event Action<string>? OnConnectionStatusChanged;

        public DiscordService()
        {
            LoadConfig();
            InitializeDiscord();
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists("config.json"))
                {
                    string configText = File.ReadAllText("config.json");
                    _config = JsonConvert.DeserializeObject<Config>(configText) ?? new Config();
                }
                else
                {
                    _config = new Config();
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config: {ex.Message}");
                _config = new Config();
            }
        }

        private void SaveConfig()
        {
            try
            {
                string configText = JsonConvert.SerializeObject(_config, Formatting.Indented);
                File.WriteAllText("config.json", configText);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config: {ex.Message}");
            }
        }

        private void InitializeDiscord()
        {
            var config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.DirectMessages | GatewayIntents.MessageContent
            };

            _client = new DiscordSocketClient(config);
            
            // Event handlers
            _client.Log += LogAsync;
            _client.Ready += ReadyAsync;
            _client.MessageReceived += MessageReceivedAsync;
            _client.Connected += ConnectedAsync;
            _client.Disconnected += DisconnectedAsync;
        }

        public async Task<bool> ConnectAsync()
        {
            if (!_config.BotEnabled || string.IsNullOrEmpty(_config.DiscordToken))
            {
                OnConnectionStatusChanged?.Invoke("Discord disabled in config");
                return false;
            }

            try
            {
                await _client.LoginAsync(TokenType.Bot, _config.DiscordToken);
                await _client.StartAsync();
                return true;
            }
            catch (Exception ex)
            {
                OnConnectionStatusChanged?.Invoke($"Connection failed: {ex.Message}");
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            if (_client != null)
            {
                await _client.StopAsync();
                await _client.LogoutAsync();
            }
        }

        public async Task<bool> SendDirectMessageAsync(string message)
        {
            if (!_isConnected || string.IsNullOrEmpty(_config.TestUserId))
                return false;

            try
            {
                if (ulong.TryParse(_config.TestUserId, out ulong userId))
                {
                    var user = await _client.GetUserAsync(userId);
                    if (user != null)
                    {
                        await user.SendMessageAsync(message);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                OnConnectionStatusChanged?.Invoke($"Send failed: {ex.Message}");
            }
            return false;
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine($"Discord: {log}");
            return Task.CompletedTask;
        }

        private Task ReadyAsync()
        {
            Console.WriteLine($"Discord bot ready! Logged in as {_client.CurrentUser}");
            OnConnectionStatusChanged?.Invoke($"Connected as {_client.CurrentUser.Username}");
            return Task.CompletedTask;
        }

        private Task ConnectedAsync()
        {
            _isConnected = true;
            OnConnectionStatusChanged?.Invoke("Connected to Discord");
            return Task.CompletedTask;
        }

        private Task DisconnectedAsync(Exception exception)
        {
            _isConnected = false;
            OnConnectionStatusChanged?.Invoke("Disconnected from Discord");
            return Task.CompletedTask;
        }

        private Task MessageReceivedAsync(SocketMessage message)
        {
            // Only process DMs from the test user
            if (message.Channel is IDMChannel && 
                message.Author.Id.ToString() == _config.TestUserId && 
                !message.Author.IsBot)
            {
                // Notify UI on main thread
                OnMessageReceived?.Invoke(message.Content, message.Timestamp.DateTime);
            }
            
            return Task.CompletedTask; // Fix for async warning
        }

        public bool IsConnected => _isConnected;
        public string GetTestUserId() => _config.TestUserId;
    }
}