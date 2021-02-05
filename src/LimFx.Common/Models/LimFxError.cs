using System;
using System.Collections.Generic;
using System.Text;

namespace LimFx.Business.Models
{
    public class LimFxError
    {
        public string type { get; set; } = "https://tools.ietf.org/html/rfc7231";
        public string title { get; set; }
        public int status { get; set; }
        public string traceId { get; set; }
        public Dictionary<string, string> errors { get; set; }

    }
}
