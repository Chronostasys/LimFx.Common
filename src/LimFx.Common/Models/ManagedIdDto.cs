using System;

namespace LimFx.Business.Dto
{
    public class ManagedId
    {
        public string id { get; set; }
    }
    public class ManagedIdDto
    {
        public ManagedIdDto(long id, Guid guid)
        {
            managedId = id;
            this.id = guid;
        }
        public long managedId { get; set; }
        public Guid id { get; set; }
    }
}
