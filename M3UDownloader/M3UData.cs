using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace M3UDownloader
{
    public delegate void M3UStreamProgressHandler(int segment, int totalCount, string message);
    public delegate void M3UStreamCompleteHandler(string message);

    public enum M3UType { Playlist, Stream, Segment };

    public abstract class M3U
    {
        public abstract M3UType Type { get; }

        public string Url { get; protected set; }
        public string BaseUrl { get { return GetBaseUrl(this.Url); } }

        protected M3U(string url)
        {
            this.Url = url.Trim();
        }

        protected abstract void Parse(string content);

        public abstract void Download(Stream stream);

        public virtual void Reload()
        {
            using (var http = new HttpClient())
            {
                var content = http.GetStringAsync(this.Url).Result;
                this.Parse(content);
            }
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

        static public string GetBaseUrl(string url)
        {
            if (String.IsNullOrWhiteSpace(url))
                return url;

            var result = url.Substring(0, url.LastIndexOf("/")) + "/";

            if (!result.StartsWith("http://") && !result.StartsWith("https://"))
                result = "http://" + result;

            return result.Trim();
        }

        static public string BuildUrl(string baseUrl, string url)
        {
            if (url == null)
                throw new ArgumentException("URL is not provided!");

            var result = url.Trim();

            // adding baseUrl
            if (!result.StartsWith("http://") && !result.StartsWith("https://"))
                result = baseUrl + url;

            // adding http prefix
            if (!result.StartsWith("http://") && !result.StartsWith("https://"))
                result = "http://" + result;

            return result.Trim();
        }

        static public M3U CreateFromUrl(string url)
        {
            using (var http = new HttpClient())
            {
                var content = http.GetStringAsync(url.Trim()).Result;

                var lines = ParseLines(content);

                if (lines.Any(l => l.StartsWith("#EXTINF:")))
                    return M3UStream.Create(url, content);
                else
                    return M3UPlaylist.Create(url, content);
            }
        }
    }

    public class M3USegment : M3U
    {
        public override M3UType Type { get { return M3UType.Segment; } }

        public M3USegment(string url)
            : base(url) { }

        protected override void Parse(string content)
        {
            throw new NotImplementedException();
        }

        public override void Download(Stream stream)
        {
            using (var http = new HttpClient())
            {
                var bytes = http.GetByteArrayAsync(this.Url).Result;
                stream.Write(bytes, 0, bytes.Length);
            }
        }
    }

    public class M3UStream : M3U
    {
        public event M3UStreamProgressHandler Progress;
        public event M3UStreamCompleteHandler Complete;

        public override M3UType Type { get { return M3UType.Segment; } }

        public int Duration { get; set; } = 0;
        public int Resolution { get; set; } = 0;

        public List<M3USegment> Segments { get; protected set; } = new List<M3USegment>();

        public M3UStream(string url, int resolution = 0)
            : base(url)
        {
            this.Resolution = resolution;
        }

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

                    case "EXTINF":
                        var segmentUrl = lines[i + 1];
                        var url = BuildUrl(this.BaseUrl, segmentUrl);
                        this.Segments.Add(new M3USegment(url));
                        break;
                }
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

        public override void Download(Stream stream)
        {
            using (var http = new HttpClient())
            {
                for (int i = 0; i < this.Segments.Count; i++)
                {
                    var segment = this.Segments[i];
                    var bytes = http.GetByteArrayAsync(segment.Url).Result;
                    stream.Write(bytes, 0, bytes.Length);
                    this.Progress?.Invoke(i + 1, this.Segments.Count, segment.Url);
                }

                this.Complete?.Invoke("Downloading complete!");
            }
        }

        public void Download(Stream stream, CancellationToken cancelationToken)
        {
            using (var http = new HttpClient())
            {
                for (int i = 0; i < this.Segments.Count; i++)
                {
                    var segment = this.Segments[i];
                    var bytes = http.GetByteArrayAsync(segment.Url).Result;
                    stream.Write(bytes, 0, bytes.Length);
                    this.Progress?.Invoke(i + 1, this.Segments.Count, segment.Url);

                    if (cancelationToken.IsCancellationRequested)
                    {
                        this.Complete?.Invoke("Downloading canceled by user!");
                        cancelationToken.ThrowIfCancellationRequested();
                    }
                }

                this.Complete?.Invoke("Downloading complete!");
            }
        }

        static public M3UStream Create(string url, string content)
        {
            var result = new M3UStream(url);
            result.Parse(content);
            return result;
        }
    }

    public class M3UPlaylist : M3U
    {
        public override M3UType Type { get { return M3UType.Playlist; } }

        public int Duration { get; set; } = 100;

        public List<M3UStream> Streams { get; protected set; } = new List<M3UStream>();

        public M3UPlaylist(string url)
            : base(url) { }

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
                        var streamUrl = lines[i + 1].Trim();
                        var url = BuildUrl(this.BaseUrl, streamUrl);
                        this.Streams.Add(new M3UStream(url, resolution));
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

        static public M3UPlaylist Create(string url, string content)
        {
            var result = new M3UPlaylist(url);
            result.Parse(content);
            return result;
        }
    }

    //public class M3UData
    //{
    //    public enum M3UType { None, Manifest, Stream };

    //    public event M3UStreamProgressHandler Progress;
    //    public event M3UStreamCompleteHandler Complete;

    //    public M3UType Type
    //    {
    //        get
    //        {
    //            if (this.Streams.Count > 0)
    //                return M3UType.Manifest;

    //            if (this.Segments.Count > 0)
    //                return M3UType.Stream;

    //            return M3UType.None;
    //        }
    //    }
    //    public string Url { get; set; }
    //    public string BaseUrl { get { return GetBaseUrl(this.Url); } }
    //    public int Duration { get; set; } = 100;
    //    public List<M3USegment> Segments { get; protected set; } = new List<M3USegment>();
    //    public List<M3UStreamInfo> Streams { get; protected set; } = new List<M3UStreamInfo>();

    //    protected M3UData(string url)
    //    {
    //        this.Url = url;
    //    }

    //    protected void GetContentFromUrl()
    //    {
    //        using (var http = new HttpClient())
    //        {
    //            var content = http.GetStringAsync(this.Url).Result;

    //            var lines = content.Split('\n');
    //            if (!lines.Any())
    //                throw new InvalidOperationException("Empty M3U8 input");

    //            var firstLine = lines[0].Trim();
    //            if (firstLine != "#EXTM3U")
    //                throw new InvalidOperationException("The provided URL does not link to a well-formed M3U8 playlist.");

    //            for (var i = 1; i < lines.Length; i++)
    //            {
    //                var line = lines[i].Trim();
    //                if (!line.StartsWith("#"))
    //                    continue;

    //                var split = line.Substring(1).Split(':');

    //                var name = split[0];
    //                var value = split.Length >= 2 ? split[1] : "";

    //                switch (name)
    //                {
    //                    case "EXT-X-TARGETDURATION":
    //                        this.Duration = Int32.Parse(value);
    //                        break;

    //                    //oh, how sweet. a header for us to entirely ignore. we'll always use cache.
    //                    case "EXT-X-ALLOW-CACHE":
    //                        break;

    //                    case "EXT-X-VERSION":
    //                        break;

    //                    case "EXT-X-MEDIA-SEQUENCE":
    //                        break;

    //                    case "EXTINF":
    //                        this.Segments.Add(new M3USegment(lines[i + 1]));
    //                        break;

    //                    case "EXT-X-STREAM-INF":
    //                        var resolution = 0;
    //                        Match match = Regex.Match(value, @"RESOLUTION=(?<resolution>\d+)");
    //                        if (match.Success)
    //                            resolution = Convert.ToInt32(match.Groups["resolution"].Value);
    //                        this.Streams.Add(new M3UStreamInfo(this.BaseUrl, lines[i + 1].Trim(), resolution));
    //                        break;
    //                }
    //            }
    //        }
    //    }

    //    protected M3UStreamInfo SelectBestStream()
    //    {
    //        if (this.Segments.Count <= 0)
    //            throw new InvalidOperationException("Не задано ни одного потока для выбора наилучшего!");

    //        return this.Streams.OrderByDescending(s => s.Resolution).FirstOrDefault();
    //    }

    //    public void Download(string path, int maxCount = 0)
    //    {
    //        var httpClient = new HttpClient();
    //        using (var fs = File.Open(path, FileMode.Append))
    //        {

    //            for (int i = 0; i < this.Segments.Count; i++)
    //            {
    //                if (maxCount > 0 && i >= maxCount)
    //                    break;

    //                var segment = this.Segments[i];
    //                var bytes = httpClient.GetByteArrayAsync(this.BaseUrl + segment.Url.Trim()).Result;
    //                fs.Write(bytes, 0, bytes.Length);
    //                this.Progress?.Invoke(i + 1, this.Segments.Count, segment.Url);
    //            }

    //            this.Complete?.Invoke("Downloading complete!");
    //            fs.Close();
    //        }
    //    }



    //    static public string GetBaseUrl(string url)
    //    {
    //        return url.Substring(0, url.LastIndexOf("/")) + "/";
    //    }

    //    static public M3UData ReadFromStream(M3UStreamInfo stream)
    //    {
    //        var url = stream.GetUrl();
    //        var result = ReadFromUrl(url);
    //        result.BaseUrl = GetBaseUrl(url);
    //        return result;
    //    }

    //    static public M3UData ReadFromUrl(string url)
    //    {
    //        var result = new M3UData(url);
    //        result.GetContentFromUrl();
    //        return result;
    //    }

    //    static public M3UData ReadBestStreamFromUrl(string url)
    //    {
    //        var result = ReadFromUrl(url);

    //        if (result.Type == M3UType.Stream)
    //        {
    //            return result;
    //        }

    //        if (result.Type == M3UType.Manifest)
    //        {
    //            var bestStream = result.SelectBestStream();
    //            if (bestStream == null)
    //                throw new InvalidOperationException("Не найдено ни одного потока в манифесте!");

    //            return ReadFromStream(bestStream);
    //        }

    //        throw new InvalidOperationException("Не распознано содержимое указанного url!");
    //    }
    //}
}
