using LimFx.Business.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using LimFx.Business.Extensions;
using Nest;
using AutoMapper;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis;
using System.Linq;
using Lucene.Net.Util;
using System.IO;
using Lucene.Net.Analysis.Cn.Smart;
using Lucene.Net.Analysis.TokenAttributes;

namespace LimFx.Business.Services
{
    /// <inheritdoc/>
    public class EsDBQueryServices<T, TEsSearch> : DBQueryServicesSlim<T>, IEsDBQueryServices<T, TEsSearch> 
        where T : IEntity, ISearchAble, ITokenedEntity
        where TEsSearch : class, ISearchAble, IGuidEntity
    {
        IElasticClient client;
        string index;
        string analyzer;
        protected IMapper mapper;
        public EsDBQueryServices(string connectionString, string databaseName, string collectionName,
            IElasticClient client, string esindex, IMapper mapper, string esanalyzer = "smartcn") : base(connectionString, databaseName, collectionName)
        {
            this.client = client;
            analyzer = esanalyzer;
            index = esindex;
            this.mapper = mapper;
            var re = client.Indices.Create(esindex, s =>
                s.Map<TEsSearch>(m =>
                    m.AutoMap()));
            collection.Indexes.CreateOne(new CreateIndexModel<T>(Builders<T>.IndexKeys.Text(t => t.SearchAbleString)));
            collection.Indexes.CreateOne(new CreateIndexModel<T>(Builders<T>.IndexKeys.Ascending(t => t.Tokens)));
        }


        public override ValueTask UpDateAsync(Expression<Func<T, bool>> filter, UpdateDefinition<T> updateDefinition, bool updateTime = true)
        {
            return base.UpDateAsync(filter, updateDefinition, updateTime);
        }


        public virtual async ValueTask IndexAllAsync(FilterDefinition<T> filter)
        {
            if (!client.Ping().IsValid)
            {
                throw new InvalidOperationException("无法在已关闭es功能的情况下调用此方法！");
            }
            int i = 0;
            int bulksize = 1000;

            while (true)
            {
                var b = await GetAsync(a => a, i, bulksize, filter: filter);
                foreach (var article in b)
                {
                    var re = await client.Indices.AnalyzeAsync(a =>
                        a.Analyzer("smartcn")
                            .Text(article.SearchAbleString));
                    await UpDateAsync(article.Id, Builders<T>.Update.Set(a => a.Tokens, re.Tokens.Select(t => t.Token)),
                        false);
                }
                if (b.Any())
                {
                    await client.IndexManyAsync(mapper.Map<IEnumerable<TEsSearch>>(b), index);
                    i++;
                }
                else
                {
                    break;
                }
            }
        }



        /// <inheritdoc/>
        protected virtual async ValueTask<IEnumerable<TProject>> EsSearchGetAsync<TProject>(
            ProjectionDefinition<T, TProject> projection, int page, int? pageSize, SortDefinition<T> orderby
            , FilterDefinition<T> filter = null, string query = null)
        {
            filter ??= Builders<T>.Filter.Empty;
            if (!string.IsNullOrWhiteSpace(query) && orderby == null)
            {
                var response = await client.SearchAsync<TEsSearch>(s => s
                    .Index(index).Query(q => q
                    .QueryString(sq => sq.Fields(f => f.Field(f => f.SearchAbleString))
                    .Query(query))).Sort(s => s.Descending("_score"))
                    .Skip(page * pageSize).Take(pageSize));
                var sr = response.Documents.Select(d => d.Id);
                var re = await collection.FindIgnoreCase(Builders<T>.Filter.Eq(t => t.IsDeleted, false) & filter
                    & filterBuilder.In(e => e.Id, sr))
                    .Project(projection)
                    .Skip(page * pageSize).Limit(pageSize)
                    .ToListAsync();
                try
                {
                    List<TProject> fin = new List<TProject>();
                    foreach (var item in sr)
                    {
                        var article = re.Where(a => ((IGuidEntity)a).Id == item).FirstOrDefault();
                        if (article != null)
                        {
                            fin.Add(article);
                        }
                    }
                    return fin;
                }
                catch (Exception)
                {
                    return re;
                }
            }
            if (!string.IsNullOrEmpty(query))
            {
                var tokens = await client.Indices
                    .AnalyzeAsync(s => s.Analyzer(analyzer).Text(query));
                filter &= filterBuilder.AnyIn(a => a.Tokens, tokens.Tokens.Select(t => t.Token));
            }
            return await collection.FindIgnoreCase(Builders<T>.Filter.Eq(t => t.IsDeleted, false) & filter)
                .Project(projection)
                .Sort(orderby ?? Builders<T>.Sort.Descending("CreateTime")).Skip(page * pageSize).Limit(pageSize)
                .ToListAsync();
        }
        /// <inheritdoc/>
        public virtual ValueTask<IEnumerable<TProject>> GetAsync<TProject>(
            Expression<Func<T, TProject>> expression, int page, int? pageSize, string orderBy, bool isDescending = true
            , FilterDefinition<T> filter = null, string query = null)
        {
            return EsSearchGetAsync(Builders<T>.Projection.Expression(expression),
                page, pageSize, 
                orderBy==null ? null:(isDescending? Builders<T>.Sort.Descending(orderBy): Builders<T>.Sort.Ascending(orderBy)), 
                filter, query);
        }



        public virtual ValueTask<IEnumerable<TProject>> GetAsync<TProject>(Expression<Func<T, TProject>> expression
            , int page, int? pageSize, Expression<Func<T, object>> orderBy
            , bool isDescending = true, FilterDefinition<T> filter = null, string query = null)
        {
            return EsSearchGetAsync(Builders<T>.Projection.Expression(expression),
                page, pageSize,
                orderBy == null ? null : (isDescending ? Builders<T>.Sort.Descending(orderBy) : Builders<T>.Sort.Ascending(orderBy)), 
                filter, query);
        }

        public virtual ValueTask<IEnumerable<TProject>> GetAsync<TProject>(Expression<Func<T, TProject>> expression
            , int page, int? pageSize, string orderBy, bool isDescending = true, Expression<Func<T, bool>> filter = null, string query = null)
        {
            return EsSearchGetAsync(Builders<T>.Projection.Expression(expression),
                page, pageSize,
                orderBy == null ? null : (isDescending ? Builders<T>.Sort.Descending(orderBy) : Builders<T>.Sort.Ascending(orderBy)),
                filter, query);
        }
        [Obsolete("Use GetAsync!")]
        public ValueTask<IEnumerable<TProject>> SearchAsync<TProject>(Expression<Func<T, TProject>> expression, string query, int page = 0, int? pageSize = 10, string orderBy = "_id", FilterDefinition<T> filter = null)
        {
            throw new NotImplementedException();
        }

        public virtual ValueTask<IEnumerable<TProject>> GetAsync<TProject>(ProjectionDefinition<T, TProject> projection, int page, int? pageSize, SortDefinition<T> sortDefinition = null, FilterDefinition<T> filter = null, string query = null)
        {
            return EsSearchGetAsync(projection,
                page, pageSize,
                sortDefinition,
                filter, query);
        }
        /// <inheritdoc/>
        public virtual void BeforeAddAndUpdate(T t)
        {
            var a = new SmartChineseAnalyzer(LuceneVersion.LUCENE_48);
            
            using var ts = a.GetTokenStream("tokens", new StringReader(t.SearchAbleString??""));
            var att = ts.GetAttribute<ICharTermAttribute>();
            var tokens = new List<string>();

            ts.Reset();
            while(ts.IncrementToken())
            {
                tokens.Add(att.ToString());
            }
            t.Tokens = tokens;
        }
        /// <inheritdoc/>
        public virtual async ValueTask AfterAddAndUpdate(IEnumerable<T> t)
        {
            var m = mapper.Map<IEnumerable<TEsSearch>>(t);
            var re = await client.IndexManyAsync<TEsSearch>(objects: m,index: index);
        }
        /// <inheritdoc/>
        public override async ValueTask AddAsync(params T[] ts)
        {
            if (ts.Length!=1)
            {
                Parallel.ForEach(ts, BeforeAddAndUpdate);
            }
            else
            {
                BeforeAddAndUpdate(ts[0]);
            }
            
            await base.AddAsync(ts);
            await AfterAddAndUpdate(ts);
        }
        /// <inheritdoc/>
        public async override ValueTask AddAsync(List<T> ts)
        {
            Parallel.ForEach(ts, BeforeAddAndUpdate);
            await base.AddAsync(ts);
            await AfterAddAndUpdate(ts);
        }
        /// <inheritdoc/>
        public override async ValueTask DeleteAsync(params Guid[] ids)
        {
            await base.DeleteAsync(ids);
            var t = client.DeleteManyAsync(ids.Select(i =>
            {
                var proj = Activator.CreateInstance<TEsSearch>();
                proj.Id = i;
                return proj;
            }));
        }

        public virtual ValueTask<IEnumerable<TProject>> GetAsync<TProject>(Expression<Func<T, TProject>> expression, int page, int? pageSize, FilterDefinition<T> filter = null, string query = null)
        {
            return EsSearchGetAsync(Builders<T>.Projection.Expression(expression),
                page, pageSize,
                null,
                filter, query);
        }

        public virtual ValueTask<IEnumerable<TProject>> GetAsync<TProject>(ProjectionDefinition<T, TProject> projection, int page, int? pageSize, FilterDefinition<T> filter = null, string query = null)
        {
            return EsSearchGetAsync(projection,
                page, pageSize,
                null,
                filter, query);
        }
    }
}
