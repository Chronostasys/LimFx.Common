using AbotEsge.Util;
using LimFx.Business.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Timers;

namespace LimFx.Business.Services
{
    public class LimFxRateLimitAttribute : ActionFilterAttribute, IDisposable
    {
        static BloomFilter<IPAddress> blackList = new BloomFilter<IPAddress>(20000, 0.001f);
        static ConcurrentDictionary<string, int> requesterInfos = new ConcurrentDictionary<string, int>();
        static Timer resettimer;
        static Timer unBlocTimer;
        private readonly int _value;

        public LimFxRateLimitAttribute(int rate)
        {
            _value = rate;
        }
        static void ResetRequests(object sender, ElapsedEventArgs e)
        {
            requesterInfos.Clear();
        }
        static void ResetBlackList(object sender, ElapsedEventArgs e)
        {
            blackList.Clear();
        }
        public static void Init(double resetMs = 1000, double blockTimeMs = 1000)
        {
            resettimer?.Dispose();
            unBlocTimer?.Dispose();
            requesterInfos.Clear();
            blackList.Clear();
            unBlocTimer = new Timer(blockTimeMs);
            unBlocTimer.Elapsed += ResetBlackList;
            resettimer = new Timer(resetMs);
            resettimer.Elapsed += ResetRequests;
            resettimer.Start();
            unBlocTimer.Start();
        }
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var ip = context.HttpContext.Connection.RemoteIpAddress;
            var key = ip.ToString() + context.HttpContext.Request.Path.ToString();
            if (!requesterInfos.TryAdd(key, 0))
            {
                requesterInfos[key]++;
                if (requesterInfos[key] >= _value)
                {
                    blackList.Add(ip);
                }

            }
            if (blackList.Contains(ip))
            {
                throw new _429Exception();
            }
            base.OnActionExecuting(context);
        }

        public void Dispose()
        {
            resettimer?.Dispose();
            unBlocTimer?.Dispose();
        }
    }
}
