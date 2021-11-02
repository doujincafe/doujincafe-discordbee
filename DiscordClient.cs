using System;
using System.Diagnostics;
using System.Text;
using DiscordRPC;
using DiscordRPC.Logging;
using DiscordRPC.Message;

namespace MusicBeePlugin
{
    public class DiscordClient
    {
        private DiscordRpcClient _client;
        private readonly RichPresence _presence;
        private string _clientId;
        private bool _isConnected;
        private Action _toInvokeWhenReady;

        public DiscordClient()
        {
            _presence = new RichPresence
            {
                Assets = new Assets()
            };
        }

        public void InitialiseDiscordRpc(string clientId)
        {
            // For reconnection.
            _clientId = clientId;

            // Do not initialise undisposed client.
            if (_client?.IsDisposed == true)
            {
                return;
            }

            _client = new DiscordRpcClient(clientId, logger: new DebugLogger(LogLevel.Trace));
            _client.OnError += ErrorCallback;
            _client.OnClose += DisconnectedCallback;
            _client.OnReady += ReadyCallback;
            _client.OnConnectionFailed += ConnectionFailedCallback;
            _client.ShutdownOnly = true;
            _client.SkipIdenticalPresence = true;
            _client.Initialize();
        }

        public void SetClientId(string clientId)
        {
            if (clientId == _clientId)
            {
                return;
            }

            // Clear and dispose.
            Close(true);
            InitialiseDiscordRpc(clientId);
            UpdatePresence();
        }

        public void SetTrackNumbers(int current, int totalTracks)
        {
            if (current > totalTracks || totalTracks <= 0 || current <= 0)
            {
                _presence.Party = null;
                return;
            }

            _presence.Party = new Party
            {
                ID = Guid.NewGuid().ToString(),
                Max = totalTracks,
                Size = current
            };
        }

        public void ConfigureToInvokeWhenReady(Action delegateToInvoke)
        {
            _toInvokeWhenReady += delegateToInvoke;
        }

        public void ClearAssets()
        {
            _presence.Assets.LargeImageKey = null;
            _presence.Assets.LargeImageText = null;
            _presence.Assets.SmallImageKey = null;
            _presence.Assets.SmallImageText = null;
        }

        public void SetLargeAssets(string key, string text)
        {
            _presence.Assets.LargeImageKey = PadString(key);
            _presence.Assets.LargeImageText = PadString(text);
        }

        public void SetSmallAssets(string key, string text)
        {
            _presence.Assets.SmallImageKey = PadString(key);
            _presence.Assets.SmallImageText = PadString(text);
        }

        public void SetStartTimestamp(ulong startTime)
        {
            _presence.Timestamps = new Timestamps
            {
                StartUnixMilliseconds = startTime
            };
        }

        public void SetEndTimestamp(ulong endTime)
        {
            _presence.Timestamps = new Timestamps
            {
                EndUnixMilliseconds = endTime
            };
        }

        public void ClearTime()
        {
            _presence.Timestamps = null;
        }

        public void SetState(string state)
        {
            _presence.State = PadString(state);
        }

        public void SetDetails(string details)
        {
            _presence.Details = PadString(details);
        }

        public void UpdatePresence()
        {
            if (_client?.IsDisposed == true || !_isConnected) return;
            _client?.SetPresence(_presence);
        }

        public void Close(bool disposeClient = false)
        {
            _client?.ClearPresence();

            // Dispose.
            if (_client?.IsDisposed == false && disposeClient)
            {
                _client.Dispose();
            }
        }

        #region Utilities
        /// <summary>
        /// Discord allows only strings with a min length of 2 or the update fails.
        /// so add some exotic space (Mongolian vowel seperator) to the string if it is smaller.
        /// Discord also disallows strings bigger than 128 bytes so handle that as well.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static string PadString(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            if (input.Length < 2)
            {
                return input + "\u180E";
            }

            if (Encoding.UTF8.GetBytes(input).Length <= 128) return input;
            var buffer = new byte[128];
            var inputChars = input.ToCharArray();
            Encoding.UTF8.GetEncoder().Convert(
                inputChars,
                0,
                inputChars.Length,
                buffer,
                0,
                buffer.Length,
                false,
                out _,
                out var bytesUsed,
                out _);
            return Encoding.UTF8.GetString(buffer, 0, bytesUsed);
        }
        #endregion

        #region Event Handlers

        private static void ErrorCallback(object sender, ErrorMessage e)
        {
            Debug.WriteLine($"Errored: {e.Code} Msg: {e.Message}");
        }

        private void DisconnectedCallback(object sender, CloseMessage c)
        {
            Debug.WriteLine($"Disconnected: {c.Code} Msg: {c.Reason}");
            _isConnected = false;
        }

        private void ReadyCallback(object sender, ReadyMessage args)
        {
            Debug.WriteLine($"Ready. Connected to Discord Client with User: {args.User.Username}", "DiscordRpc");
            _isConnected = true;
            _toInvokeWhenReady();
        }

        private void ConnectionFailedCallback(object sender, ConnectionFailedMessage e)
        {
            Debug.WriteLine($"Connection Failed: ${e.Type}");
            // Re-initialise the client.
            _isConnected = false;
            InitialiseDiscordRpc(_clientId);
        }
        #endregion
    }
}
