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
        public bool IsDirty { get; private set; } = false;

        // Don't serialize properties so only user set changes are serialized and not default values

        #region Settings

        [DataMember] private string _seperator;

        public string Seperator
        {
            get => _seperator == null ? "./-_" : _seperator;
            set => setIfChanged("_seperator", value);
        }

        [DataMember] private string _smallImageText;

        public string SmallImageText
        {
            get => _smallImageText == null ? "[Volume]%" : _smallImageText;
            set => setIfChanged("_smallImageText", value);
        }

        [DataMember] private string _presenceState;

        public string PresenceState
        {
            get => _presenceState == null ? "[TrackTitle]" : _presenceState;
            set => setIfChanged("_presenceState", value);
        }

        [DataMember] private string _presenceDetails;

        public string PresenceDetails
        {
            get => _presenceDetails == null ? "[Artist] - [Album]" : _presenceDetails;
            set => setIfChanged("_presenceDetails", value);
        }

        [DataMember] private string _presenceTrackCnt;

        public string PresenceTrackCnt
        {
            get => _presenceTrackCnt == null ? "[TrackCount]" : _presenceTrackCnt;
            set => setIfChanged("_presenceTrackCnt", value);
        }

        [DataMember] private string _presenceTrackNo;

        public string PresenceTrackNo
        {
            get => _presenceTrackNo == null ? "[TrackNo]" : _presenceTrackNo;
            set => setIfChanged("_presenceTrackNo", value);
        }

        [DataMember] private bool? _updatePresenceWhenStopped;

        public bool UpdatePresenceWhenStopped
        {
            get => !_updatePresenceWhenStopped.HasValue || _updatePresenceWhenStopped.Value;
            set => setIfChanged("_updatePresenceWhenStopped", value);
        }

        [DataMember] private bool? _showRemainingTime;

        public bool ShowRemainingTime
        {
            get => _showRemainingTime.HasValue && _showRemainingTime.Value;
            set => setIfChanged("_showRemainingTime", value);
        }

        [DataMember] private bool? _textOnly;

        public bool TextOnly
        {
            get => _textOnly.HasValue && _textOnly.Value;
            set => setIfChanged("_textOnly", value);
        }

        #region Custom Stuff
        [DataMember] private string _clientId;

        public string ClientId
        {
            get => _clientId == null ? "409394531948298250" : _clientId;
            set => setIfChanged("_clientId", value);
        }

        [DataMember] private string _largeImageId;

        public string LargeImageId
        {
            get => _largeImageId == null ? "logo" : _largeImageId;
            set => setIfChanged("_largeImageId", value);
        }

        [DataMember] private string _playingImage;

        public string PlayingImage
        {
            get => _playingImage == null ? "play" : _playingImage;
            set => setIfChanged("_playingImage", value);
        }

        [DataMember] private string _pausedImage;

        public string PausedImage
        {
            get => _pausedImage == null ? "pause" : _pausedImage;
            set => setIfChanged("_pausedImage", value);
        }

        [DataMember] private string _stoppedImage;

        public string StoppedImage
        {
            get => _stoppedImage == null ? "stop" : _stoppedImage;
            set => setIfChanged("_stoppedImage", value);
        }

        [DataMember] private bool? _doNotDisplayInformation;

        public bool DoNotDisplayInformation
        {
            get => _doNotDisplayInformation.HasValue && _doNotDisplayInformation.Value;
            set => setIfChanged("_doNotDisplayInformation", value);
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

        private void setIfChanged<T>(string fieldName, T value)
        {
            FieldInfo target = GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (target != null)
            {
                PropertyInfo targetProp = GetType().GetProperty(getPropertyNameForField(target.Name), BindingFlags.Instance | BindingFlags.Public);
                if (targetProp != null)
                {
                    if (!targetProp.GetValue(this, null).Equals(value))
                    {
                        target.SetValue(this, value);
                        IsDirty = true;
                    }
                }
            }
        }

        private string getPropertyNameForField(string field)
        {
            if (field.StartsWith("_"))
            {
                string tmp = field.Remove(0, 1);
                return tmp.First().ToString().ToUpper() + tmp.Substring(1);
            }
            return null;
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