using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;

namespace LimFx.Business.Services
{
    public static class RateLimitExtension
    {
        /// <summary>
        /// 注入访问保护服务
        /// 此服务与<strong>RateLimitAttribute</strong>配套使用，
        /// 能够限定一定时间内一个用户对特定路径的最大允许访问次数
        /// </summary>
        /// <param name="services"></param>
        /// <param name="blockBaseOn"> 判断不同用户的claim，建议使用ip</param>
        /// <param name="resetMs">重设所有请求为0的间隔，毫秒单位</param>
        /// <param name="blockTimeMs">黑名单持续时间，毫秒单位</param>
        /// <returns></returns>
        public static IServiceCollection AddEnhancedRateLimiter(this IServiceCollection services, 
            string blockBaseOn = ClaimTypes.UserData, double resetMs = 1000, double blockTimeMs = 1000)
        {
            services.AddSingleton<IAuthorizationPolicyProvider, LimfxPolicyProvider>();
            services.AddSingleton<IAuthorizationHandler, LimFxRateLimitHandler>(sp=> 
            {
                return new LimFxRateLimitHandler(blockBaseOn, resetMs, blockTimeMs);
            });
            return services;
        }

    }
}
