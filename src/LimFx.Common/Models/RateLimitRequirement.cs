using Microsoft.AspNetCore.Authorization;

namespace LimFx.Business.Services
{
    public class RateLimitRequirement : IAuthorizationRequirement
    {
        public int MaxNum { get; set; }
        public RateLimitRequirement(int maxNum) => MaxNum = maxNum;
    }
}
