using LimFx.Business.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LimFx.Business.Extensions;
using Nest;
using MimeKit.Cryptography;

namespace LimFx.Business.Services
{
    /// <inheritdoc/>
    public class DBQueryServices<T> : DBQueryServicesSlim<T>, IDBQueryServices<T> where T : IEntity, ISearchAble
    {
        public DBQueryServices(string connectionString, string databaseName, string collectionName) : base(connectionString, databaseName, collectionName)
        {
            collection.Indexes.CreateOne(new CreateIndexModel<T>(Builders<T>.IndexKeys.Text(t => t.SearchAbleString)));
        }

        /// <inheritdoc/>
        public virtual async ValueTask<IEnumerable<TProject>> GetAsync<TProject>(
            ProjectionDefinition<T, TProject> projection, int page, int? pageSize
            , FilterDefinition<T> filter = null, string query = null)
        {
            filter ??= Builders<T>.Filter.Empty;
            var stringsearch = query == null ? Builders<T>.Filter.Empty : Builders<T>.Filter.Text(query, new TextSearchOptions()
            {
                CaseSensitive = false,
                DiacriticSensitive = false,
                Language = "none"
            });
            return await collection.FindIgnoreCase(Builders<T>.Filter.Eq(t => t.IsDeleted, false) & filter & stringsearch)
                .Project(projection).Skip(page * pageSize).Limit(pageSize)
                .ToListAsync();
        }
        /// <inheritdoc/>
        public virtual async ValueTask<IEnumerable<TProject>> GetAsync<TProject>(
            Expression<Func<T, TProject>> expression, int page, int? pageSize
            , FilterDefinition<T> filter = null, string query = null)
        {
            filter ??= Builders<T>.Filter.Empty;
            var stringsearch = query == null ? Builders<T>.Filter.Empty : Builders<T>.Filter.Text(query, new TextSearchOptions()
            {
                CaseSensitive = false,
                DiacriticSensitive = false,
                Language = "none"
            });
            return await collection.FindIgnoreCase(Builders<T>.Filter.Eq(t => t.IsDeleted, false) & filter & stringsearch)
                .Project(expression).Skip(page * pageSize).Limit(pageSize)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public virtual async ValueTask<IEnumerable<TProject>> GetAsync<TProject>(
            ProjectionDefinition<T, TProject> projection, int page, int? pageSize, SortDefinition<T> sortDefinition
            , FilterDefinition<T> filter = null, string query = null)
        {
            filter ??= Builders<T>.Filter.Empty;
            var stringsearch = query == null ? Builders<T>.Filter.Empty : Builders<T>.Filter.Text(query, new TextSearchOptions()
            {
                CaseSensitive = false,
                DiacriticSensitive = false,
                Language = "none"
            });
            return await collection.FindIgnoreCase(Builders<T>.Filter.Eq(t => t.IsDeleted, false) & filter & stringsearch)
                .Project(projection)
                .Sort(sortDefinition).Skip(page * pageSize).Limit(pageSize)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public virtual async ValueTask<IEnumerable<TProject>> GetAsync<TProject>(
            Expression<Func<T, TProject>> expression, int page, int? pageSize, string orderBy, bool isDescending = true
            , FilterDefinition<T> filter = null, string query = null)
        {
            if (isDescending)
            {
                return await GetAsync(Builders<T>.Projection.Expression(expression), page, pageSize, Builders<T>.Sort.Descending(orderBy??"CreateTime"), filter, query);
            }
            else
            {
                return await GetAsync(Builders<T>.Projection.Expression(expression), page, pageSize, Builders<T>.Sort.Ascending(orderBy??"CreateTime"), filter, query);
            }
        }
        /// <inheritdoc/>
        public virtual async ValueTask<IEnumerable<TProject>> GetAsync<TProject>(
            Expression<Func<T, TProject>> expression, int page, int? pageSize, Expression<Func<T, object>> orderBy, bool isDescending = true
            , FilterDefinition<T> filter = null, string query = null)
        {
            if (isDescending)
            {
                return await GetAsync(Builders<T>.Projection.Expression(expression), page, pageSize, Builders<T>.Sort.Descending(orderBy), filter, query);
            }
            else
            {
                return await GetAsync(Builders<T>.Projection.Expression(expression), page, pageSize, Builders<T>.Sort.Ascending(orderBy), filter, query);
            }
        }
        /// <inheritdoc/>
        public virtual async ValueTask<IEnumerable<TProject>> GetAsync<TProject>(
            Expression<Func<T, TProject>> expression, int page, int? pageSize, string orderBy, bool isDescending = true
            , Expression<Func<T, bool>> filter = null, string query = null)
        {
            if (isDescending)
            {
                return await GetAsync(Builders<T>.Projection.Expression(expression), page, pageSize, Builders<T>.Sort.Descending(orderBy??"CreateTime"), filter, query);
            }
            else
            {
                return await GetAsync(Builders<T>.Projection.Expression(expression), page, pageSize, Builders<T>.Sort.Ascending(orderBy??"CreateTime"), filter, query);
            }
        }
        /// <inheritdoc/>
        [Obsolete]
        public virtual async ValueTask<IEnumerable<TProject>> SearchAsync<TProject>(Expression<Func<T, TProject>> expression, string query, int page = 0, int? pageSize = 10, string orderBy = "_id",
            FilterDefinition<T> filter = null)
        {
            var filt = Builders<T>.Filter.Where(t => true);
            if (filter != null)
            {
                filt = filter;
            }
            return await collection.FindIgnoreCase(Builders<T>.Filter.Eq(t => t.IsDeleted, false) & filt
                & Builders<T>.Filter.Text(query, new TextSearchOptions()
                {
                    CaseSensitive = false,
                    DiacriticSensitive = false,
                    Language = "none"
                }))
                .Project(Builders<T>.Projection.Expression(expression))
                .Skip(page * pageSize).Limit(pageSize)
                .Sort(Builders<T>.Sort.Descending(orderBy??"CreateTime")).ToListAsync();
        }
    }
}
