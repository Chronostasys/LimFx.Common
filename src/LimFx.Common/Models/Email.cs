using LimFx.Business.Services;
using System;
using System.Collections.Generic;

namespace LimFx.Business.Models
{
    public class Email : Entity, ISearchAble, IEmail
    {
        public Email() : base()
        {
            ExpectSendTime = DateTime.UtcNow;
            Contents = new Dictionary<string, string>();
            Receivers = new List<string>();
        }
        public DateTime ExpectSendTime { get; set; }
        public string Sender { get; set; }
        public string ProjectName { get; set; }
        public List<string> Receivers { get; set; }
        public string Subject { get; set; }
        public string Url { get; set; }
        public string SearchAbleString { get; set; }
        public string RazorTemplate { get; set; }
        public string Requester { get; set; }
        public Dictionary<string, string> Contents { get; set; }
    }
}
