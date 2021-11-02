using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        private MusicBeeApiInterface _mbApiInterface;
        private readonly PluginInfo _about = new PluginInfo();
        private DiscordClient _rpcClient;
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
            _about.Revision = 5;
            _about.MinInterfaceVersion = MinInterfaceVersion;
            _about.MinApiRevision = MinApiRevision;
            _about.ReceiveNotifications = (ReceiveNotificationFlags.PlayerEvents | ReceiveNotificationFlags.TagEvents);
            _about.ConfigurationPanelHeight = 0;   // height in pixels that musicbee should reserve in a panel for config settings. When set, a handle to an empty panel will be passed to the Configure function

            var settingsFilePath = _mbApiInterface.Setting_GetPersistentStoragePath() + _about.Name + "\\" + _about.Name + ".settings";

            _settings = Settings.GetInstance(settingsFilePath);
            _settingsWindow = new SettingsWindow(this, _settings);

            _rpcClient = new DiscordClient();
            _rpcClient.InitialiseDiscordRpc(_settings.ClientId);

            _rpcClient.ConfigureToInvokeWhenReady(() =>
            {
                UpdateDiscordPresence(_mbApiInterface.Player_GetPlayState());
            });

            // Match least number of chars possible but min 1
            _layoutHandler = new LayoutHandler(new Regex("\\[(.+?)\\]"));

            Debug.WriteLine(_about.Name + " loaded");

            return _about;
        }

        public string GetVersionString()
        {
            return $"{_about.VersionMajor}.{_about.VersionMinor}.{_about.Revision}";
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
            _rpcClient.SetClientId(_settings.ClientId);
        }

        // MusicBee is closing the plugin (plugin is being disabled by user or MusicBee is shutting down)
        public void Close(PluginCloseReason reason)
        {
            _rpcClient.Close(true);
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
                    var playState = _mbApiInterface.Player_GetPlayState();
                    // assuming MusicBee wasn't closed and started again in the same Discord session
                    if (_settings.UpdatePresenceWhenStopped ||
                        playState != PlayState.Paused && playState != PlayState.Stopped)
                    {
                        UpdateDiscordPresence(playState);
                    }

                    break;
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
            ret.Add("Codec", _mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.Kind));
            ret.Add("FileSize", _mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.Size));
            ret.Add("AudioChannels", _mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.Channels));
            ret.Add("AudioSampleRate", _mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.SampleRate));
            ret.Add("AudioBitrate", _mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.Bitrate));
            ret.Add("Duration", _mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.Duration));

            return ret;
        }

        private void UpdateDiscordPresence(PlayState playerGetPlayState)
        {
            Debug.WriteLine("DiscordBee: Updating Presence with PlayState {0}...", playerGetPlayState);

            var t = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1));

            if (_settings.ShowRemainingTime)
            {
                // show remaining time
                // subtract current track position from total duration for position seeking
                var totalRemainingDuration = _mbApiInterface.NowPlaying_GetDuration() - _mbApiInterface.Player_GetPosition();
                _rpcClient.SetEndTimestamp((ulong)(Math.Round(t.TotalSeconds) + Math.Round(totalRemainingDuration / 1000.0)));
            }
            else
            {
                // show elapsed time
                _rpcClient.SetStartTimestamp((ulong)(Math.Round(t.TotalSeconds) - Math.Round(_mbApiInterface.Player_GetPosition() / 1000.0)));
            }

            switch (playerGetPlayState)
            {
                case PlayState.Playing:
                    SetImage(_settings.PlayingImage);
                    break;
                case PlayState.Stopped:
                    SetImage(_settings.StoppedImage);
                    _rpcClient.ClearTime();
                    break;
                case PlayState.Paused:
                    SetImage(_settings.PausedImage);
                    _rpcClient.ClearTime();
                    break;
                case PlayState.Undefined:
                case PlayState.Loading:
                    break;
            }

            _rpcClient.SetState(RenderLayoutHandler(_settings.PresenceState));
            _rpcClient.SetDetails(RenderLayoutHandler(_settings.PresenceDetails));

            var trackcnt = -1;
            var trackno = -1;
            try
            {
                trackcnt = int.Parse(RenderLayoutHandler(_settings.PresenceTrackCnt));
                trackno = int.Parse(RenderLayoutHandler(_settings.PresenceTrackNo));
            }
            catch (Exception)
            {
                // ignored
            }

            _rpcClient.SetTrackNumbers(trackno, trackcnt);

            if (!_settings.UpdatePresenceWhenStopped && (playerGetPlayState == PlayState.Paused || playerGetPlayState == PlayState.Stopped))
            {
                Debug.WriteLine("Clearing Presence...", "DiscordBee");
                _rpcClient.Close();
                return;
            }
            _rpcClient.UpdatePresence();
        }

        #region Utilities
        private void SetImage(string name)
        {
            if (_settings.TextOnly)
            {
                _rpcClient.ClearAssets();
                return;
            }

            _rpcClient.SetLargeAssets(
                RenderLayoutHandler(_settings.LargeImageId),
                LargeImageTextHandler(_settings.DoNotDisplayInformation));
            _rpcClient.SetSmallAssets(
                name,
                RenderLayoutHandler(_settings.SmallImageText));
        }

        private string LargeImageTextHandler(bool doNotDisplayInformation)
        {
            if (doNotDisplayInformation)
            {
                return RenderLayoutHandler(_settings.LargeImageText);
            }

            return RenderLayoutHandler(
                "MusicBee: [Codec] / [AudioBitrate] / [AudioSampleRate] / [AudioChannels] / [FileSize] / [Duration]");
        }

        private string RenderLayoutHandler(string input)
        {
            var metadataDictionary = GenerateMetaDataDictionary();
            return _layoutHandler.Render(input, metadataDictionary, _settings.Seperator);
        }
        #endregion
    }
}
