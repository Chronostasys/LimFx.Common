using System;

namespace LimFx.Business.Dto
{
    public interface IArticleFeed
    {
        Guid id { get; set; }
        bool isReadBefore { get; set; }
    }
}