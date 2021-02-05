using AbotEsge.Util;
using LimFx.Business.Exceptions;
using Microsoft.AspNetCore.Builder;
using System.Collections.Concurrent;
using System.Net;
using System.Timers;

namespace LimFx.Business.Services
{
    public static class RateLimiter
    {
        static BloomFilter<IPAddress> blackList = new BloomFilter<IPAddress>(20000, 0.001f);
        static ConcurrentDictionary<IPAddress, int> requesterInfos = new ConcurrentDictionary<IPAddress, int>();
        static Timer resettimer;
        static Timer reqReset;
        static Timer unBlocTimer;
        static int req = 0;
        static void ResetRequests(object sender, ElapsedEventArgs e)
        {
            requesterInfos.Clear();
        }
        static void ResetBlackList(object sender, ElapsedEventArgs e)
        {
            blackList.Clear();
        }
        /// <summary>
        /// Limite request rate
        /// 时间单位全是毫秒
        /// </summary>
        /// <param name="app"></param>
        /// <param name="reset">计算请求数量的周期</param>
        /// <param name="maxRequest">请求周期内一个ip的最大请求数</param>
        /// <param name="blockTime">违规封禁时间</param>
        /// <param name="maxReq">触发保护的请求数</param>
        /// <param name="period">保护机制的循环周期</param>
        /// <returns></returns>
        public static IApplicationBuilder UseRateLimiter(this IApplicationBuilder app,
            int reset = 1000, int maxRequest = 10, int blockTime = 3600000, int maxReq = 100, int period = 1000)
        {
            reqReset = new Timer(period);
            reqReset.Elapsed += ReqReset_Elapsed;
            unBlocTimer = new Timer(blockTime);
            unBlocTimer.Elapsed += ResetBlackList;

            resettimer = new Timer(reset);
            resettimer.Elapsed += ResetRequests;
            resettimer.Start();
            unBlocTimer.Start();
            reqReset.Start();
            app.Use(async (context, next) =>
            {
                var ip = context.Connection.RemoteIpAddress;
                if (!requesterInfos.TryAdd(ip, 0))
                {
                    requesterInfos[ip]++;
                    if (requesterInfos[ip] > maxRequest)
                    {
                        blackList.Add(ip);
                    }

                }
                if (maxReq < req++ || blackList.Contains(ip))
                {
                    throw new _429Exception();
                }
                await next();
            });

            return app;
        }

        private static void ReqReset_Elapsed(object sender, ElapsedEventArgs e)
        {
            req = 0;
        }
    }
}
