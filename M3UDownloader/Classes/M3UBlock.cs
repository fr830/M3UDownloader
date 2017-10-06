using System.IO;
using System.Net.Http;

namespace M3UDownloader.Classes
{
    public class M3UBlock : M3U
    {
        public override M3UType Type { get { return M3UType.Block; } }

        public M3UBlock(string url)
            : base(url) { }

        public override void Download(Stream stream)
        {
            using (var http = new HttpClient())
            {
                var bytes = http.GetByteArrayAsync(this.Url).Result;
                stream.Write(bytes, 0, bytes.Length);
            }
        }
    }
}
