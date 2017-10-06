using System;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace M3UDownloader.Classes
{
    public delegate void M3UStreamProgressHandler(int index, int totalCount, string message);
    public delegate void M3UStreamCompleteHandler(string message);

    public enum M3UType { Manifest, Stream, Block };

    /// <summary>
    /// M3U Element
    /// </summary>
    public abstract class M3U
    {
        public abstract M3UType Type { get; }
        public UrlAddress Url { get; protected set; }

        protected M3U(string url)
        {
            this.Url = new UrlAddress(url);
        }

        public abstract void Download(Stream stream);
    }

    /// <summary>
    /// M3U Content Element (Manifest, Playlist, Stream, ...)
    /// </summary>
    public abstract class M3UContent : M3U
    {
        protected abstract void Parse(string content);

        public virtual void Reload()
        {
            using (var http = new HttpClient())
            {
                var content = http.GetStringAsync(this.Url).Result;
                this.Parse(content);
            }
        }

        protected M3UContent(string url, string content = null)
            : base(url)
        {
            if (content != null)
                this.Parse(content);
        }

        static protected string[] ParseLines(string content)
        {
            if (String.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Empty M3U8 content!");

            var lines = content.Split('\n');
            if (!lines.Any())
                throw new InvalidOperationException("Empty M3U8 content!");

            var firstLine = lines[0].Trim();
            if (firstLine != "#EXTM3U")
                throw new InvalidOperationException("The provided content is not a well-formed M3U8 content!");

            return lines;
        }

        static public M3UContent CreateFromUrl(string url)
        {
            using (var http = new HttpClient())
            {
                var content = http.GetStringAsync(url.Trim()).Result;

                var lines = ParseLines(content);

                if (lines.Any(l => l.StartsWith("#EXTINF:")))
                    return M3UStream.Create(url, content);
                else
                    return M3UManifest.Create(url, content);
            }
        }
    }
}
