using LimFx.Business.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LimFx.Business.Extensions;
using Microsoft.ApplicationInsights.WindowsServer;

namespace LimFx.Business.Services
{
    public class DBQueryServicesSlim<T> : IDBQueryServicesSlim<T> where T : IEntity
    {
        public IMongoCollection<T> collection { get; }
        Guid counterId = Guid.Parse("10000000-0000-0000-0000-000000000000");
        public static FilterDefinitionBuilder<T> filterBuilder { get; } = Builders<T>.Filter;
        public DBQueryServicesSlim(string connectionString, string databaseName, string collectionName)
        {
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            //database.DropCollection(collectionName);
            collection = database.GetCollection<T>(collectionName);
            try
            {
                collection.Indexes.CreateOneAsync(new CreateIndexModel<T>(Builders<T>.IndexKeys.Ascending(t => t.CreateTime)));
                collection.Indexes.CreateOneAsync(new CreateIndexModel<T>(Builders<T>.IndexKeys.Ascending(t => t.UpdateTime)));
            }
            catch (Exception)
            {
                collection.Indexes.DropAll();
                collection.Indexes.CreateOneAsync(new CreateIndexModel<T>(Builders<T>.IndexKeys.Ascending(t => t.CreateTime)));
                collection.Indexes.CreateOneAsync(new CreateIndexModel<T>(Builders<T>.IndexKeys.Ascending(t => t.UpdateTime)));
            }
        }
        public virtual ValueTask<Guid> GetGuidAsync(long managedId)
        {
            return FindFirstAsync(e => e.ManagedId == managedId, e => e.Id);
        }
        public virtual async ValueTask<long> GetNextManagedId(long i=1)
        {
            try
            {
                return (await collection.FindOneAndUpdateAsync(e => e.Id == counterId, Builders<T>.Update.Inc(e => e.ManagedId, i))).ManagedId;
            }
            catch (Exception)
            {
                var t = Activator.CreateInstance<T>();
                t.Id = counterId;
                t.ManagedId = 1;
                t.IsDeleted = true;
                await collection.InsertOneAsync(t);
                return (await collection
                    .FindOneAndUpdateAsync(
                    e => e.Id == counterId, 
                    Builders<T>.Update.Inc(e => e.ManagedId, i))).ManagedId;
            }
        }
        public virtual async ValueTask<bool> AnyAsync(FilterDefinition<T> filter)
        {
            return await collection.FindIgnoreCase(Builders<T>.Filter.Eq(t => t.IsDeleted, false) & filter).AnyAsync();
        }
        public virtual async ValueTask<bool> AnyAsync(Expression<Func<T, bool>> filter)
        {
            return await collection.FindIgnoreCase(Builders<T>.Filter.Eq(t => t.IsDeleted, false) & filter).AnyAsync();
        }

