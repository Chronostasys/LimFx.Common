using AngleSharp;
using AngleSharp.Dom;
using AutoMapper;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Ganss.XSS;
using LimFx.Business.Dto;
using LimFx.Business.Exceptions;
using LimFx.Business.Extensions;
using LimFx.Business.Models;
using Lucene.Net.Analysis.Cn.Smart;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Util;
using Markdig;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using Nest;
using Newtonsoft.Json;
using OpenXmlPowerTools;
using Qiniu.Http;
using Qiniu.Storage;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace LimFx.Business.Services
{
    /// <summary>
    /// 集成文章查询类，使用mongodb和es，自带md转换、word转换，支持草稿
    /// </summary>
    /// <typeparam name="TArticle">文章</typeparam>
    /// <typeparam name="TUser">用户</typeparam>
    /// <typeparam name="TUserBrief">文章中存的用户信息</typeparam>
    /// <typeparam name="TArticleSearch">es中索引的可搜索信息</typeparam>
    /// <typeparam name="TArticleFeed">文章的feed</typeparam>
    public class IntergratedArticleService<TArticle,TUser,TUserBrief, TArticleSearch, TArticleFeed> : DBQueryServices<TArticle>
        where TArticle:IArticle<TUserBrief>
        where TUserBrief:IUserBrief
        where TArticleSearch:class,ISearchAble,ICompleteAble,IGuidEntity
        where TUser:IEntity,IUser,ISearchAble,IPraiseAble
        where TArticleFeed: IArticleFeed
    {
        Func<TArticle, TArticleSearch> getSearchFromArticle;
        Expression<Func<TArticle, TArticleFeed>> mapArticleToBrief;
        protected IMapper mapper;
        protected IElasticClient client;
        protected IImageService imageService;
        protected MarkdownPipeline pipeline;
        protected ILogService<TUserBrief> logService;
        protected IUserService<TUser> userService;
        protected ILogService<TUserBrief> readlogService;
        protected List<string> allowedTags;
        /// <summary>
        /// 这个事件在用户成功发布公开文章成功后触发（对于普通用户来说在通过审核之后触发）
        /// </summary>
        public event EventHandler<TArticle> AfterPublicArticlePublished;
        Regex keywords = new Regex("<!-- keywords:.* -->", RegexOptions.Compiled);
        Regex description = new Regex("<!-- description:.* -->", RegexOptions.Compiled);
        Regex cover = new Regex("<!-- coverimage:.* -->", RegexOptions.Compiled);
        Regex titleReg = new Regex("<h1>.*</h1>", RegexOptions.Compiled);
        bool enableEs;
        ResumableUploader target;
        HtmlSanitizer sanitizer;
        /// <summary>
        /// 构造方法会自动建立所需要的索引
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="colname">mongodb里article集合的名称</param>
        /// <param name="mapper"></param>
        /// <param name="logService">用来获取点赞、收藏信息的LogService</param>
        /// <param name="esClient">如果不用es，手动传入null</param>
        /// <param name="userService"></param>
        /// <param name="imageService">imageservice只有word上传用到了，不准备使用word上传功能的话可以设为null</param>
        /// <param name="readlogService">用来获得用户阅读信息的logservice，可以和之前的logservice相同</param>
        /// <param name="getSearchFromArticle">把article映射到articlesearch的函数</param>
        /// <param name="mapArticleToBrief">把article映射成brief的函数</param>
        public IntergratedArticleService(IBaseDbSettings settings, string colname , IMapper mapper, 
            ILogService<TUserBrief> logService, IElasticClient esClient, IUserService<TUser> userService
            ,IImageService imageService, ILogService<TUserBrief> readlogService
            , Func<TArticle, TArticleSearch> getSearchFromArticle, Expression<Func<TArticle, TArticleFeed>> mapArticleToBrief)
            : base(settings.ConnectionString, settings.DatabaseName, colname)
        {
            this.getSearchFromArticle = getSearchFromArticle;
            this.mapArticleToBrief = mapArticleToBrief;
            this.readlogService = readlogService;
            enableEs = esClient != null;
            client = esClient;
            allowedTags = HtmlSanitizer.DefaultAllowedTags.ToList();
            allowedTags.Add("audio");
            allowedTags.Add("svg");
            allowedTags.Add("source");
            allowedTags.Add("style");
            allowedTags.Add("path");
            allowedTags.Add("iframe");
            allowedTags.Add("img");
            allowedTags.Add("blockquote");
            allowedTags.Add("span");
            allowedTags.Add("center");
            this.userService = userService;
            this.logService = logService;
            pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseDiagrams()
                .UseEmphasisExtras()
                .UseEmojiAndSmiley()
                .UseGridTables()
                .UsePipeTables()
                //.UseMathematics()
                .UseMediaLinks()
                .UseAutoLinks()
                .UseFigures()
                .UseFooters()
                .UseFootnotes()
                .Build();
            Collation _caseInsensitiveCollation = new Collation("en", strength: CollationStrength.Primary);
            this.imageService = imageService;
            this.mapper = mapper;
            createIndex:
            try
            {
                collection.Indexes.CreateOne(new CreateIndexModel<TArticle>(Builders<TArticle>.IndexKeys.Ascending(l => l.Stars)));
                collection.Indexes.CreateOne(new CreateIndexModel<TArticle>(Builders<TArticle>.IndexKeys.Ascending(l => l.Awesomes)));
                collection.Indexes.CreateOne(new CreateIndexModel<TArticle>(Builders<TArticle>.IndexKeys.Ascending(l => l.IsDraft)));
                collection.Indexes.CreateOne(new CreateIndexModel<TArticle>(Builders<TArticle>.IndexKeys.Ascending(l => l.AuthorBrief.Id)));
                collection.Indexes.CreateOne(new CreateIndexModel<TArticle>(Builders<TArticle>.IndexKeys.Ascending(l => l.Visibility)));
                collection.Indexes.CreateOne(new CreateIndexModel<TArticle>(Builders<TArticle>.IndexKeys.Ascending(l => l.KeyWords),
                    new CreateIndexOptions() { Collation = _caseInsensitiveCollation }));
                collection.Indexes.CreateOne(new CreateIndexModel<TArticle>(Builders<TArticle>.IndexKeys.Ascending(l => l.ProjectId)));
                collection.Indexes.CreateOne(new CreateIndexModel<TArticle>(Builders<TArticle>.IndexKeys.Ascending(l => l.Saved)));
                collection.Indexes.CreateOne(new CreateIndexModel<TArticle>(Builders<TArticle>.IndexKeys.Ascending(l => l.ArticleId)));
                collection.Indexes.CreateOne(new CreateIndexModel<TArticle>(Builders<TArticle>.IndexKeys.Ascending(l => l.Score)));
                collection.Indexes.CreateOne(new CreateIndexModel<TArticle>(Builders<TArticle>.IndexKeys.Ascending(l => l.Examined)));
                collection.Indexes.CreateOne(new CreateIndexModel<TArticle>(Builders<TArticle>.IndexKeys.Ascending(l => l.Tokens),
                    new CreateIndexOptions() { Collation = _caseInsensitiveCollation }));
                collection.Indexes.CreateOne(new CreateIndexModel<TArticle>(Builders<TArticle>.IndexKeys.Ascending(l => l.ManagedId)));
            }
            catch (Exception)
            {
                collection.Indexes.DropAll();
                goto createIndex;
            }
            Config config = new Config();
            // 设置上传区域
            config.Zone = Zone.ZONE_CN_East;
            // 设置 http 或者 https 上传
            config.UseHttps = true;
            config.UseCdnDomains = true;
            config.ChunkSize = ChunkUnit.U512K;
            target = new ResumableUploader(config);
            var attrs = new[]
{
                "style",
                "class",
                "href",
                "alt",
                "src",
                "controls",
                "id",
                "preserveAspectRatio",
                "d",
                "viewBox",
                "width",
                "height",
                "title",
                "data-formula",
                "aria-hidden",
            };
            foreach (var item in attrs)
            {
                HtmlSanitizer.DefaultAllowedAttributes.Add(item);
            }
            sanitizer = new HtmlSanitizer(allowedTags, allowedAttributes: HtmlSanitizer.DefaultAllowedAttributes);
            //StartReportService();
            //StartElastic();
        }
        public async ValueTask<FilterDefinition<TArticle>> GetIntergratedSearchFilter(string query, IElasticClient client)
        {
            var fb = Builders<TArticle>.Filter;
            if (query == null||!enableEs)
            {
                return fb.Empty;
            }

            var tokens = await client.Indices
                .AnalyzeAsync(s => s.Analyzer("smartcn").Text(query));
            return fb.AnyIn(a => a.Tokens, tokens.Tokens.Select(t => t.Token));
        }
        /// <summary>
        /// 这个方法通过调用elastic search api来获取符合搜索条件的文章id，并返回mongodb的filter
        /// </summary>
        /// <param name="query">这个参数是null时不会调用es</param>
        /// <param name="page"></param>
        /// <param name="pagesize"></param>
        /// <returns></returns>
        public async ValueTask<(FilterDefinition<TArticle> filter, IEnumerable<Guid> guids)> GetSearchFilterAsync(string query, int page, int pagesize)
        {
            if (string.IsNullOrEmpty(query))
            {
                return (Builders<TArticle>.Filter.Empty, new List<Guid>());
            }
            if (!enableEs)
            {
                throw new InvalidOperationException("无法在禁用es功能的情况下使用搜索功能！");
            }
            var ids = await GetSearchIdAsync(query, page, pagesize);
            return (Builders<TArticle>.Filter.In(a => a.ArticleId, ids), ids);
        }
        /// <summary>
        /// 封装通用搜索过程
        /// </summary>
        /// <param name="page"></param>
        /// <param name="pagesize"></param>
        /// <param name="orderby"></param>
        /// <param name="query"></param>
        /// <param name="keylist"></param>
        /// <param name="isDescending"></param>
        /// <param name="donotReturnReaded"></param>
        /// <param name="optionalFilter"></param>
        /// <returns></returns>
        public virtual async ValueTask<IEnumerable<TArticleFeed>> ProcessArticleSearchListAsync(ClaimsPrincipal user, int page = 0, int pagesize = 10,
            string orderby = null, string query = null, string keylist = null,
            bool isDescending = true, bool donotReturnReaded = false, FilterDefinition<TArticle> optionalFilter = null)
        {
            bool orderBySearch = false;
            if (!string.IsNullOrEmpty(query) && (orderby?.ToLower() == "search" || string.IsNullOrEmpty(orderby)))
            {
                orderBySearch = true;
            }
            var articlefilter = (optionalFilter ?? Builders<TArticle>.Filter.Empty);
            if (!orderBySearch)
            {
                var searchFilter = await GetIntergratedSearchFilter(query, client);
                articlefilter &= searchFilter;
                return await ProcessArticleListAsync(orderBySearch, user, page, pagesize, orderby,
                    query, keylist, isDescending, donotReturnReaded, articlefilter);
            }
            var re = await GetSearchFilterAsync(query, page, pagesize);
            articlefilter &= re.filter;
            var articles = await ProcessArticleListAsync(orderBySearch, user, page, pagesize, orderby,
                query, keylist, isDescending, donotReturnReaded, articlefilter);
            if (string.IsNullOrEmpty(query) || !string.IsNullOrEmpty(orderby))
            {

                return articles;
            }
            var finarticles = new List<TArticleFeed>();
            foreach (var item in re.guids)
            {
                var article = articles.Where(a => a.id == item).FirstOrDefault();
                if (article != null)
                {
                    finarticles.Add(article);
                }
            }
            return finarticles;
        }
        /// <summary>
        /// 封装获取文章list的过程
        /// </summary>
        /// <param name="orderBySearch"></param>
        /// <param name="page"></param>
        /// <param name="pagesize"></param>
        /// <param name="orderby"></param>
        /// <param name="query"></param>
        /// <param name="keylist"></param>
        /// <param name="isDescending"></param>
        /// <param name="donotReturnReaded"></param>
        /// <param name="optionalFilter"></param>
        /// <returns></returns>
        public virtual async ValueTask<IEnumerable<TArticleFeed>> ProcessArticleListAsync(bool orderBySearch, ClaimsPrincipal user, int page = 0, int? pagesize = 10,
            string orderby = null, string query = null, string keylist = null,
            bool isDescending = true, bool donotReturnReaded = false, FilterDefinition<TArticle> optionalFilter = null)
        {
            var t = GetReadedIdsAsync(user);
            var articlefilter = (optionalFilter ?? Builders<TArticle>.Filter.Empty) 
                & Builders<TArticle>.Filter.Eq(u => u.IsDraft, false).FilterKeyWords(keylist);
            if (donotReturnReaded)
            {
                articlefilter &= Builders<TArticle>.Filter.Nin(a => a.ArticleId, await t);
            }
            var articles = await GetAsync(mapArticleToBrief, orderBy: orderby, page: orderBySearch ? 0 : page, pageSize: pagesize,
                filter: articlefilter, query: null, isDescending: isDescending);
            var ids = await t;
            var re = new List<TArticleFeed>();

            foreach (var item in articles)
            {
                if (ids.Contains(item.id))
                {
                    item.isReadBefore = true;
                }
            }
            return articles;
        }
        public override async ValueTask AddAsync(List<TArticle> t)
        {
            int i = 0;
            foreach (var item in t)
            {
                long managedid = 0;
                try
                {
                    managedid = await GetManagedIdAsync(item.ArticleId);
                }
                catch (Exception)
                {
                }
                item.CreateTime = DateTime.UtcNow;
                item.UpdateTime = DateTime.UtcNow;
                item.IsDeleted = false;
                item.ManagedId = managedid == 0 ? await GetNextManagedId() : managedid;
                i++;
            }
            await collection.InsertManyAsync(t);
        }
        public override async ValueTask AddAsync(params TArticle[] t)
        {
            for (int i = 0; i < t.Length; i++)
            {
                long managedid = 0;
                try
                {
                    managedid = await GetManagedIdAsync(t[i].ArticleId);
                }
                catch (Exception)
                {
                }
                t[i].UpdateTime = DateTime.UtcNow;
                t[i].IsDeleted = false;
                t[i].CreateTime = DateTime.UtcNow;
                t[i].ManagedId = managedid == 0 ? await GetNextManagedId() : managedid;
            }
            await collection.InsertManyAsync(t);
        }

        public override ValueTask<Guid> GetGuidAsync(long managedId)
        {
            return base.FindFirstAsync(a => a.ManagedId == managedId, a => a.ArticleId);
        }
        public ValueTask<long> GetManagedIdAsync(Guid id)
        {
            return base.FindFirstAsync(a => a.ArticleId == id, a => a.ManagedId);
        }

        public async ValueTask IndexAllArticlesAsync()
        {
            if (!enableEs)
            {
                throw new InvalidOperationException("无法在已关闭es功能的情况下调用此方法！");
            }
            int i = 0;
            int bulksize = 1000;

            while (true)
            {
                var b = await GetAsync(a => a, i, bulksize, filter: filterBuilder.Eq(a => a.IsDraft, false) &
                    filterBuilder.Eq(a => a.Visibility, ArticleVisibility.everyone)
                    & filterBuilder.Eq(a => a.Examined, true));
                foreach (var article in b)
                {
                    var re = await client.Indices.AnalyzeAsync(a =>
                        a.Analyzer("smartcn")
                            .Text(article.SearchAbleString));
                    await UpDateAsync(article.Id, Builders<TArticle>.Update.Set(a => a.Tokens, re.Tokens.Select(t => t.Token)), 
                        false);
                }
                if (b.Any())
                {
                    await client.IndexManyAsync(b.Select(getSearchFromArticle));
                    i++;
                }
                else
                {
                    break;
                }
            }
        }
        public async ValueTask<IEnumerable<Guid>> GetReadedIdsAsync(ClaimsPrincipal user)
        {
            var fb = Builders<Auditlog<TUserBrief>>.Filter;
            var d = await readlogService.collection.DistinctAsync(l => l.OperatedObjectInfo.Id, fb.Eq(a => a.Operator.Id, Guid.Parse(user.Identity.Name))
                & fb.Eq(a => a.Operation, Operation.Access)
                & fb.Eq(a => a.OperatedObjectInfo.type, OperatedType.Article));
            return await d.ToListAsync();
        }
        public async ValueTask<IEnumerable<Guid>> GetReadedIdsAsync(Guid uid)
        {
            var fb = Builders<Auditlog<TUserBrief>>.Filter;
            var d = await readlogService.collection.DistinctAsync(l => l.OperatedObjectInfo.Id, fb.Eq(a => a.Operator.Id, uid)
                & fb.Eq(a => a.Operation, Operation.Access)
                & fb.Eq(a => a.OperatedObjectInfo.type, OperatedType.Article));
            return await d.ToListAsync();
        }
        public async ValueTask<FilterDefinition<TArticle>> GetNotReadFilterAsync(ClaimsPrincipal user)
        {
            var ids = await GetReadedIdsAsync(user);
            return Builders<TArticle>.Filter.Nin(a => a.ArticleId, ids);
        }
        public async ValueTask<TArticle> SantilizeArticle(TArticle article)
        {
            article.Content = await Santilize(article.Content);
            return article;
        }
        protected virtual XElement WordImageHandler(ImageInfo info)
        {
            var dto = imageService.GenerateQiNiuImageToken(null, Guid.Empty, SaveImageType.ArticelContent).Result;
            string token = dto.token;

            PutExtra extra = new PutExtra();
            using var stream = new MemoryStream();
            info.Bitmap.Save(stream, ImageFormat.Jpeg);
            HttpResult result = target.UploadStream(stream, dto.resource_key, token, extra);
            if (result.Code == 200)
            {
                var dto2 = JsonConvert.DeserializeObject<QiniuReturnDto>(result.Text);
                return new XElement(Xhtml.img, new XAttribute(NoNamespace.src, Path.Combine(imageService.baseUrl, dto2.key)));
            }
            else
            {
                return new XElement(Xhtml.img, new XAttribute(NoNamespace.alt, "图片转化失败"));
            }
        }
        public async ValueTask<ManagedIdDto> RenderWordAsync(Stream stream, string title, TUserBrief user, Guid id)
        {
            var anytask = AnyAsync(a => a.ArticleId == id);
            var article = Activator.CreateInstance<TArticle>();
            using WordprocessingDocument doc = WordprocessingDocument.Open(stream, true);
            HtmlConverterSettings settings = new HtmlConverterSettings()
            {
                PageTitle = "My Page Title",
                ImageHandler = WordImageHandler
            };
            XElement html = HtmlConverter.ConvertToHtml(doc, settings);
            article.AuthorBrief = user;

            var santilizetask = Santilize(html.ToStringNewLineOnAttributes());
            article.Title = title;
            article.IsDraft = true;
            article.Visibility = ArticleVisibility.everyone;
            article.Saved = true;
            article.DraftOrPublishId = Guid.Empty;
            Guid aid = Guid.Empty;
            article.Content = await santilizetask;

            article.Content = string.Concat(article.Content, @"
<hr>
<p>本文章由word文档转换而成</p>
");
            var ud = Builders<TArticle>.Update
                .Set(a => a.Content, article.Content)
                .Set(a => a.Saved, true);
            if (id != Guid.Empty && await anytask)
            {
                await UpDateAsync(a => a.ArticleId == id && a.IsDraft == true, ud);
                aid = id;
            }
            else
            {
                article.ArticleId = Guid.NewGuid();
                article.DraftOrPublishId = Guid.Empty;
                await AddArticleAsync(article);
                aid = article.ArticleId;
            }
            return new ManagedIdDto(await GetManagedIdAsync(aid), aid);
        }

        public async ValueTask<ManagedIdDto> RenderMarkDownAsync(IFormFileCollection files, TUserBrief user, Guid id)
        {

            var task = FindFirstAsync(a => a.ArticleId == id, a => a.AuthorBrief.Id);
            var article = Activator.CreateInstance<TArticle>();
            article.AuthorBrief = user;
            var text = files.Where(f => f.FileName.ToUpper().EndsWith(".MD")).First();
            var md = new StreamReader(text.OpenReadStream());
            try
            {
                var content = md.ReadToEnd();
                var ti = titleReg.Match(content);
                var title = "默认标题";
                try
                {
                    title = ti.Value.Substring(4, ti.Value.Length - 9);
                }
                catch (Exception)
                {
                }
                content = content.Remove(ti.Index, ti.Length);
                content = new string(content.Concat(@"  
<hr/>
<p>本文章使用<a href=""https://www.limfx.pro/ReadArticle/349/limfx-de-vscode-cha-jian-kuai-su-ru-men"" rel=""noopener noreferrer nofollow"">limfx的vscode插件</a>快速发布</p>").ToArray());

                //content = mathBlock.Replace(content, s =>"\n" + s.Value + "\n", 1000);
                var html = content;
                if (string.IsNullOrWhiteSpace(html))
                {
                    throw new _400Exception();
                }
                article.Content = await Santilize(html);
                article.Title = title;
                article.IsDraft = true;
                article.Visibility = ArticleVisibility.everyone;
                article.Saved = true;
                var ud = Builders<TArticle>.Update
                    .Set(a => a.Content, article.Content)
                    .Set(a => a.Title, article.Title)
                    .Set(a => a.Saved, true);
                var ss = content.Split("```");
                var builder = new StringBuilder(500000);
                for (int i = 0; i < ss.Length; i++)
                {
                    if (i % 2 == 0)
                    {
                        builder.Append(ss[i]);
                    }
                }
                var s = builder.ToString();
                var km = keywords.Match(s);
                var dm = description.Match(s);
                var cm = cover.Match(s);
                if (km.Success)
                {
                    article.KeyWords = km.Value.Substring(14, km.Value.Length - 18).Split(";").Where(i => !string.IsNullOrWhiteSpace(i)).ToArray();
                    ud = ud.Set(a => a.KeyWords, article.KeyWords);
                }
                if (dm.Success)
                {
                    article.ArticleAbstract = dm.Value.Substring(17, dm.Value.Length - 21);
                    ud = ud.Set(a => a.ArticleAbstract, article.ArticleAbstract);
                }
                if (cm.Success)
                {
                    var c = cm.Value.Split('(')[1];
                    article.CoverUrl = c.Substring(0, c.Length - 5);
                    ud = ud.Set(a => a.CoverUrl, article.CoverUrl);
                }
                Guid aid = Guid.Empty;
                if (id != Guid.Empty)
                {
                    try
                    {
                        var userid = await task;
                        if (userid != user.Id)
                        {
                            throw new _403Exception();
                        }
                        await UpDateAsync(a => a.ArticleId == id && a.IsDraft == true, ud);
                        aid = id;

                    }
                    catch (Exception)
                    {
                        article.ArticleId = Guid.NewGuid();
                        article.DraftOrPublishId = Guid.Empty;
                        await AddArticleAsync(article);
                        aid = article.ArticleId;
                    }
                }
                else
                {
                    article.ArticleId = Guid.NewGuid();
                    article.DraftOrPublishId = Guid.Empty;
                    await AddArticleAsync(article);
                    aid = article.ArticleId;
                }
                md.Dispose();
                return new ManagedIdDto(await GetManagedIdAsync(aid), aid);
            }
            catch (Exception e)
            {

                throw new _400Exception(exception: e);
            }
        }


        public async Task<string> Santilize(string html)
        {
            //Use the default configuration for AngleSharp
            var config = Configuration.Default;

            //Create a new context for evaluating webpages with the given config
            var context = BrowsingContext.New(config);

            //Just get the DOM representation
            var document = await context.OpenAsync(req => req.Content(html));
            var frames = document.GetElementsByTagName("ifrme");
            if (!(frames.Where(e => e.GetAttribute("src").Contains("player.bilibili.com")).Count() == frames.Count()))
                throw new _400Exception("Invalid frame src!");
            document.Dispose();
            return sanitizer.Sanitize(html);
        }

        public async ValueTask<IEnumerable<TArticleFeed>> GetDraftsAsync(Guid id, int page = 0,
            int pagesize = 10, string orderby = "UpdateTime", string query = null, string keylist = null, bool isDescending = true)
        {

            return await GetAsync(mapArticleToBrief, page, pagesize, orderby,
                filter: filterBuilder.Where(a => a.AuthorBrief.Id == id && a.IsDraft && a.Saved)
                .FilterKeyWords(keylist), query: query, isDescending: isDescending);
        }
        public async ValueTask<IEnumerable<Guid>> GetSearchIdAsync(string searchstring, int page, int pagesize)
        {
            var response = await client.SearchAsync<TArticleSearch>(s => s
                .Index("article").Query(q => q
                        .QueryString(sq => sq.Fields(f => f.Field(f => f.SearchAbleString))
                        .Query(searchstring))).Sort(s => s.Descending("_score"))
                        .Skip(page * pagesize).Take(pagesize));
            return response.Documents.Select(d => d.Id).ToList();
        }
        public async IAsyncEnumerable<string> GetSuggestions(string prefix, int size = 5)
        {
            var s = await client.SearchAsync<TArticleSearch>(s => s.Suggest(g =>
                g.Completion("complete", a => a.Prefix(prefix).SkipDuplicates().Size(size).Field(a => a.CompletionField))));
            foreach (var item1 in s.Suggest["complete"].Select(a => a.Options).AsEnumerable())
            {
                foreach (var item in item1)
                {
                    yield return item.Text;
                }
            }
        }
        public async ValueTask UpDateAsync(TArticle article, Expression<Func<TArticle, bool>> filter, UpdateDefinition<TArticle> updateDefinition, bool updateTime = true)
        {
            if (!article.IsDraft&&enableEs)
            {
                BeforeUpdateOrEditAsync(article);
                updateDefinition = updateDefinition.Set(a => a.Tokens, article.Tokens);
            }
            var doc = getSearchFromArticle(article);
            var ct = client.IndexAsync(doc, S => S.Index("article"));
            await base.UpDateAsync(filter, updateDefinition, updateTime);
            if (article.Examined && !article.IsDraft && article.Visibility == ArticleVisibility.everyone && enableEs)
            {
                await AfterPublishPublicArticleAsync(article);
            }
            await ct;
        }
        public virtual async ValueTask UpDateArticleAsync(TArticle article, bool isdraft)
        {
            article = await SantilizeArticle(article);
            await UpDateAsync(
                filterBuilder.Eq(b => b.ArticleId, article.ArticleId)
                & filterBuilder.Eq(t => t.IsDraft, isdraft), Builders<TArticle>.Update
                .Set(b => b.Title, article.Title)
                .Set(b => b.ProjectId, article.ProjectId)
                .Set(b => b.Content, article.Content)
                .Set(b => b.ArticleAbstract, article.ArticleAbstract)
                .Set(b => b.CoverUrl, article.CoverUrl)
                .Set(b => b.Visibility, article.Visibility)
                .Set(b => b.KeyWords, article.KeyWords)
                .Set(b => b.Saved, article.Saved)
                .Set(b => b.IsDraft, article.IsDraft)
                .Set(b => b.DraftOrPublishId, article.DraftOrPublishId)
                .Set(b => b.SearchAbleString, article.SearchAbleString)
                .Set(b => b.Examined, article.Examined)
                .Set(a => a.ExamineState, article.ExamineState)
                .Set(a => a.Bgm, article.Bgm)
                .Set(a => a.Tokens, article.Tokens));
        }
        protected void BeforeUpdateOrEditAsync(TArticle t)
        {
            var a = new SmartChineseAnalyzer(LuceneVersion.LUCENE_48);

            using var ts = a.GetTokenStream("tokens", new StringReader(t.SearchAbleString));
            var att = ts.GetAttribute<ICharTermAttribute>();
            var tokens = new List<string>();

            ts.Reset();
            while (ts.IncrementToken())
            {
                tokens.Add(att.ToString());
            }
            t.Tokens = tokens;
        }
        protected virtual async ValueTask AfterPublishPublicArticleAsync(TArticle article)
        {
            var doc = getSearchFromArticle(article);
            var ct = client.IndexAsync(doc, S => S.Index("article"));
            AfterPublicArticlePublished?.Invoke(this, article);
            await ct;
        }

        public virtual async ValueTask<Guid> AddArticleAsync(TArticle article)
        {
            if (!article.IsDraft && enableEs)
            {
                BeforeUpdateOrEditAsync(article);
            }
            if (string.IsNullOrEmpty(article.Title) && !article.IsDraft)
            {
                throw new _400Exception("BlogTitle should not be null!");
            }
            if (!article.IsDraft)
            {
                article = await SantilizeArticle(article);
            }
            if (article.Examined && !article.IsDraft && article.Visibility == ArticleVisibility.everyone && enableEs)
            {
                await AfterPublishPublicArticleAsync(article);
            }
            article.Awesomes = 0;
            article.Id = Guid.NewGuid();
            article.AdminScore = 100;
            article.Score = 100;
            //article.DraftOrPublishId = Guid.Empty;
            article.IsDeleted = false;
            await AddAsync(article);
            return article.Id;
        }
        public async ValueTask<IEnumerable<TArticleFeed>> GetProjectArticles(Guid id, int page, int? pagesize,
            FilterDefinition<TArticle> filterDefinition, string orderby = "UpdateTime", string query = null, bool isDescending = true)
        {
            if (filterDefinition == null)
            {
                filterDefinition = filterBuilder.Empty;
            }
            return await GetAsync(mapArticleToBrief, page, pagesize, orderBy: orderby,
                filter: filterBuilder.Eq(a => a.ProjectId, id)
                & filterBuilder.Eq(a => a.IsDraft, false)
                & filterBuilder.Ne(a => a.Visibility, ArticleVisibility.onlyMyself) & filterDefinition
                , query: query, isDescending: isDescending);
        }



        public async ValueTask<ManagedIdDto> UpsertArticle(TArticle article, HttpContext httpContext,bool saveDraft = true)
        {
            //默认文章没有通过审核
            article.Examined = false;
            article.ExamineState = ExamineState.Examining;
            //if id is empty, insert the article as draft
            if (article.Id == Guid.Empty)
            {
                article.Saved = saveDraft;
                article.DraftOrPublishId = Guid.Empty;
                //这是更新后的articleid，草稿和发布版共用
                article.ArticleId = Guid.NewGuid();
                article.DraftOrPublishId = Guid.Empty;
                await AddArticleAsync(article);
                //if the user publish the article before auto save
                if (!article.IsDraft)
                {
                    //加入草稿
                    article.DraftOrPublishId = article.Id;
                    article.IsDraft = true;
                    article.Saved = false;
                    await AddArticleAsync(article);

                    //更新之前插入的草稿为发布文章
                    var id1 = article.DraftOrPublishId;
                    var id2 = article.Id;
                    article.Id = id1;
                    article.DraftOrPublishId = id2;
                    article.IsDraft = false;
                    if (await userService.CheckAuth(httpContext, Roles.SeniorUser))
                    {
                        //高级用户不需要审核
                        article.Examined = true;
                    }
                    await UpDateArticleAsync(article, article.IsDraft);
                    await userService.collection.IncreAsync(article.AuthorBrief.Id, u => u.Exp, 10);
                }
                return new ManagedIdDto(
                    await GetManagedIdAsync(article.ArticleId), article.ArticleId);
            }
            //check authority一下过程处理的文章是已经又草稿的，所以要检查是否是作者
            //这里注意，上传的dto中的id应当是统一的articleid，非entityid
            //到这里说明之前用户应当插入过这个article，找出它
            var publishArticle = await FindFirstAsync(a => a.ArticleId == article.ArticleId, a => a);
            if (publishArticle.AuthorBrief.Id.ToString() != httpContext.User.Identity.Name)
            {
                throw new _403Exception();
            }
            //if article is not draft, then the user is supposed to publish it
            if (!article.IsDraft)
            {
                //if article.DraftOrPublishId is empty, then this is the
                //first time user trying to publish this article
                //in that case, insert a copy of draft with the DraftOrPublishId has the value of 
                //draft's id and change isDraft to false.
                //On the opposite, set draft's DraftOrPublishId to the newly
                //created publish article's Id.
                //you are not supposed to understand this d=====(￣▽￣*)b
                if (publishArticle.DraftOrPublishId == Guid.Empty || publishArticle.DraftOrPublishId == publishArticle.Id)
                {
                    //进入这个if代表之前只保存过草稿，从来没发布过文章
                    //make a copy of article
                    //用automapper来copy，没错效率不高，但我就是懒
                    article.DraftOrPublishId = publishArticle.Id;
                    if (await userService.CheckAuth(httpContext, Roles.SeniorUser))
                    {
                        //高级用户不需要审核
                        article.Examined = true;
                    }
                    await AddArticleAsync(article);
                    var id1 = article.Id;
                    var id2 = article.DraftOrPublishId;
                    article.DraftOrPublishId = id1;
                    article.Id = id2;
                    article.Saved = false;
                    article.IsDraft = true;
                    var t1 = userService.collection.IncreAsync(article.AuthorBrief.Id, u => u.Exp, 10);
                    await UpDateArticleAsync(article, article.IsDraft);
                    await t1;
                    return new ManagedIdDto(
                        await GetManagedIdAsync(article.ArticleId), article.ArticleId);
                }
                else//this means the article is published before, and the user want to apply changes from draft to it
                {
                    if (await userService.CheckAuth(httpContext, Roles.SeniorUser))
                    {
                        //高级用户不需要审核
                        article.Examined = true;
                    }
                    await UpDateAsync(article, a => a.ArticleId == article.ArticleId, Builders<TArticle>.Update
                        .Set(b => b.Title, article.Title)
                        .Set(b => b.ProjectId, article.ProjectId)
                        .Set(b => b.Content, article.Content)
                        .Set(b => b.ArticleAbstract, article.ArticleAbstract)
                        .Set(b => b.CoverUrl, article.CoverUrl)
                        .Set(b => b.Visibility, article.Visibility)
                        .Set(b => b.KeyWords, article.KeyWords)
                        .Set(b => b.SearchAbleString, article.SearchAbleString)
                        .Set(b => b.Examined, article.Examined)
                        .Set(a => a.ExamineState, article.ExamineState)
                        .Set(a => a.Bgm, article.Bgm)
                        .Set(b => b.Saved, false)/*把草稿从草稿箱移除*/);
                    return new ManagedIdDto(
                        await GetManagedIdAsync(article.ArticleId), article.ArticleId);
                }

            }
            //if the user do not want to keep the draft, then sync it to publish article
            if (!saveDraft)
            {
                try
                {
                    //publisharticle is draft
                    TArticle publish;
                    if (publishArticle.IsDraft)
                    {
                        publish = await FindFirstAsync(publishArticle.DraftOrPublishId);
                    }
                    else
                    {
                        publish = publishArticle;
                    }
                    var id1 = publish.Id;
                    var id2 = publish.DraftOrPublishId;
                    publish.Id = id2;
                    publish.DraftOrPublishId = id1;
                    //now, publish is the last saved copy of draft, sync it!
                    await UpDateArticleAsync(publish, publish.IsDraft);
                    return new ManagedIdDto(
                        await GetManagedIdAsync(article.ArticleId), article.ArticleId);
                }
                catch (Exception)
                {
                    await DeleteAsync(a => a.ArticleId == article.ArticleId);
                    return null;
                }
            }
            article.Saved = saveDraft;
            if (article.DraftOrPublishId == article.ArticleId)
            {
                if (publishArticle.IsDraft)
                {
                    article.DraftOrPublishId = publishArticle.Id;
                }
                else
                {
                    article.DraftOrPublishId = publishArticle.DraftOrPublishId;
                }
            }
            await UpDateAsync(article, a => a.ArticleId == article.ArticleId && a.IsDraft == true,
                Builders<TArticle>.Update
                .Set(b => b.Title, article.Title)
                .Set(b => b.ProjectId, article.ProjectId)
                .Set(b => b.Content, article.Content)
                .Set(b => b.ArticleAbstract, article.ArticleAbstract)
                .Set(b => b.CoverUrl, article.CoverUrl)
                .Set(b => b.Visibility, article.Visibility)
                .Set(b => b.KeyWords, article.KeyWords)
                .Set(b => b.Saved, article.Saved)
                .Set(a => a.Bgm, article.Bgm)
                .Set(b => b.IsDraft, article.IsDraft)
                .Set(b => b.SearchAbleString, article.SearchAbleString)
                .Set(a => a.ExamineState, article.ExamineState)
                .Set(b => b.Examined, article.Examined));
            return new ManagedIdDto(
                await GetManagedIdAsync(article.ArticleId), article.ArticleId);
        }


    }
}
