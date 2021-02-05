using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace LimFx.Business.Services
{
    public interface IErrorLogger
    {
        ValueTask LogErrorAsync(Exception err, HttpContext context);
    }
}