        /// <inheritdoc/>
        public virtual async ValueTask<IEnumerable<TProject>> GetAsync<TProject>(
            ProjectionDefinition<T, TProject> projection, int page, int? pageSize, SortDefinition<T> sortDefinition
            , FilterDefinition<T> filter = null)
        {
            await Task.Yield();
            filter ??= Builders<T>.Filter.Empty;
            return await collection.FindIgnoreCase(Builders<T>.Filter.Eq(t => t.IsDeleted, false) & filter)
                .Project(projection)
                .Sort(sortDefinition).Skip(page * pageSize).Limit(pageSize)
                .ToListAsync();
        }
        /// <inheritdoc/>
        public virtual async ValueTask<IEnumerable<TProject>> GetAsync<TProject>(
            ProjectionDefinition<T, TProject> projection, int page, int? pageSize
            , FilterDefinition<T> filter = null)
        {
            filter ??= Builders<T>.Filter.Empty;
            return await collection.FindIgnoreCase(Builders<T>.Filter.Eq(t => t.IsDeleted, false) & filter)
                .Project(projection).Skip(page * pageSize).Limit(pageSize)
                .ToListAsync();
        }
        /// <inheritdoc/>
        public virtual async ValueTask<IEnumerable<TProject>> GetAsync<TProject>(
            Expression<Func<T, TProject>> expression, int page, int? pageSize
            , FilterDefinition<T> filter = null)
        {
            await Task.Yield();
            filter ??= Builders<T>.Filter.Empty;
            return await collection.FindIgnoreCase(Builders<T>.Filter.Eq(t => t.IsDeleted, false) & filter)
                .Project(expression).Skip(page * pageSize).Limit(pageSize)
                .ToListAsync();
        }
        /// <inheritdoc/>
        public virtual async ValueTask<IEnumerable<TProject>> GetAsync<TProject>(
            Expression<Func<T, TProject>> expression, int page, int? pageSize, string orderBy, bool isDescending = true
            , FilterDefinition<T> filter = null)
        {
            if (isDescending)
            {
                return await GetAsync(Builders<T>.Projection.Expression(expression), page, pageSize, Builders<T>.Sort.Descending(orderBy??"CreateTime"), filter);
            }
            else
            {
                return await GetAsync(Builders<T>.Projection.Expression(expression), page, pageSize, Builders<T>.Sort.Ascending(orderBy??"CreateTime"), filter);
            }
        }
        /// <inheritdoc/>
        public virtual async ValueTask<IEnumerable<TProject>> GetAsync<TProject>(
            Expression<Func<T, TProject>> expression, int page, int? pageSize, Expression<Func<T, object>> orderBy, bool isDescending = true
            , FilterDefinition<T> filter = null)
        {
            if (isDescending)
            {
                return await GetAsync(Builders<T>.Projection.Expression(expression), page, pageSize, Builders<T>.Sort.Descending(orderBy), filter);
            }
            else
            {
                return await GetAsync(Builders<T>.Projection.Expression(expression), page, pageSize, Builders<T>.Sort.Ascending(orderBy), filter);
            }
        }
        /// <inheritdoc/>
        public virtual async ValueTask<IEnumerable<TProject>> GetAsync<TProject>(
            Expression<Func<T, TProject>> expression, int page, int? pageSize, string orderBy, bool isDescending = true
            , Expression<Func<T, bool>> filter = null)
        {
            if (isDescending)
            {
                return await GetAsync(Builders<T>.Projection.Expression(expression), page, pageSize, Builders<T>.Sort.Descending(orderBy??"CreateTime"), filter);
            }
            else
            {
                return await GetAsync(Builders<T>.Projection.Expression(expression), page, pageSize, Builders<T>.Sort.Ascending(orderBy??"CreateTime"), filter);
            }
        }

