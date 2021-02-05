using System;
using System.Collections.Generic;
using System.Text;

namespace LimFx.Business.Models
{
    public interface IBaseDbSettings
    {
        public string ConnectionString { get; set; }
        public string DatabaseName { get; set; }
    }
}
