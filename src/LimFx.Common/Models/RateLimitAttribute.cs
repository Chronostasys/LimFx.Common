using Microsoft.AspNetCore.Authorization;
using System;

namespace LimFx.Business.Services
{
    public class RateLimitAttribute : AuthorizeAttribute
    {
        const string POLICY_PREFIX = "RateLimit";
        public int MaxNum
        {
            get
            {
                if (int.TryParse(Policy.Substring(POLICY_PREFIX.Length), out var maxnum))
                {
                    return maxnum;
                }
                throw new InvalidOperationException();
            }
            set
            {
                Policy = $"{POLICY_PREFIX}{value}";
            }
        }
        public RateLimitAttribute(int maxNum) => MaxNum = maxNum;
    }
}
