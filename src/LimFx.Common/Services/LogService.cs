using AutoMapper;
using DocumentFormat.OpenXml.Office2010.Excel;
using LimFx.Business.Dto;
using LimFx.Business.Exceptions;
using LimFx.Business.Models;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LimFx.Business.Modals
{
}

namespace LimFx.Business.Services
{
    /// <summary>
    /// 这是一个神奇的类，能干好多好多事情
    /// </summary>
    public class LogService<TUser, TUserBrief> : DBQueryServicesSlim<Auditlog<TUserBrief>>, IErrorLogger, ILogService<TUserBrief> where TUser : ISearchAble, IUser
        where TUserBrief : IUserBrief
    {
        readonly IDBQueryServicesSlim<TUser> userServices;
        readonly FilterDefinitionBuilder<Auditlog<TUserBrief>> f = Builders<Auditlog<TUserBrief>>.Filter;
        readonly IMapper mapper;
        public LogService(IBaseDbSettings settings, string collectionname, IMapper mapper, IDBQueryServicesSlim<TUser> userServices) :
            base(settings.ConnectionString, settings.DatabaseName, collectionname)
        {
            this.mapper = mapper;
            this.userServices = userServices;
            var client = new MongoClient(settings.ConnectionString);
            var database = client.GetDatabase(settings.DatabaseName);
            collection.Indexes.CreateOne(new CreateIndexModel<Auditlog<TUserBrief>>(Builders<Auditlog<TUserBrief>>.IndexKeys.Ascending(l => l.OperatedObjectInfo.Id)));
            collection.Indexes.CreateOne(new CreateIndexModel<Auditlog<TUserBrief>>(Builders<Auditlog<TUserBrief>>.IndexKeys.Ascending(l => l.Operator.Id)));
            collection.Indexes.CreateOne(new CreateIndexModel<Auditlog<TUserBrief>>(Builders<Auditlog<TUserBrief>>.IndexKeys.Ascending(l => l.Operation)));
            collection.Indexes.CreateOne(new CreateIndexModel<Auditlog<TUserBrief>>(Builders<Auditlog<TUserBrief>>.IndexKeys.Ascending(l => l.OperatedObjectInfo.OperatedUserId)));
            collection.Indexes.CreateOne(new CreateIndexModel<Auditlog<TUserBrief>>(Builders<Auditlog<TUserBrief>>.IndexKeys.Ascending(l => l.CreateTime)));
            collection.Indexes.CreateOne(new CreateIndexModel<Auditlog<TUserBrief>>(Builders<Auditlog<TUserBrief>>.IndexKeys.Ascending(l => l.OperatedObjectInfo.Infos["colname"])));
            collection.Indexes.CreateOne(new CreateIndexModel<Auditlog<TUserBrief>>(Builders<Auditlog<TUserBrief>>.IndexKeys.Ascending(l => l.OperatedObjectInfo.Infos)));
        }
        public async ValueTask LogAsync(Auditlog<TUserBrief> auditlog)
        {
            auditlog.Operator = await userServices.FindFirstAsync(
                u => u.Id == auditlog.Operator.Id, u => mapper.Map<TUserBrief>(u));
            await collection.InsertOneAsync(auditlog);
        }
        public async ValueTask LogAsync(List<Auditlog<TUserBrief>> auditlogs, string email)
        {
            var userBrief = await userServices.FindFirstAsync(
                u => u.Email == email, u => mapper.Map<TUserBrief>(u));
            for (int i = 0; i < auditlogs.Count(); i++)
            {
                auditlogs[i].Operator = userBrief;
            }
            await collection.InsertManyAsync(auditlogs);
        }
        public async ValueTask LogAsync(Guid userId, Operation operation, OperatedObjectInfo objectInfo, LogLevel logLevel = LogLevel.Info)
        {
            await Task.Yield();
            var userBrief = await userServices.FindFirstAsync(
                u => u.Id == userId, u => mapper.Map<TUserBrief>(u));
            var log = new Auditlog<TUserBrief>() { Operator = userBrief, Operation = operation, OperatedObjectInfo = objectInfo, LogLevel = logLevel };
            await collection.InsertOneAsync(log);
        }
        public async ValueTask<IEnumerable<Auditlog<TUserBrief>>> SearchLogsAsync(string email, DateTime start, DateTime end,
            int page = 0, int pageSize = 10)
        {
            var f = Builders<Auditlog<TUserBrief>>.Filter;
            var ft = f.Eq(a => a.Operator.Email, email);
            if (string.IsNullOrEmpty(email))
            {
                ft = f.Empty;
            }
            return await collection.Find(ft
                & f.Lt(a => a.CreateTime, end)
                & f.Gt(a => a.CreateTime, start)).Sort(Builders<Auditlog<TUserBrief>>.Sort
                .Descending(a => a.CreateTime)).Skip(page * pageSize).Limit(pageSize).ToListAsync();
        }
        public async ValueTask<IEnumerable<Auditlog<TUserBrief>>> GetLogsAsync(int page = 0, int pageSize = 10)
        {
            return await collection.Find(u => true).Sort(Builders<Auditlog<TUserBrief>>.Sort
                .Descending(a => a.CreateTime)).Skip(page * pageSize).Limit(pageSize).ToListAsync();
        }
        public async ValueTask CheckInvitedAsync(string email)
        {
            var filter = f.Eq(a => a.Operation, Operation.Invite)
                 & f.Eq(a => a.OperatedObjectInfo.Infos["Email"], email);
            var log = await collection.Find(filter).FirstOrDefaultAsync();
            if (log == null)
            {
                throw new _403Exception();
            }
        }
        public async ValueTask CheckProjectSessionExistedAsync(string[] emails, Guid projectId)
        {
            if (emails.GroupBy(e => e).Any(e => e.Count() > 1))
            {
                throw new _400Exception("duplicate emails!");
            }
            var filter = f.Eq(a => a.Operation, Operation.Invite);
            filter = filter & f.Eq(a => a.OperatedObjectInfo.Infos["Id"], projectId.ToString())
                & f.In(a => a.OperatedObjectInfo.Infos["Email"], emails);
            var log = await collection.Find(filter).FirstOrDefaultAsync();
            if (log != null)
            {
                if ((DateTime)log.OperatedObjectInfo.Infos["ExpireDate"] < DateTime.UtcNow)
                {
                    await collection.DeleteOneAsync(l => l.Id == log.Id);
                    return;
                }
                throw new _400Exception("Some of the invite emails have already been sent before!");
            }
        }
        public async ValueTask<(string email, Guid projectId, Guid logid)> CheckProjectInviteSessionIdAsync(Guid sessionId)
        {
            try
            {
                var filter = f.Eq(a => a.Operation, Operation.Invite)
                    & f.Eq(a => a.OperatedObjectInfo.Id, sessionId);
                var b = await collection.Find(filter).FirstAsync();
                await collection.FindOneAndUpdateAsync(f.Eq(t => t.Id, b.Id), new UpdateDefinitionBuilder<Auditlog<TUserBrief>>()
                    .Set(t => t.ProcessedStamps, new List<ProcessedStamp>() { new ProcessedStamp() { Processor = "EmailSender", ProcessedTime = DateTime.UtcNow } }));
                return (b.OperatedObjectInfo.Infos["Email"] as string, Guid.Parse(b.OperatedObjectInfo.Infos["Id"] as string), b.Id);
            }
            catch (Exception)
            {
                throw new _400Exception();
            }
        }
        public async ValueTask<IEnumerable<InvitesDto>> GetProjectInviteSessionsAsync(Guid projectId, string baseUrl)
        {
            try
            {
                var filter = f.Eq(a => a.Operation, Operation.Invite)
                    & f.Eq(a => a.OperatedObjectInfo.Infos["Id"], projectId.ToString());
                var b = await collection.Find(filter).ToListAsync();
                var re = b.Select(t => new InvitesDto()
                {
                    InviteUrl = $"{baseUrl}projectinvitedpage/{t.OperatedObjectInfo.Id.ToString()}",
                    Email = t.OperatedObjectInfo.Infos["Email"] as string
                });
                return re;
            }
            catch (Exception)
            {
                throw new _400Exception();
            }
        }
        public async ValueTask LogEntityAsync(string userId,
            Guid entityId, Operation operation, OperatedType operatedType = OperatedType.Article, Guid? operatedUserId = null,
            Dictionary<string, object> infos = null)
            => await LogAsync(Guid.Parse(userId), operation,
            new OperatedObjectInfo(entityId, operatedType, infos, operatedUserId));

        /// <summary>
        /// 获取对当前实例的推荐实例
        /// </summary>
        /// <param name="articleId"></param>
        /// <param name="op"></param>
        /// <param name="operatedType"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public IEnumerable<Guid> GetRecEntities(Guid articleId, Operation op, OperatedType operatedType, int num = 10)
        {
            var ids = collection.AsQueryable().Where(l => l.OperatedObjectInfo.Id == articleId && l.Operation == op
                && l.OperatedObjectInfo.type == operatedType && l.IsDeleted == false).Select(a => a.OperatedObjectInfo).GroupBy(a => a.OperatedUserId)
                .OrderByDescending(g => g.Count()).Take(num).AsEnumerable().Select(g => g.Key);
            return ids;

        }
        public async ValueTask<Guid> GetLastXXXedGuid(string userid, Operation op, OperatedType operatedType)
        {
            try
            {
                return await FindFirstAsync(l => l.Operator.Id == Guid.Parse(userid) && l.Operation == op
                    && l.OperatedObjectInfo.type == operatedType && l.IsDeleted == false, a => a.OperatedObjectInfo.Id,
                    Builders<Auditlog<TUserBrief>>.Sort.Descending(a => a.CreateTime));
            }
            catch (Exception)
            {
                return Guid.Empty;
            }
        }
        public async ValueTask<IEnumerable<string>> GetCollectionNames(Guid userid, Operation op, OperatedType operatedType, int num = 10)
        {
            var re = collection.Distinct(l => l.OperatedObjectInfo.Infos["colname"], l => l.Operator.Id == userid && l.Operation == op
                   && l.OperatedObjectInfo.type == operatedType && l.IsDeleted == false);
            return (await re.ToListAsync()).Cast<string>();
        }
        /// <summary>
        /// 获取某人满足特定条件的操作中被操作物体的id的集合
        /// </summary>
        /// <param name="id">操作者id</param>
        /// <param name="page">页数</param>
        /// <param name="pagesize">每页数量</param>
        /// <param name="operatedType">被操作实体类型</param>
        /// <param name="operation">操作类型</param>
        /// <param name="exFilter"></param>
        /// <returns>被操作实体的id集合</returns>
        public async ValueTask<IEnumerable<Guid>> GetXXXedIds(Guid id, int page = 0, int? pagesize = 10,
            OperatedType operatedType = OperatedType.Article, Operation operation = Operation.Star,
            FilterDefinition<Auditlog<TUserBrief>> exFilter = null)
        {
            return await GetAsync(a => a.OperatedObjectInfo.Id, page, pagesize, orderBy: "CreateTime", true,
                filterBuilder.Eq(a => a.OperatedObjectInfo.type, operatedType)
                & filterBuilder.Eq(a => a.Operation, operation)
                & filterBuilder.Eq(a => a.Operator.Id, id)
                & (exFilter ?? filterBuilder.Empty));
        }
        /// <summary>
        /// 获取特定被操作实体的满足特定条件的操作人id集合
        /// </summary>
        /// <param name="operatedId">被操作实体的id</param>
        /// <param name="page">页数</param>
        /// <param name="pagesize">每页数量</param>
        /// <param name="operatedType">被操作实体类型</param>
        /// <param name="operation">操作类型</param>
        /// <returns>操作者id集合</returns>
        public List<Guid> GetOperatorIds(Guid operatedId, int page = 0, int pagesize = 10,
            OperatedType operatedType = OperatedType.Article, Operation operation = Operation.Star)
        {
            return collection.AsQueryable().Where(a => a.OperatedObjectInfo.type == operatedType
                & a.Operation == operation & a.OperatedObjectInfo.Id == operatedId)
                .Select(a => a.Operator.Id).Skip(page * pagesize).Take(pagesize).ToList();
        }

        public async ValueTask UnXXXAsync(string userid, Guid id, Operation op, OperatedType operatedType)
        {
            await Task.Yield();
            var ids = collection.AsQueryable().Where(a => a.OperatedObjectInfo.type == operatedType
                & a.Operation == op & a.Operator.Id == Guid.Parse(userid) &
                a.OperatedObjectInfo.Id == id).Select(a => a.Id).ToList();
            if (ids.Count == 0)
            {
                throw new _400Exception("Have not stared before!");
            }
            await collection.DeleteManyAsync(Builders<Auditlog<TUserBrief>>.Filter.In(a => a.Id, ids));
        }
        /// <summary>
        /// 检验某特定操作(eg：收藏/点赞)是否之前被执行过
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="id">被操作物体的id</param>
        /// <param name="op">操作类型</param>
        /// <param name="operatedType">被操作物体类型</param>
        /// <param name="exFilter"></param>
        /// <returns></returns>
        public async ValueTask<bool> IsXXXBefore(string userid, Guid id, Operation op, OperatedType operatedType,
            FilterDefinition<Auditlog<TUserBrief>> exFilter = null)
        {
            return await AnyAsync(filterBuilder.Eq(a => a.OperatedObjectInfo.type, operatedType)
                & filterBuilder.Eq(a => a.Operation, op)
                & filterBuilder.Eq(a => a.OperatedObjectInfo.Id, id)
                & filterBuilder.Eq(a => a.Operator.Id, Guid.Parse(userid))
                & (exFilter ?? filterBuilder.Empty));
        }
        public async ValueTask<IEnumerable<Guid>> GetAccessedArticleIdsAsync(Guid userid, int page, int? pagesize) =>
            await GetAsync(l => l.OperatedObjectInfo.Id, page, pagesize, "CreateTime", true, Builders<Auditlog<TUserBrief>>.Filter
                .Eq(l => l.Operator.Id, userid) & Builders<Auditlog<TUserBrief>>.Filter
                .Eq(l => l.OperatedObjectInfo.type, OperatedType.Article));
        public async ValueTask<IEnumerable<Auditlog<TUserBrief>>> GetInfosAsync(Guid userid, int page, int pagesize)
        {
            var logs = await GetAsync(a => a, page, pagesize, "CreateTime", filter: a => a.OperatedObjectInfo.OperatedUserId == userid);
            var users = await userServices.GetAsync(u => mapper.Map<TUserBrief>(u), 0, pagesize,
                filter: Builders<TUser>.Filter.In(u => u.Id, logs.Select(l => l.Operator.Id)));
            var t = UpDateAsync(a => a.OperatedObjectInfo.OperatedUserId == userid, Builders<Auditlog<TUserBrief>>.Update
                .Set(a => a.ProcessedStamps, new List<ProcessedStamp>()));
            foreach (var item in logs)
            {
                item.Operator = users.Where(u => u.Id == item.Operator.Id).First();
            }
            await t;
            return logs;
        }
        public async ValueTask<int> GetInfoNumAsync(Guid userid)
        {
            var logs = await GetNumAsync(a => a.OperatedObjectInfo.OperatedUserId == userid && a.ProcessedStamps == null);
            return logs;
        }
        public async ValueTask LogErrorAsync(Exception err, HttpContext context)
        {
            try
            {
                var id = (Guid)(err?.Data?["id"] == null ? Guid.Empty : err?.Data?["id"]);
                var type = (OperatedType)(err?.Data?["operatedtype"] == null ? OperatedType.UnKnown : err?.Data?["operatedtype"]);
                var op = Activator.CreateInstance<TUserBrief>();
                op.Id = Guid.Parse(context.User.Identity.Name);
                var dic = new Dictionary<string, object>();
                var extradic = err?.Data?["_infos"] as Dictionary<string, object>;
                if (extradic == null)
                {
                    extradic = new Dictionary<string, object>();
                }
                foreach (var item in extradic)
                {
                    dic.Add(item.Key, item.Value);
                }
                var reader = new StreamReader(context.Request.Body);
                dic.Add("inner", err.ToString());
                dic.Add("message", err.Message);
                dic.Add("path", context.Request.Path.Value);
                dic.Add("query", context.Request.QueryString.Value);
                dic.Add("method", context.Request.Method);
                dic.Add("body", await reader.ReadToEndAsync());
                reader.Dispose();
                var log = new Auditlog<TUserBrief>()
                {
                    LogLevel = LogLevel.Error,
                    Operator = op,
                    Operation = (Operation)(err?.Data?["operation"] == null ? Operation.UnKnown : err?.Data?["operation"]),
                    OperatedObjectInfo = new OperatedObjectInfo(id, type, dic)
                };
                var t = LogAsync(log);
            }
            catch (Exception)
            {
                //logger本身报错可能会导致循环报错，暂时不处理
            }
        }
        public MessageDto<TUserBrief> MapLogToMessage(Auditlog<TUserBrief> log)
        {
            var dto = new MessageDto<TUserBrief>();
            dto.user = log.Operator;
            switch (log.Operation)
            {
                case Operation.Create:
                    break;
                case Operation.Update:
                    break;
                case Operation.Delete:
                    break;
                case Operation.Access:
                    break;
                case Operation.Login:
                    break;
                case Operation.Logout:
                    break;
                case Operation.Invite:
                    break;
                case Operation.Kick:
                    break;
                case Operation.Praise:
                    switch (log.OperatedObjectInfo.type)
                    {
                        case OperatedType.Article:
                            {
                                var article = log.OperatedObjectInfo.Infos["article"] as ArticleLog;
                                dto.message = $"点赞了您的文章<a href=\"/readarticle/{article.ManagedId}\">{article.Title}</a>";
                            }
                            break;
                        case OperatedType.Project:
                            {
                                var project = log.OperatedObjectInfo.Infos["project"] as ProjectLog;
                                dto.message = $"赞了您的项目<a href=\"/ProjectHome/{project.Id}\">{project.Name}</a>";
                            }
                            break;
                        case OperatedType.User:
                            {
                                dto.message = "赞了您";
                            }
                            break;
                        case OperatedType.UnKnown:
                            break;
                        case OperatedType.Comment:
                            {
                                var article = log.OperatedObjectInfo.Infos["article"] as ArticleLog;
                                var comment = log.OperatedObjectInfo.Infos["comment"] as CommentLog;
                                dto.message = $"赞了你在文章{article.Title}的<a href=\"/readarticle/{article.ManagedId}#{comment.Id}\">回复</a>:\n{comment.CommentMessage}";
                            }
                            break;
                        default:
                            break;
                    }
                    break;
                case Operation.UnKnown:
                    break;
                case Operation.Star:
                    switch (log.OperatedObjectInfo.type)
                    {
                        case OperatedType.Article:
                            {
                                var article = log.OperatedObjectInfo.Infos["article"] as ArticleLog;
                                dto.message = $"收藏了您的文章<a href=\"/readarticle/{article.ManagedId}\">{article.Title}</a>";
                            }
                            break;
                        case OperatedType.Project:
                            {
                                var project = log.OperatedObjectInfo.Infos["project"] as ProjectLog;
                                dto.message = $"收藏了您的项目<a href=\"/ProjectHome/{project.Id}\">{project.Name}</a>";
                            }
                            break;
                        case OperatedType.UnKnown:
                            break;
                        default:
                            break;
                    }
                    break;
                case Operation.Follow:
                    {
                        dto.message = "关注了你";
                    }
                    break;
                case Operation.Examine:
                    {
                        var article = log.OperatedObjectInfo.Infos["article"] as ArticleLog;
                        dto.message = $"审核通过了您的文章<a href=\"/readarticle/{article.ManagedId}\">{article.Title}</a>";
                    }
                    break;
                case Operation.UnExamine:
                    {
                        var article = log.OperatedObjectInfo.Infos["article"] as ArticleLog;
                        dto.message = $"您的文章<a href=\"/readarticle/{article.ManagedId}\">{article.Title}</a>需要修改后再次审核,原因:{log.OperatedObjectInfo.Infos["reason"]}";
                    }
                    break;
                case Operation.Comment:
                    {
                        var article = log.OperatedObjectInfo.Infos["article"] as ArticleLog;
                        var comment = log.OperatedObjectInfo.Infos["comment"] as CommentLog;
                        switch (log.OperatedObjectInfo.type)
                        {
                            case OperatedType.Article:
                                dto.message = $"评论了你的文章<a href=\"/readarticle/{article.ManagedId}#{comment.Id}\">{article.Title}</a>：\n{comment.CommentMessage}";
                                break;
                            case OperatedType.Comment:
                                dto.message = $"回复了你在文章{article.Title}的<a href=\"/readarticle/{article.ManagedId}#{comment.Id}\">评论</a>:\n{comment.CommentMessage}";
                                break;
                            default:
                                throw new _500Exception("错误的OperatedType");
                        }
                    }
                    break;
                default:
                    break;
            }
            return dto;
        }
    }
}
