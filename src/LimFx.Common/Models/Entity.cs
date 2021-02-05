using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace LimFx.Business.Models
{
    public class Entity : IEntity
    {

        public Entity()
        {
            Id = System.Guid.NewGuid();
            ExtraInformation = new Dictionary<string, object>();
            CreateTime = DateTime.UtcNow;
            UpdateTime = CreateTime;
            IsDeleted = false;
        }
        [BsonRepresentation(MongoDB.Bson.BsonType.Binary)]
        public Guid Id { get; set; }
        public long ManagedId { get; set; }

        public bool IsDeleted { get; set; }
        public DateTime DeleteTime { get; set; }

        public string EntityType { get; protected set; }

        //附加信息
        [BsonExtraElements]
        public Dictionary<string, object> ExtraInformation { get; set; }

        [BsonElement("CreateTime")]
        [BsonDateTimeOptions(DateOnly = false, Kind = DateTimeKind.Utc, Representation = MongoDB.Bson.BsonType.Int64)]
        public DateTime CreateTime { get; set; }
        [BsonDateTimeOptions(DateOnly = false, Kind = DateTimeKind.Utc, Representation = MongoDB.Bson.BsonType.Int64)]
        public DateTime UpdateTime { get; set; }

        public void Update()
        {
            UpdateTime = DateTime.UtcNow;
        }
    }
}
