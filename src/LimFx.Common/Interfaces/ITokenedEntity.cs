using System.Collections.Generic;

namespace LimFx.Business.Models
{
    public interface ITokenedEntity
    {
        public IEnumerable<string> Tokens { get; set; }
    }
}
