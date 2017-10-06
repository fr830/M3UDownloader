using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace M3UDownloader.Classes
{
    public class M3UManifest : M3UContent
    {
        public override M3UType Type { get { return M3UType.Manifest; } }

        public int Duration { get; set; } = 100;

        public List<M3UStream> Streams { get; protected set; } = new List<M3UStream>();

        public M3UManifest(string url, string content = null)
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

                    //oh, how sweet. a header for us to entirely ignore. we'll always use cache.
                    case "EXT-X-ALLOW-CACHE":
                        break;

                    case "EXT-X-VERSION":
                        break;

                    case "EXT-X-MEDIA-SEQUENCE":
                        break;

                    case "EXT-X-STREAM-INF":
                        var resolution = 0;
                        Match match = Regex.Match(value, @"RESOLUTION=(?<resolution>\d+)");
                        if (match.Success)
                            resolution = Convert.ToInt32(match.Groups["resolution"].Value);

                        var stream = new M3UStream(this.Url + lines[i + 1].Trim())
                        {
                            Resolution = resolution
                        };

                        this.Streams.Add(stream);

                        break;
                }
            }
        }

        public M3UStream GetBestStream()
        {
            return this.Streams?.OrderByDescending(s => s.Resolution).FirstOrDefault();
        }

        public override void Download(Stream stream)
        {
            var best = this.GetBestStream();
            if (best == null)
                throw new InvalidOperationException("No valid streams in M3U8 playlist!");

            best.Download(stream);
        }

        static public M3UManifest Create(string url, string content)
        {
            var result = new M3UManifest(url);
            result.Parse(content);
            return result;
        }
    }
}
