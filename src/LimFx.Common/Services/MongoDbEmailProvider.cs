using LimFx.Business.Exceptions;
using LimFx.Business.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LimFx.Business.Services
{

    public class MongoDbEmailProvider<T> : IEmailProvider<T> where T : Entity, IEmail
    {
        IDBQueryServicesSlim<T> EmailQuery;
        public MongoDbEmailProvider(string connectionString, string dataBaseName, string emailCollectionName)
        {
            EmailQuery = new DBQueryServicesSlim<T>(connectionString, dataBaseName, emailCollectionName);
            EmailQuery.collection.Indexes.CreateOneAsync(Builders<T>.IndexKeys.Ascending(e => e.ExpectSendTime));
            EmailQuery.collection.Indexes.CreateOneAsync(Builders<T>.IndexKeys.Ascending(e => e.Requester));
            EmailQuery.collection.Indexes.CreateOneAsync(Builders<T>.IndexKeys.Ascending(e => e.Receivers));
        }
        public async ValueTask<IEnumerable<T>> GetEmailsAsync()
        {
            return await EmailQuery.GetAsync(e => e, 0,pageSize: null, orderBy: "ExpectSendTime", filter: Builders<T>.Filter.Lt(e => e.ExpectSendTime, DateTime.UtcNow));
        }
        public async ValueTask AddAsync(params T[] emails)
        {
            await EmailQuery.AddAsync(emails);
        }
        public async ValueTask AddSentAsync(params T[] emails)
        {
            int i = 0;
            var id = await EmailQuery.GetNextManagedId(emails.Length);
            foreach (var item in emails)
            {
                item.CreateTime = DateTime.UtcNow;
                item.UpdateTime = DateTime.UtcNow;
                item.IsDeleted = true;
                item.ManagedId = i + id;
                i++;
            }
            await EmailQuery.collection.InsertManyAsync(emails);
        }
        public async ValueTask ThrowIfTooFrequentAsync(T email, int emailInterval)
        {
            var fb = Builders<T>.Filter;
            var toomuch = await EmailQuery.collection.Find(fb.Eq(e => e.Requester, email.Requester)
                & fb.Gte(e => e.CreateTime, email.CreateTime.AddSeconds(-emailInterval))
                & fb.AnyIn(e => e.Receivers, email.Receivers)).AnyAsync();
            if (toomuch)
            {
                throw new _429Exception($"{emailInterval}秒内向目标用户发送过email，请过段时间再发送");
            }
        }
        public async ValueTask DeleteAsync(params Guid[] ids)
        {
            await EmailQuery.DeleteAsync(ids);
        }
    }
}
