﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text;
using DiscordRPC;
using DiscordRPC.Logging;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        private MusicBeeApiInterface _mbApiInterface;
        private readonly PluginInfo _about = new PluginInfo();
        private DiscordRpcClient _discordClient;
        private readonly RichPresence _discordPresence = new RichPresence();
        private LayoutHandler _layoutHandler;
        private Settings _settings;
        private SettingsWindow _settingsWindow;

        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            _mbApiInterface = new MusicBeeApiInterface();
            _mbApiInterface.Initialise(apiInterfacePtr);
            _about.PluginInfoVersion = PluginInfoVersion;
            _about.Name = "DiscordBee";
            _about.Description = "Update your Discord Profile with the currently playing track";
            _about.Author = "Stefan Lengauer";
            _about.TargetApplication = "";   // current only applies to artwork, lyrics or instant messenger name that appears in the provider drop down selector or target Instant Messenger
            _about.Type = PluginType.General;
            _about.VersionMajor = 1;  // your plugin version
            _about.VersionMinor = 4;
            _about.Revision = 3;
            _about.MinInterfaceVersion = MinInterfaceVersion;
            _about.MinApiRevision = MinApiRevision;
            _about.ReceiveNotifications = (ReceiveNotificationFlags.PlayerEvents | ReceiveNotificationFlags.TagEvents);
            _about.ConfigurationPanelHeight = 0;   // height in pixels that musicbee should reserve in a panel for config settings. When set, a handle to an empty panel will be passed to the Configure function

            var settingsFilePath = _mbApiInterface.Setting_GetPersistentStoragePath() + _about.Name + "\\" + _about.Name + ".settings";

            _settings = Settings.GetInstance(settingsFilePath);
            _settingsWindow = new SettingsWindow(this, _settings);

            _discordClient = new DiscordRpcClient(_settings.ClientId);
            _discordClient.OnError += ErrorCallback;
            _discordClient.OnClose += DisconnectedCallback;
            _discordClient.SkipIdenticalPresence = true;
            _discordClient.Logger = new DebugLogger(LogLevel.Trace);
            _discordClient.Initialize();

            _discordPresence.Assets = new Assets();
            _discordPresence.Party = new Party();
            _discordPresence.Timestamps = new Timestamps();

            // Match least number of chars possible but min 1
            _layoutHandler = new LayoutHandler(new Regex("\\[(.+?)\\]"));

            Debug.WriteLine(_about.Name + " loaded");

            return _about;
        }

        private void ErrorCallback(Object sender, DiscordRPC.Message.ErrorMessage e)
        {
            Debug.WriteLine("Errored: " + e.Code + " Msg: " + e.Message);
        }

        private void DisconnectedCallback(Object sender, DiscordRPC.Message.CloseMessage c)
        {
            Debug.WriteLine("Disconnected: " + c.Code + " Msg: " + c.Reason);
        }

        public string GetVersionString()
        {
            return "" + _about.VersionMajor + "." + _about.VersionMinor + "." + _about.Revision;
        }

        public bool Configure(IntPtr panelHandle)
        {
            _settingsWindow.Show();
            return true;
        }

        // called by MusicBee when the user clicks Apply or Save in the MusicBee Preferences screen.
        // its up to you to figure out whether anything has changed and needs updating
        public void SaveSettings()
        {
            _settings.Save();
        }

        // MusicBee is closing the plugin (plugin is being disabled by user or MusicBee is shutting down)
        public void Close(PluginCloseReason reason)
        {
            _discordClient.ClearPresence();
            _discordClient.Dispose();
        }

        // uninstall this plugin - clean up any persisted files
        public void Uninstall()
        {
            _settings.Delete();
        }

        // receive event notifications from MusicBee
        // you need to set about.ReceiveNotificationFlags = PlayerEvents to receive all notifications, and not just the startup event
        public void ReceiveNotification(string sourceFileUrl, NotificationType type)
        {
            // perform some action depending on the notification type
            switch (type)
            {
                case NotificationType.PluginStartup:
                case NotificationType.TrackChanged:
                case NotificationType.PlayStateChanged:
                    UpdateDiscordPresence(_mbApiInterface.Player_GetPlayState());
                    break;
            }
        }

        public Dictionary<string, string> GenerateMetaDataDictionary()
        {
            var ret = new Dictionary<string, string>(Enum.GetNames(typeof(MetaDataType)).Length);

            foreach (MetaDataType elem in Enum.GetValues(typeof(MetaDataType)))
            {
                ret.Add(elem.ToString(), _mbApiInterface.NowPlaying_GetFileTag(elem));
            }
            ret.Add("PlayState", _mbApiInterface.Player_GetPlayState().ToString());
            ret.Add("Volume", Convert.ToInt32(_mbApiInterface.Player_GetVolume() * 100.0f).ToString());

            return ret;
        }

        private void UpdateDiscordPresence(PlayState playerGetPlayState)
        {
            var metaDataDict = GenerateMetaDataDictionary();

            // Discord allows only strings with a min length of 2 or the update fails
            // so add some exotic space (Mongolian vovel seperator) to the string if it is smaller
            // Discord also disallows strings bigger than 128bytes so handle that as well
            string PadString(string input)
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
                    chars: inputChars,
                    charIndex: 0,
                    charCount: inputChars.Length,
                    bytes: buffer,
                    byteIndex: 0,
                    byteCount: buffer.Length,
                    flush: false,
                    charsUsed: out _,
                    bytesUsed: out int bytesUsed,
                    completed: out _);
                return Encoding.UTF8.GetString(buffer, 0, bytesUsed);
            }

            void SetImage(string name)
            {
                if (_settings.TextOnly)
                {
                    _discordPresence.Assets.LargeImageKey = null;
                    _discordPresence.Assets.LargeImageText = null;
                    _discordPresence.Assets.SmallImageKey = null;
                    _discordPresence.Assets.SmallImageText = null;
                    return;
                }

                // Large Image Text
                var codec = _mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.Kind);
                var size = _mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.Size);
                var channels = _mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.Channels);
                var sampleRate = _mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.SampleRate);
                var bitrate = _mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.Bitrate);
                var duration = _mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.Duration);

                _discordPresence.Assets.LargeImageText = PadString(!_settings.DoNotDisplayInformation ? $"MusicBee: {codec} / {bitrate} / {sampleRate} / {channels} / {size} / {duration}" : _settings.LargeImageText);

                _discordPresence.Assets.LargeImageKey = _settings.LargeImageId;
                _discordPresence.Assets.SmallImageKey = PadString(name);
                _discordPresence.Assets.SmallImageText = PadString(_layoutHandler.Render(_settings.SmallImageText, metaDataDict, _settings.Seperator));
            }

            _discordPresence.State = PadString(_layoutHandler.Render(_settings.PresenceState, metaDataDict, _settings.Seperator));

            var t = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1));

            if (_settings.ShowRemainingTime)
            {
                // show remaining time
                // subtract current track position from total duration for position seeking
                var totalRemainingDuration = _mbApiInterface.NowPlaying_GetDuration() - _mbApiInterface.Player_GetPosition();
                _discordPresence.Timestamps.EndUnixMilliseconds = (ulong)(Math.Round(t.TotalSeconds) + Math.Round(totalRemainingDuration / 1000.0));
            }
            else
            {
                // show elapsed time
                _discordPresence.Timestamps.StartUnixMilliseconds = (ulong)(Math.Round(t.TotalSeconds) - Math.Round(_mbApiInterface.Player_GetPosition() / 1000.0));
            }

            switch (playerGetPlayState)
            {
                case PlayState.Playing:
                    SetImage(_settings.PlayingImage);
                    break;
                case PlayState.Stopped:
                    SetImage(_settings.StoppedImage);
                    _discordPresence.Timestamps.Start = null;
                    _discordPresence.Timestamps.End = null;
                    break;
                case PlayState.Paused:
                    SetImage(_settings.PausedImage);
                    _discordPresence.Timestamps.Start = null;
                    _discordPresence.Timestamps.End = null;
                    break;
                case PlayState.Undefined:
                case PlayState.Loading:
                    break;
            }

            _discordPresence.Details = PadString(_layoutHandler.Render(_settings.PresenceDetails, metaDataDict, _settings.Seperator));

            var trackcnt = -1;
            var trackno = -1;
            try
            {
                trackcnt = int.Parse(_layoutHandler.Render(_settings.PresenceTrackCnt, metaDataDict, _settings.Seperator));
                trackno = int.Parse(_layoutHandler.Render(_settings.PresenceTrackNo, metaDataDict, _settings.Seperator));
            }
            catch (Exception)
            {
                // ignored
            }

            if (trackcnt < trackno || trackcnt <= 0 || trackno <= 0)
            {
                _discordPresence.Party = null;
            }
            else
            {
                _discordPresence.Party = new Party
                {
                    ID = "aaaa",
                    Max = trackcnt,
                    Size = trackno
                };
            }

            if (!_settings.UpdatePresenceWhenStopped && (playerGetPlayState == PlayState.Paused || playerGetPlayState == PlayState.Stopped))
            {
                _discordClient.ClearPresence();
            }
            else
            {
                _discordClient.SetPresence(_discordPresence);
            }
        }
    }
    public class DebugLogger : ILogger
    {
        public LogLevel Level { get; set; }

        public DebugLogger(LogLevel level)
        {
            Level = level;
        }

        public void Error(string message, params object[] args)
        {
            if (Level > LogLevel.Error) return;
            Log(message, args);
        }

        public void Info(string message, params object[] args)
        {
            if (Level > LogLevel.Info) return;
            Log(message, args);
        }

        public void Trace(string message, params object[] args)
        {
            if (Level > LogLevel.Trace) return;
            Log(message, args);
        }

        public void Warning(string message, params object[] args)
        {
            if (Level > LogLevel.Warning) return;
            Log(message, args);
        }

        private void Log(string msg, params object[] args)
        {
            Debug.WriteLine("" + Level.ToString() + ": " + msg, args);
        }
    }
}