        /// <inheritdoc/>
        public virtual async ValueTask AddAsync(params T[] t)
        {
            var id = await GetNextManagedId(t.Length);
            for (int i = 0; i < t.Length; i++)
            {
                t[i].UpdateTime = DateTime.UtcNow;
                t[i].IsDeleted = false;
                t[i].CreateTime = DateTime.UtcNow;
                t[i].ManagedId = i + id;
            }

            await collection.InsertManyAsync(t);
        }
        /// <inheritdoc/>
        public virtual async ValueTask AddAsync(List<T> t)
        {
            int i = 0;
            var id = await GetNextManagedId(t.Count);
            foreach (var item in t)
            {
                item.CreateTime = DateTime.UtcNow;
                item.UpdateTime = DateTime.UtcNow;
                item.IsDeleted = false;
                item.ManagedId = i + id;
                i++;
            }
            await collection.InsertManyAsync(t);
        }
        /// <inheritdoc/>
        public virtual async ValueTask<int> GetNumAsync(Expression<Func<T, bool>> filter)
        {
            return (int)await collection.FindIgnoreCase(filter & filterBuilder.Eq(a => a.IsDeleted, false)).CountDocumentsAsync();
        }
        /// <inheritdoc/>
        public virtual async ValueTask UpDateAsync(Guid id, UpdateDefinition<T> updateDefinition, bool updateTime = true)
        {
            if (updateTime)
            {
                updateDefinition = updateDefinition.Set(t => t.UpdateTime, DateTime.UtcNow);
            }
            var re = await collection.UpdateOneAsync(t => t.Id == id, updateDefinition);
        }
        /// <inheritdoc/>
        public virtual async ValueTask UpDateAsync(Expression<Func<T, bool>> filter, UpdateDefinition<T> updateDefinition, bool updateTime = true)
        {
            if (updateTime)
            {
                updateDefinition = updateDefinition.Set(t => t.UpdateTime, DateTime.UtcNow);
            }
            await collection.UpdateManyAsync(filter, updateDefinition);
        }
        /// <inheritdoc/>
        public virtual async ValueTask UpDateAsync(FilterDefinition<T> filter, UpdateDefinition<T> updateDefinition, bool updateTime = true)
        {
            if (updateTime)
            {
                updateDefinition = updateDefinition.Set(t => t.UpdateTime, DateTime.UtcNow);
            }
            await collection.UpdateManyAsync(filter, updateDefinition);
        }
        /// <inheritdoc/>
        public virtual async ValueTask<T> FindFirstAsync(Guid Id, FilterDefinition<T> filter = null)
        {
            return await collection.FindIgnoreCase(filterBuilder.Eq(t => t.Id, Id) & Builders<T>.Filter.Eq(t => t.IsDeleted, false) & (filter ?? filterBuilder.Empty)).FirstAsync();
        }
        /// <inheritdoc/>
        public virtual async ValueTask<TProject> FindFirstAsync<TProject>(FilterDefinition<T> filter, Expression<Func<T, TProject>> projection, SortDefinition<T> sort = null)
        {
            return await collection.FindIgnoreCase(filterBuilder.Eq(t => t.IsDeleted, false) & filter).Project(projection).Sort(sort ?? Builders<T>.Sort.Ascending("CreateTime")).FirstAsync();
        }
        /// <inheritdoc/>
        public virtual async ValueTask<TProject> FindFirstAsync<TProject>(Expression<Func<T, bool>> filter, Expression<Func<T, TProject>> projection, SortDefinition<T> sort = null)
        {
            return await collection.FindIgnoreCase(filterBuilder.Eq(t => t.IsDeleted, false) & filter).Project(projection).Sort(sort ?? Builders<T>.Sort.Ascending("CreateTime")).FirstAsync();
        }
        /// <inheritdoc/>
        public virtual async ValueTask DeleteAsync(params Guid[] id)
        {
            await collection.UpdateManyAsync(Builders<T>.Filter.In(t => t.Id, id), new UpdateDefinitionBuilder<T>()
                .Set(t => t.IsDeleted, true).Set(t => t.DeleteTime, DateTime.UtcNow));
        }
        /// <inheritdoc/>
        public virtual async ValueTask DeleteAsync(FilterDefinition<T> filter)
        {
            await collection.UpdateManyAsync(filter, new UpdateDefinitionBuilder<T>()
                .Set(t => t.IsDeleted, true).Set(t => t.DeleteTime, DateTime.UtcNow));
        }
        /// <inheritdoc/>
        public virtual async ValueTask DeleteAsync(Expression<Func<T, bool>> filter)
        {
            await collection.UpdateManyAsync(filter, new UpdateDefinitionBuilder<T>()
                .Set(t => t.IsDeleted, true).Set(t => t.DeleteTime, DateTime.UtcNow));
        }
        /// <inheritdoc/>
        public async ValueTask<int> GetNumAsync(FilterDefinition<T> filter)
        {
            return (int)await collection.FindIgnoreCase(filter & filterBuilder.Eq(a => a.IsDeleted, false)).CountDocumentsAsync();
        }
    }

}
