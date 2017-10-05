using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace M3U3Downloader
{
    public delegate void M3UDataProgressHandler(int segment, int totalCount, string message);
    public delegate void M3UDataCompleteHandler(string message);

    public class M3UDataSegment
    {
        public string Url { get; set; }

        public M3UDataSegment(string url)
        {
            this.Url = url;
        }
    }

    public class M3UData
    {
        public event M3UDataProgressHandler Progress;
        public event M3UDataCompleteHandler Complete;

        public string Url { get; set; }
        public string BaseUrl { get; set; }
        public int Duration { get; set; } = 100;
        public List<M3UDataSegment> Segments { get; protected set; } = new List<M3UDataSegment>();
        public List<M3UStreamInfo> Streams { get; protected set; } = new List<M3UStreamInfo>();

        public M3UData() { }

        public M3UData(string url)
        {
            this.Url = url;
            this.BaseUrl = GetBaseUrl(url);
        }

        //protected void GetContent()
        //{
        //    var httpClient = new HttpClient();
        //    var data = httpClient.GetStringAsync(url).Result;

        //    var lines = data.Split('\n');
        //    if (!lines.Any())
        //        throw new InvalidOperationException("Empty M3U8 input");

        //    var firstLine = lines[0].Trim();
        //    if (firstLine != "#EXTM3U")
        //        throw new InvalidOperationException("The provided URL does not link to a well-formed M3U8 playlist.");

        //    for (var i = 1; i < lines.Length; i++)
        //    {
        //        var line = lines[i];
        //        if (!line.StartsWith("#"))
        //            continue;

        //        var lineData = line.Substring(1);

        //        var split = lineData.Split(':');

        //        var name = split[0];
        //        var value = split.Length >= 2 ? split[1] : "";

        //        switch (name)
        //        {
        //            case "EXT-X-TARGETDURATION":
        //                result.Duration = int.Parse(value);
        //                break;

        //            //oh, how sweet. a header for us to entirely ignore. we'll always use cache.
        //            case "EXT-X-ALLOW-CACHE":
        //                break;

        //            case "EXT-X-VERSION":
        //                break;

        //            case "EXT-X-MEDIA-SEQUENCE":
        //                break;

        //            case "EXTINF":
        //                result.Segments.Add(new M3UDataSegment(lines[i + 1]));
        //                break;
        //            case "EXT-X-STREAM-INF":
        //                var resolution = 0;
        //                Match match = Regex.Match(value, @"RESOLUTION=(?<resolution>\d+)");
        //                if (match.Success)
        //                {
        //                    resolution = Convert.ToInt32(match.Groups["resolution"].Value);
        //                }
        //                result.Streams.Add(new M3UStreamInfo(GetBaseUrl(url), lines[i + 1].Trim(), resolution));
        //                break;
        //        }
        //    }


        //}

        public void Download(string path, int maxCount = 0)
        {
            var httpClient = new HttpClient();
            using (var fs = File.Open(path, FileMode.Append))
            {

                for (int i = 0; i < this.Segments.Count; i++)
                {
                    if (maxCount > 0 && i >= maxCount)
                        break;

                    var segment = this.Segments[i];
                    var bytes = httpClient.GetByteArrayAsync(this.BaseUrl + segment.Url.Trim()).Result;
                    fs.Write(bytes, 0, bytes.Length);
                    this.Progress?.Invoke(i + 1, this.Segments.Count, segment.Url);
                }

                this.Complete?.Invoke("Downloading complete!");
                fs.Close();
            }
        }

        public class M3UStreamInfo
        {
            public string BaseUrl { get; set; }
            public string Path { get; set; }
            public int Resolution { get;set; } = 0;

            public string GetUrl()
            {
                if (this.Path.StartsWith("http"))
                    return this.Path;
                return this.BaseUrl + this.Path;
            }

            public M3UStreamInfo() { }

            public M3UStreamInfo(string baseUrl, string path, int resolution = 0)
            {
                this.BaseUrl = baseUrl;
                this.Path = path;
                this.Resolution = resolution;
            }
        }

        static public string GetBaseUrl(string url)
        {
            return url.Substring(0, url.LastIndexOf("/")) + "/";
        }

        static public M3UData ReadFromStream(M3UStreamInfo stream)
        {
            var url = stream.GetUrl();
            var result = ReadFromUrl(url);
            result.BaseUrl = GetBaseUrl(url);
            return result;
        }

        static public M3UData ReadFromUrl(string url)
        {
            var result = new M3UData(url);
            //if (url.EndsWith(".m3u8") || url.EndsWith(".m3u"))
            //    result.BaseUrl = GetBaseUrl(url);
            //else
            //    result.BaseUrl = url;

            var httpClient = new HttpClient();
            var data = httpClient.GetStringAsync(url).Result;

            var lines = data.Split('\n');
            if (!lines.Any())
                throw new InvalidOperationException("Empty M3U8 input");

            var firstLine = lines[0].Trim();
            if (firstLine != "#EXTM3U")
                throw new InvalidOperationException("The provided URL does not link to a well-formed M3U8 playlist.");

            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!line.StartsWith("#"))
                    continue;

                var lineData = line.Substring(1);

                var split = lineData.Split(':');

                var name = split[0];
                var value = split.Length >= 2 ? split[1] : "";

                switch (name)
                {
                    case "EXT-X-TARGETDURATION":
                        result.Duration = int.Parse(value);
                        break;

                    //oh, how sweet. a header for us to entirely ignore. we'll always use cache.
                    case "EXT-X-ALLOW-CACHE":
                        break;

                    case "EXT-X-VERSION":
                        break;

                    case "EXT-X-MEDIA-SEQUENCE":
                        break;

                    case "EXTINF":
                        result.Segments.Add(new M3UDataSegment(lines[i + 1]));
                        break;
                    case "EXT-X-STREAM-INF":
                        var resolution = 0;
                        Match match = Regex.Match(value, @"RESOLUTION=(?<resolution>\d+)");
                        if (match.Success)
                        {
                            resolution = Convert.ToInt32(match.Groups["resolution"].Value);
                        }
                        result.Streams.Add(new M3UStreamInfo(GetBaseUrl(url), lines[i + 1].Trim(), resolution));
                        break;
                }
            }

            if (result.Streams.Count > 0 && result.Segments.Count <=0)
            {
                var bestStream = result.Streams.OrderByDescending(s => s.Resolution).FirstOrDefault();
                if (bestStream != null)
                {
                    var best = ReadFromStream(bestStream);
                    best.Streams = result.Streams;
                    return best;
                }
            }

            return result;
        }
    }
}
