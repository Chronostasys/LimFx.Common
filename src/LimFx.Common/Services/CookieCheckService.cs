using LimFx.Business.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using OpenXmlPowerTools;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using System.Linq;
using LimFx.Business.Exceptions;
using Microsoft.AspNetCore.Authentication;


//Finish this part with the help of docs
//https://docs.microsoft.com/en-us/aspnet/core/security/authentication/cookie?tabs=aspnetcore2x&view=aspnetcore-3.1

namespace LimFx.Business.Services
{
    public class CookieCheckService<TUser> : CookieAuthenticationEvents where TUser:IUser
    {
        readonly IDBQueryServicesSlim<TUser> userService;
        public CookieCheckService(IDBQueryServicesSlim<TUser> userService)
        {
            this.userService = userService;
        }
        public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
        {
            var userPrincipal = context.Principal;
            var userId = Guid.Parse(userPrincipal.Identity.Name);
            var StampInDb =await userService.FindFirstAsync(t=>t.Id==userId,t=>t.SecurityStamp);
            var StampInCookie = (from item in userPrincipal.Claims
                                 where item.Type == "SecurityStamp"
                                 select item.Value).FirstOrDefault();
            if (StampInCookie != StampInDb)
            {
                context.RejectPrincipal();

                await context.HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);
            }
        }
    }
}
