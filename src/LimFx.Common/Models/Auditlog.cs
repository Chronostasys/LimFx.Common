using System;
using System.Collections.Generic;

namespace LimFx.Business.Models
{
    /// <summary>
    /// 这个类极其灵活，能够用来记录点赞、收藏、评论、阅读、邀请、审核等等.....同时也能从这些信息中生成用户的消息盒信息
    /// </summary>
    public class Auditlog<TUserBrief> : Entity
    {
        public LogLevel LogLevel { get; set; }
        public List<ProcessedStamp> ProcessedStamps { get; set; }
        public TUserBrief Operator { get; set; }
        public Operation Operation { get; set; }
        public OperatedObjectInfo OperatedObjectInfo { get; set; }
    }
    /// <summary>
    /// 千万<strong>不要在这个枚举中间加新的选项</strong>
    /// 否则以前存进mongodb的信息的意义可能会改变。
    /// 要加新选项在最后边加
    /// </summary>
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Fatal
    }
    /// <summary>
    /// 千万<strong>不要在这个枚举中间加新的选项</strong>
    /// 否则以前存进mongodb的信息的意义可能会改变。
    /// 要加新选项在最后边加
    /// </summary>
    public enum Operation
    {
        Create,
        Update,
        Delete,
        Access,
        Login,
        Logout,
        Invite,
        Kick,
        Praise,
        UnKnown,
        Star,
        Follow,
        Examine,
        UnExamine,
        Comment
    }
    public class OperatedObjectInfo
    {
        public OperatedObjectInfo(Guid id, OperatedType opType, Dictionary<string, object> _infos, Guid? userid = null)
        {
            OperatedUserId = userid.GetValueOrDefault(Guid.Empty);
            type = opType;
            Id = id;
            Infos = _infos??new Dictionary<string, object>();
        }
        public Guid Id { get; set; }
        public OperatedType type { get; set; }
        /// <summary>
        /// such as author/owner etc.
        /// </summary>
        public Guid OperatedUserId { get; set; }
        public Dictionary<string, object> Infos { get; set; }
    }
    /// <summary>
    /// 千万<strong>不要在这个枚举中间加新的选项</strong>
    /// 否则以前存进mongodb的信息的意义可能会改变。
    /// 要加新选项在最后边加
    /// </summary>
    public enum OperatedType
    {
        Article,
        Project,
        User,
        UnKnown,
        Comment,
        ArticleCol
    }
    public class ProcessedStamp
    {
        public string Processor { get; set; }
        public DateTime ProcessedTime { get; set; }
    }
}
