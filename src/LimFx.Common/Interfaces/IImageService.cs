using LimFx.Business.Dto;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace LimFx.Business.Services
{
    /// <summary>
    /// todo: 添加删除图片的服务
    /// 这一部分需要前端配合
    /// </summary>
    public enum SaveImageType
    {
        UserIcon,
        ArticleCover,
        ArticelContent,
        ProjectCover
    }
    public interface IImageService
    {
        public string baseUrl { get; }
        void DeleteArticleFiles(string userId, Guid articleId, string[] excepts = null);
        ValueTask<ImageTokenDto> GenerateQiNiuImageToken(string userid, Guid entityId, SaveImageType saveImageType, HttpContext httpContext=null, string filename = null);
        string GetUserFiles(string userid);
        ValueTask<ImgResultDto> SaveImgAsync(string userid, SaveImageType saveImageType, IFormFileCollection files, string identity = null);
    }
}