using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;

namespace M3UDownloader.Classes
{
    public class M3UStream : M3UContent
    {
        public event M3UStreamProgressHandler Progress;
        public event M3UStreamCompleteHandler Complete;

        public override M3UType Type { get { return M3UType.Block; } }

        public int Duration { get; set; } = 0;
        public int Resolution { get; set; } = 0;
        public bool IsCompleted { get; set; } = false;

        public List<M3UBlock> Blocks { get; protected set; } = new List<M3UBlock>();

        public M3UStream(string url, string content = null)
            : base(url, content) { }

        protected override void Parse(string content)
        {
            var lines = ParseLines(content);

            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (!line.StartsWith("#"))
                    continue;

                var split = line.Substring(1).Split(':');

                var name = split[0];
                var value = split.Length >= 2 ? split[1] : "";

                switch (name)
                {
                    case "EXT-X-TARGETDURATION":
                        this.Duration = Int32.Parse(value);
                        break;

                    case "EXT-X-ALLOW-CACHE":
                        break;

                    case "EXT-X-VERSION":
                        break;

                    case "EXT-X-MEDIA-SEQUENCE":
                        break;

                    case "EXT-X-ENDLIST":
                        this.IsCompleted = true;
                        break;

                    case "EXTINF":
                        var block = new M3UBlock(this.Url + lines[i + 1].Trim());
                        this.Blocks.Add(block);
                        break;
                }
            }
        }

        public override void Download(Stream stream)
        {
            using (var http = new HttpClient())
            {
                for (int i = 0; i < this.Blocks.Count; i++)
                {
                    var segment = this.Blocks[i];
                    var bytes = http.GetByteArrayAsync(segment.Url).Result;
                    stream.Write(bytes, 0, bytes.Length);
                    this.Progress?.Invoke(i + 1, this.Blocks.Count, segment.Url);
                }

                this.Complete?.Invoke("Downloading complete!");
            }
        }

        public void Download(Stream stream, CancellationToken cancelationToken)
        {
            using (var http = new HttpClient())
            {
                for (int i = 0; i < this.Blocks.Count; i++)
                {
                    var segment = this.Blocks[i];
                    var bytes = http.GetByteArrayAsync(segment.Url).Result;
                    stream.Write(bytes, 0, bytes.Length);
                    this.Progress?.Invoke(i + 1, this.Blocks.Count, segment.Url);

                    if (cancelationToken.IsCancellationRequested)
                    {
                        this.Complete?.Invoke("Downloading canceled by user!");
                        cancelationToken.ThrowIfCancellationRequested();
                    }
                }

                this.Complete?.Invoke("Downloading complete!");
            }
        }

        public void Download(string path, CancellationToken cancelationToken)
        {
            using (var fs = File.Open(path, FileMode.Append))
            {
                this.Download(fs, cancelationToken);
                fs.Close();
            }
        }

        static public M3UStream Create(string url, string content)
        {
            var result = new M3UStream(url);
            result.Parse(content);
            return result;
        }
    }
}
