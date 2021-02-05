using Nest;
using System;
using System.Collections.Generic;

namespace LimFx.Business.Models
{
    public interface IArticle<TAuthor>:IEntity,ISearchAble,IKeyWords
        where TAuthor:IUserBrief
    {
        int AdminScore { get; set; }
        string ArticleAbstract { get; set; }
        Guid ArticleId { get; set; }
        TAuthor AuthorBrief { get; set; }
        int Awesomes { get; set; }
        CompletionField CompletionField { get; set; }
        string Content { get; set; }
        string CoverUrl { get; set; }
        Guid DraftOrPublishId { get; set; }
        bool Examined { get; set; }
        ExamineState ExamineState { get; set; }
        bool IsDraft { get; set; }
        Guid ProjectId { get; set; }
        int ReaderNum { get; set; }
        bool Saved { get; set; }
        int Score { get; set; }
        int Stars { get; set; }
        string Title { get; set; }
        IEnumerable<string> Tokens { get; set; }
        ArticleVisibility Visibility { get; set; }
        string Bgm { get; set; }
    }
    public enum ExamineState
    {
        Examining,
        Failed,
        Passed
    }
    public enum ArticleVisibility
    {
        everyone,
        projectMember,
        onlyMyself,
    }
}