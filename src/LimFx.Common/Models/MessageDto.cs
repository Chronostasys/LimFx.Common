using LimFx.Business.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace LimFx.Business.Dto
{
    public class MessageDto<TUserBrief>
    {
        public TUserBrief user { get; set; }
        public string message { get; set; }
    }
}
