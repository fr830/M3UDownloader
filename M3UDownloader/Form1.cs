using M3UDownloader.Classes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace M3UDownloader
{
    public partial class Form1 : Form
    {
        CancellationTokenSource _cancelationTokenSource = new CancellationTokenSource();
        M3UStream _stream = null;

        public Form1()
        {
            InitializeComponent();
        }

        protected void SetStream(M3UStream stream)
        {
            if (stream == null)
                throw new ArgumentException("M3U8 stream is not provided!");

            _stream = stream;
            if (_stream.Blocks.Count <= 0)
                _stream.Reload();

            _stream.Progress += _download_Progress;
            _stream.Complete += _download_Complete;

            this.labelProgress.Text = $"{_stream.Blocks.Count} segments would be downloaded!";

            this.progressBar.Minimum = 0;
            this.progressBar.Maximum = _stream.Blocks.Count;
            this.progressBar.Value = 0;

            this.buttonDownload.Enabled = _stream.Blocks.Count > 0;
        }

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            var sfd = new SaveFileDialog();
            if (sfd.ShowDialog() != DialogResult.OK)
                return;

            this.textBoxDestination.Text = sfd.FileName;
        }

        private void buttonGetData_Click(object sender, EventArgs e)
        {
            var source = this.textBoxSource.Text;
            if (!source.StartsWith("http://") && !source.StartsWith("https://"))
                source = "http://" + source;

            var m3u = M3UContent.CreateFromUrl(source);

            if (m3u is M3UManifest)
            {
                var playlist = m3u as M3UManifest;

                var sb = new StringBuilder();
                sb.AppendLine("PLAYLIST:");
                sb.AppendLine($"Duration: {playlist.Duration}");
                sb.AppendLine($"Streams Count: {playlist.Streams.Count}");
                foreach (var st in playlist.Streams)
                    sb.AppendLine($"Stream: resolution={st.Resolution}, url={st.Url}");

                var stream = playlist.GetBestStream();
                if (stream != null)
                {
                    sb.AppendLine($"Selected Stream: resolution={stream.Resolution}, url={stream.Url}");
                    this.SetStream(stream);
                }
                else
                    sb.AppendLine("WARNING: no stream found!");

                this.textBoxMessage.Text = sb.ToString();
            }
            else if (m3u is M3UStream)
            {
                var stream = m3u as M3UStream;

                var sb = new StringBuilder();
                sb.AppendLine("STREAM:");
                sb.AppendLine($"Duration: {stream.Duration}");
                sb.AppendLine($"Resolution: {stream.Resolution}");
                sb.AppendLine($"Url: {stream.Url}");
                sb.AppendLine($"Segments Count: {stream.Blocks.Count}");

                this.SetStream(stream);

                this.textBoxMessage.Text = sb.ToString();
            }
        }

        private void buttonDownload_Click(object sender, EventArgs e)
        {
            if (_stream == null)
                throw new ArgumentException("No stream selected to download!");

            if (String.IsNullOrWhiteSpace(this.textBoxDestination.Text))
                throw new ArgumentException("Destination file is not specified!");

            var cancelationToken = _cancelationTokenSource.Token;
            var task = Task.Run(() =>
            {
                _stream.Download(this.textBoxDestination.Text, cancelationToken);
            }, cancelationToken);

            this.buttonCancel.Enabled = true;
            this.buttonDownload.Enabled = false;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            _cancelationTokenSource.Cancel(false);
        }

        private void _download_Complete(string message)
        {
            this.buttonCancel.Enabled = false;
            this.buttonDownload.Enabled = true;
            this.labelProgress.Text = message;

            MessageBox.Show(message);

            this.progressBar.Value = 0;
        }

        private void _download_Progress(int segment, int totalCount, string message)
        {
            this.progressBar.Value = segment > this.progressBar.Maximum ? this.progressBar.Maximum : segment;
            this.labelProgress.Text = $"[{segment}/{totalCount}]: {message}";
        }

      
    }
}
