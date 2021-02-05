using LimFx.Business.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LimFx.Business.Services
{
    public interface IEmailProvider<T> where T : Entity, IEmail
    {
        ValueTask AddSentAsync(params T[] emails);
        ValueTask AddAsync(params T[] emails);
        ValueTask DeleteAsync(params Guid[] ids);
        ValueTask<IEnumerable<T>> GetEmailsAsync();
        ValueTask ThrowIfTooFrequentAsync(T email, int emailInterval);
    }
}