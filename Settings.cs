using System;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Runtime.Serialization;
using System.Linq;

namespace MusicBeePlugin
{
    [DataContract]
    public class Settings
    {
        private string FilePath { get; set; }
        public bool IsDirty { get; private set; }

        // Don't serialize properties so only user set changes are serialized and not default values

        #region Settings

        [DataMember] private string _seperator;

        public string Seperator
        {
            get => _seperator ?? "./-_";
            set => SetIfChanged("_seperator", value);
        }

        [DataMember] private string _smallImageText;

        public string SmallImageText
        {
            get => _smallImageText ?? "[Volume]%";
            set => SetIfChanged("_smallImageText", value);
        }

        [DataMember] private string _presenceState;

        public string PresenceState
        {
            get => _presenceState ?? "[TrackTitle]";
            set => SetIfChanged("_presenceState", value);
        }

        [DataMember] private string _presenceDetails;

        public string PresenceDetails
        {
            get => _presenceDetails ?? "[Artist] - [Album]";
            set => SetIfChanged("_presenceDetails", value);
        }

        [DataMember] private string _presenceTrackCnt;

        public string PresenceTrackCnt
        {
            get => _presenceTrackCnt ?? "[TrackCount]";
            set => SetIfChanged("_presenceTrackCnt", value);
        }

        [DataMember] private string _presenceTrackNo;

        public string PresenceTrackNo
        {
            get => _presenceTrackNo ?? "[TrackNo]";
            set => SetIfChanged("_presenceTrackNo", value);
        }

        [DataMember] private bool? _updatePresenceWhenStopped;

        public bool UpdatePresenceWhenStopped
        {
            get => !_updatePresenceWhenStopped.HasValue || _updatePresenceWhenStopped.Value;
            set => SetIfChanged("_updatePresenceWhenStopped", value);
        }

        [DataMember] private bool? _showRemainingTime;

        public bool ShowRemainingTime
        {
            get => _showRemainingTime.HasValue && _showRemainingTime.Value;
            set => SetIfChanged("_showRemainingTime", value);
        }

        [DataMember] private bool? _textOnly;

        public bool TextOnly
        {
            get => _textOnly.HasValue && _textOnly.Value;
            set => SetIfChanged("_textOnly", value);
        }

        #region Custom Stuff
        [DataMember] private string _clientId;

        public string ClientId
        {
            get => _clientId ?? "409394531948298250";
            set => SetIfChanged("_clientId", value);
        }

        [DataMember] private string _largeImageId;

        public string LargeImageId
        {
            get => _largeImageId ?? "logo";
            set => SetIfChanged("_largeImageId", value);
        }

        [DataMember] private string _playingImage;

        public string PlayingImage
        {
            get => _playingImage ?? "play";
            set => SetIfChanged("_playingImage", value);
        }

        [DataMember] private string _pausedImage;

        public string PausedImage
        {
            get => _pausedImage ?? "pause";
            set => SetIfChanged("_pausedImage", value);
        }

        [DataMember] private string _stoppedImage;

        public string StoppedImage
        {
            get => _stoppedImage ?? "stop";
            set => SetIfChanged("_stoppedImage", value);
        }

        [DataMember] private bool? _doNotDisplayInformation;

        public bool DoNotDisplayInformation
        {
            get => _doNotDisplayInformation.HasValue && _doNotDisplayInformation.Value;
            set => SetIfChanged("_doNotDisplayInformation", value);
        }
        #endregion

        #endregion

        public static Settings GetInstance(string filePath)
        {
            Settings newSettings;

            try
            {
                newSettings = Load(filePath);
            }
            catch (Exception e) when (e is IOException || e is XmlException || e is InvalidOperationException)
            {
                newSettings = new Settings();
            }

            newSettings.FilePath = filePath;

            return newSettings;
        }

        private void SetIfChanged<T>(string fieldName, T value)
        {
            var target = GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (target == null) return;
            var targetProp = GetType().GetProperty(getPropertyNameForField(target.Name), BindingFlags.Instance | BindingFlags.Public);
            if (targetProp == null) return;
            if (targetProp.GetValue(this, null).Equals(value)) return;
            target.SetValue(this, value);
            IsDirty = true;
        }

        private string getPropertyNameForField(string field)
        {
            if (!field.StartsWith("_")) return null;
            var tmp = field.Remove(0, 1);
            return tmp.First().ToString().ToUpper() + tmp.Substring(1);
        }

        public void Save()
        {
            if (!IsDirty) return;
            if (Path.GetDirectoryName(FilePath) != null && !Directory.Exists(Path.GetDirectoryName(FilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath) ?? throw new InvalidOperationException());
            }

            using (var writer = XmlWriter.Create(FilePath))
            {
                var serializer = new DataContractSerializer(GetType());
                serializer.WriteObject(writer, this);
                writer.Flush();
            }
        }

        private static Settings Load(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            {
                var serializer = new DataContractSerializer(typeof(Settings));
                return serializer.ReadObject(stream) as Settings;
            }
        }

        public void Delete()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }

            if (Path.GetDirectoryName(FilePath) != null && Directory.Exists(Path.GetDirectoryName(FilePath)))
            {
                Directory.Delete(Path.GetDirectoryName(FilePath) ?? throw new InvalidOperationException());
            }

            Clear();
        }

        public void Clear()
        {
            var properties = GetType().GetProperties();

            foreach (var propertyInfo in properties)
            {
                if (propertyInfo.PropertyType == typeof(string) && propertyInfo.Name != "FilePath")
                {
                    propertyInfo.SetValue(this, null, null);
                }
            }

            // field is used for boolean settings because nullable is used internally and property would be non-nullable
            var fields = GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var fieldInfo in fields)
            {
                if (!fieldInfo.Name.StartsWith("_")) continue;
                if (fieldInfo.FieldType == typeof(bool?))
                {
                    fieldInfo.SetValue(this, null);
                }
            }

            IsDirty = false;
        }
    }
}
