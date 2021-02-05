using LimFx.Business.Dto;
using LimFx.Business.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LimFx.Business.Services
{
    public interface ILogService<TUserBrief>: IDBQueryServicesSlim<Auditlog<TUserBrief>> where TUserBrief : IUserBrief
    {
        ValueTask CheckInvitedAsync(string email);
        ValueTask<(string email, Guid projectId, Guid logid)> CheckProjectInviteSessionIdAsync(Guid sessionId);
        ValueTask CheckProjectSessionExistedAsync(string[] emails, Guid projectId);
        ValueTask<IEnumerable<Guid>> GetAccessedArticleIdsAsync(Guid userid, int page, int? pagesize);
        ValueTask<IEnumerable<string>> GetCollectionNames(Guid userid, Operation op, OperatedType operatedType, int num = 10);
        ValueTask<int> GetInfoNumAsync(Guid userid);
        ValueTask<IEnumerable<Auditlog<TUserBrief>>> GetInfosAsync(Guid userid, int page, int pagesize);
        ValueTask<Guid> GetLastXXXedGuid(string userid, Operation op, OperatedType operatedType);
        ValueTask<IEnumerable<Auditlog<TUserBrief>>> GetLogsAsync(int page = 0, int pageSize = 10);
        List<Guid> GetOperatorIds(Guid operatedId, int page = 0, int pagesize = 10, OperatedType operatedType = OperatedType.Article, Operation operation = Operation.Star);
        ValueTask<IEnumerable<InvitesDto>> GetProjectInviteSessionsAsync(Guid projectId, string baseUrl);
        IEnumerable<Guid> GetRecEntities(Guid articleId, Operation op, OperatedType operatedType, int num = 10);
        ValueTask<IEnumerable<Guid>> GetXXXedIds(Guid id, int page = 0, int? pagesize = 10, OperatedType operatedType = OperatedType.Article, Operation operation = Operation.Star, FilterDefinition<Auditlog<TUserBrief>> exFilter = null);
        ValueTask<bool> IsXXXBefore(string userid, Guid id, Operation op, OperatedType operatedType, FilterDefinition<Auditlog<TUserBrief>> exFilter = null);
        ValueTask LogAsync(Auditlog<TUserBrief> auditlog);
        ValueTask LogAsync(Guid userId, Operation operation, OperatedObjectInfo objectInfo, LogLevel logLevel = LogLevel.Info);
        ValueTask LogAsync(List<Auditlog<TUserBrief>> auditlogs, string email);
        ValueTask LogEntityAsync(string userId, Guid entityId, Operation operation, OperatedType operatedType = OperatedType.Article, Guid? operatedUserId = null, Dictionary<string, object> infos = null);
        MessageDto<TUserBrief> MapLogToMessage(Auditlog<TUserBrief> log);
        ValueTask<IEnumerable<Auditlog<TUserBrief>>> SearchLogsAsync(string email, DateTime start, DateTime end, int page = 0, int pageSize = 10);
        ValueTask UnXXXAsync(string userid, Guid id, Operation op, OperatedType operatedType);
    }
}