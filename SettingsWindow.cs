using System;
using System.Windows.Forms;

namespace MusicBeePlugin
{
    public partial class SettingsWindow : Form
    {
        private readonly Plugin _parent;
        private PlaceholderTableWindow _placeholderTableWindow;
        private readonly Settings _settings;
        private bool _defaultsRestored;

        public SettingsWindow(Plugin parent, Settings settings)
        {
            _parent = parent;
            _settings = settings;
            InitializeComponent();
            UpdateValues(_settings);
            Text += " (v" + parent.GetVersionString() + ")";

            FormClosing += OnFormClosing;
            Shown += OnShown;
            VisibleChanged += OnVisibleChanged;
        }

        private void OnVisibleChanged(object sender, EventArgs eventArgs)
        {
            if (Visible)
            {
                UpdateValues(_settings);
            }
        }

        private void OnShown(object sender, EventArgs eventArgs)
        {
            UpdateValues(_settings);
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing) return;
            Hide();
            e.Cancel = true;
        }

        private void UpdateValues(Settings settings)
        {
            textBoxTrackNo.Text = settings.PresenceTrackNo;
            textBoxTrackCnt.Text = settings.PresenceTrackCnt;
            textBoxDetails.Text = settings.PresenceDetails;
            textBoxState.Text = settings.PresenceState;
            textBoxSmallImage.Text = settings.SmallImageText;
            textBoxSeperator.Text = settings.Seperator;
            checkBoxPresenceUpdate.Checked = settings.UpdatePresenceWhenStopped;
            checkBoxShowRemainingTime.Checked = settings.ShowRemainingTime;
            checkBoxTextOnly.Checked = settings.TextOnly;
            clientId.Text = settings.ClientId;
            largeImageId.Text = settings.LargeImageId;
            playingImageId.Text = settings.PlayingImage;
            pausedImageId.Text = settings.PausedImage;
            stoppedImage.Text = settings.StoppedImage;
            displayFileInfoCheckbox.Checked = settings.DoNotDisplayInformation;
        }

        private void buttonPlaceholders_Click(object sender, EventArgs e)
        {
            _placeholderTableWindow = new PlaceholderTableWindow();
            _placeholderTableWindow.UpdateTable(_parent.GenerateMetaDataDictionary());
            _placeholderTableWindow.Show(this);
        }

        private void buttonRestoreDefaults_Click(object sender, EventArgs e)
        {
            _settings.Clear();
            UpdateValues(_settings);
            _defaultsRestored = true;
        }

        private void buttonSaveClose_Click(object sender, EventArgs e)
        {
            _settings.PresenceTrackNo = textBoxTrackNo.Text;
            _settings.PresenceTrackCnt = textBoxTrackCnt.Text;
            _settings.PresenceDetails = textBoxDetails.Text;
            _settings.PresenceState = textBoxState.Text;
            _settings.SmallImageText = textBoxSmallImage.Text;
            _settings.Seperator = textBoxSeperator.Text;
            _settings.UpdatePresenceWhenStopped = checkBoxPresenceUpdate.Checked;
            _settings.ShowRemainingTime = checkBoxShowRemainingTime.Checked;
            _settings.TextOnly = checkBoxTextOnly.Checked;
            _settings.ClientId = clientId.Text;
            _settings.LargeImageId = largeImageId.Text;
            _settings.PlayingImage = playingImageId.Text;
            _settings.PausedImage = pausedImageId.Text;
            _settings.StoppedImage = stoppedImage.Text;
            _settings.DoNotDisplayInformation = displayFileInfoCheckbox.Checked;

            if (_defaultsRestored && !_settings.IsDirty)
            {
                _settings.Delete();
                _defaultsRestored = false;
            }

            _settings.Save();
            Hide();
        }

    }
}