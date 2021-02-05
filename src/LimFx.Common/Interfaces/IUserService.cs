using LimFx.Business.Models;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LimFx.Business.Services
{
    public interface IUserService<TUser>: IDBQueryServices<TUser> 
        where TUser : IEntity, IUser, ISearchAble, IPraiseAble
    {
        ValueTask<TUser> AddUserAsync(TUser user, bool isanonymous = false);
        ValueTask ChangePasswordAsync(string email, string password);
        ValueTask<bool> CheckAuth(HttpContext context, string role);
        ValueTask FollowUserAsync(Guid userId, Guid id);
        ValueTask<TUser> GetUserByEmailAsync(string email);
        ValueTask<TUser> LogOutAsync(HttpContext httpContext);
        ValueTask PraiseUserAsync(Guid id);
        ValueTask<TUser> SignInAsAnonymous(HttpContext httpContext);
        ValueTask<bool> SignInAsync(TUser user, HttpContext httpContext, bool rememberMe = true, bool validatePassword = true);
        ValueTask UnFollowUserAsync(Guid userId, Guid id);
        ValueTask UnPraiseUserAsync(Guid id);
        ValueTask ValidateEmailAsync(string email);
        void ValidatePassword(string password);
    }
}