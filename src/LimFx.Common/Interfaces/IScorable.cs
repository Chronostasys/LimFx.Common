using System;
using System.Collections.Generic;
using System.Text;

namespace LimFx.Business.Models
{
    public interface IScorable
    {
        int Score { get; set; }
        int AdminScore { get; set; }
        int Stars { get; set; }
        int Awesomes { get; set; }
        DateTime CreateTime { get; set; }
    }

}
