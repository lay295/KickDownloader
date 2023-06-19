using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KickDownloader
{
    public class KickVideoResponse
    {
        public string title { get; set; }
        public string created_at { get; set; }
        public List<Stream> streams { get; set; }
    }

    public class Stream
    {
        public string bandwidth { get; set; }
        public string resolution { get; set; }
        public string quality { get; set; }
        public string source { get; set; }

        public override string ToString()
        {
            return quality;
        }
    }
}
