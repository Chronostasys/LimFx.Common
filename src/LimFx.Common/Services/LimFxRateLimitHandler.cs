using AbotEsge.Util;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Timers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Routing;
using LimFx.Business.Exceptions;
using Microsoft.AspNetCore.Http;

namespace LimFx.Business.Services
{
    public class LimFxRateLimitHandler : AuthorizationHandler<RateLimitRequirement>
    {
        static BloomFilter<string> blackList = new BloomFilter<string>(20000, 0.001f);
        static ConcurrentDictionary<string, int> requesterInfos = new ConcurrentDictionary<string, int>();
        static Timer resettimer;
        static Timer unBlocTimer;
        string blockBaseOn;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="blockBaseOn"> 判断不同用户的claim，建议使用ip</param>
        /// <param name="resetMs">重设所有请求为0的间隔，毫秒单位</param>
        /// <param name="blockTimeMs">黑名单持续时间，毫秒单位</param>
        public LimFxRateLimitHandler(string blockBaseOn = ClaimTypes.UserData, double resetMs = 1000, double blockTimeMs = 1000)
        {
            resettimer = new Timer();
            resettimer.Interval = resetMs;
            resettimer.Elapsed += (s,e) => requesterInfos.Clear();
            unBlocTimer = new Timer(blockTimeMs);
            this.blockBaseOn = blockBaseOn;
            unBlocTimer.Elapsed+= (s, e) => blackList.Clear();
            resettimer.Start();
            unBlocTimer.Start();
        }
        public override Task HandleAsync(AuthorizationHandlerContext context)
        {
            return base.HandleAsync(context);
        }
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RateLimitRequirement requirement)
        {
            var endpoint = context.Resource as DefaultHttpContext;
            int maxRequest = requirement.MaxNum;
            if (!context.User.Identity.IsAuthenticated)
            {
                throw new _403Exception();
            }
            try
            {
                var ip = context.User.FindFirst(blockBaseOn).Value;
                var key = ip + endpoint.Request.Path.Value;
                if (!requesterInfos.TryAdd(key, 0))
                {
                    requesterInfos[key]++;
                    if (requesterInfos[key] > maxRequest)
                    {
                        blackList.Add(ip);
                    }
                }
                if (blackList.Contains(ip))
                {
                    throw new _429Exception();
                }
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
            catch (System.Exception e)
            {
                if (e is _429Exception)
                {
                    throw e;
                }
                throw new _400Exception(exception: e);
            }
        }
    }
}
