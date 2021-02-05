using LimFx.Business.Services;
using System;
using System.Collections.Generic;

namespace LimFx.Business.Models
{
    public interface IEntity:IGuidEntity
    {
        DateTime CreateTime { get; set; }
        DateTime DeleteTime { get; set; }
        string EntityType { get; }
        Dictionary<string, object> ExtraInformation { get; set; }
        Guid Id { get; set; }
        bool IsDeleted { get; set; }
        long ManagedId { get; set; }
        DateTime UpdateTime { get; set; }

        void Update();
    }
}