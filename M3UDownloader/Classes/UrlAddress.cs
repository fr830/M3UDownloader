using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace M3UDownloader.Classes
{
    public class UrlAddress
    {
        public string Url { get; protected set; }

        public UrlAddress(string url)
        {
            if (String.IsNullOrWhiteSpace(url))
                throw new ArgumentException("Empty Url Address provided!");

            this.Url = url.Trim();

            if (HasHttpPrefix(this.Url) == false)
                this.Url = "http://" + this.Url;
        }

        public override string ToString()
        {
            return this.Url;
        }

        public UrlAddress Join(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
                return new UrlAddress(this.Url);

            if (HasHttpPrefix(path))
                return new UrlAddress(path);

            var result = this.GetBaseAddress();

            while (path.StartsWith("../") || path.StartsWith("/"))
            {
                if (path.StartsWith("../"))
                {
                    result = result.GetPreviousAddress().GetBaseAddress();
                    path = path.Substring(3);
                }
                else if (path.StartsWith("/"))
                {
                    path = path.Substring(1);
                }
            }

            return new UrlAddress(result.Url + path);
        }

        protected UrlAddress GetPreviousAddress()
        {
            var previousUrl = this.Url;
            if (previousUrl.Contains("/"))
                previousUrl = previousUrl.Substring(0, previousUrl.LastIndexOf("/"));

            return new UrlAddress(previousUrl);

        }

        public UrlAddress GetBaseAddress()
        {
            return new UrlAddress(this.GetPreviousAddress().Url + "/");
        }

        static public UrlAddress operator+(UrlAddress address, string path)
        {
            return address.Join(path);
        }

        static public implicit operator UrlAddress(string url)
        {
            return new UrlAddress(url);
        }

        static public implicit operator string (UrlAddress address)
        {
            return address?.Url ?? "";
        }

        static protected bool HasHttpPrefix(string url)
        {
            if (url == null)
                throw new ArgumentException("Invalid url provided!");

            return url.StartsWith("http://") || url.StartsWith("https://");
        }
    }
}
