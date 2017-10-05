using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace M3U3Downloader
{
    public partial class Form1 : Form
    {
        M3UData _data;

        public Form1()
        {
            InitializeComponent();
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

            _data = M3UData.ReadFromUrl(source);

            _data.Progress += _data_Progress;
            _data.Complete += _data_Complete;

            var sb = new StringBuilder();
            sb.AppendLine($"Segments: {_data.Segments.Count}");
            sb.AppendLine($"Duration: {_data.Duration}");
            sb.AppendLine($"Streams: {_data.Streams.Count}");
            var bestStream = _data.Streams.OrderByDescending(s => s.Resolution).FirstOrDefault();
            if (bestStream != null)
                sb.AppendLine($"Selected Stream: R={bestStream.Resolution}: Url={bestStream.Path}");
            this.textBoxMessage.Text = sb.ToString();

            this.progressBar.Minimum = 0;
            this.progressBar.Maximum = _data.Segments.Count;
            this.progressBar.Value = 0;
        }

        private void _data_Complete(string message)
        {
            MessageBox.Show(message);

            this.progressBar.Value = 0;
        }

        private void _data_Progress(int segment, int totalCount, string message)
        {
            this.progressBar.Value = segment > this.progressBar.Maximum ? this.progressBar.Maximum : segment;
            this.labelProgress.Text = message;
        }

        private void buttonExport_Click(object sender, EventArgs e)
        {
            if (_data == null)
                throw new ArgumentException("No Source to download!");

            if (String.IsNullOrWhiteSpace(this.textBoxDestination.Text))
                throw new ArgumentException("No Destination File Specified!");

            _data.Download(this.textBoxDestination.Text);
        }
    }
}
