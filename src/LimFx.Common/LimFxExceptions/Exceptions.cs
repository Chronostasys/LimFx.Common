using System;
using System.Collections;
using System.Collections.Generic;

namespace LimFx.Business.Exceptions
{
    public class HttpException : Exception
    {
        public override IDictionary Data { get; }
        public int StatusCode { get; }
        public HttpException(string msg = "Internal Server Error!",
            Exception exception = null, int statusCode = 500,
            Dictionary<string, object> dic = null) : base(msg, exception)
        {
            StatusCode = statusCode;
            Data = dic;
        }
    }
    public class _429Exception : HttpException
    {
        public _429Exception(string msg = "Too many requests!",
            Exception exception = null,
            Dictionary<string, object> dic = null) : base(msg, exception, 429, dic)
        {
        }
    }
    public class _403Exception : HttpException
    {
        public _403Exception(string msg = "You don't have the authority to do this!",
                        Exception exception = null,
                        Dictionary<string, object> dic = null) : base(msg, exception, 403, dic)
        {
        }
    }
    public class _401Exception : HttpException
    {
        public _401Exception(string msg,
                        Exception exception = null,
                        Dictionary<string, object> dic = null) : base(msg, exception, 401, dic)
        {
        }
    }
    public class _400Exception : HttpException
    {
        public _400Exception(string msg = "Limfx: Bad Input!",
                        Exception exception = null,
                        Dictionary<string, object> dic = null) : base(msg, exception, 400, dic)
        {
        }
    }
    public class _404Exception : HttpException
    {
        public _404Exception(string msg = "Limfx: Not Found!",
                        Exception exception = null,
                        Dictionary<string, object> dic = null) : base(msg, exception, 404, dic)
        {
        }
    }
    public class IllegleBotPathException : HttpException
    {
        public IllegleBotPathException(string msg = "Limfx: bad path",
                        Exception exception = null,
                        Dictionary<string, object> dic = null) : base(msg, exception, 400, dic)
        {
        }
    }
    public class _500Exception : HttpException
    {
        public _500Exception(string msg = "Internal server error!\r\nIf this bug occurs frequently, please contact admin for help!",
                        Exception exception = null,
                        Dictionary<string, object> dic = null) : base(msg, exception, 500, dic)
        {
        }
    }
    public class ServiceStartUpException : Exception
    {
        public override IDictionary Data { get; }
        public ServiceStartUpException(string msg, Exception exception = null, Dictionary<string, object> dic = null) : base(msg, exception)
        {
            Data = dic;
        }
    }
    public class Error
    {
        public string errorMessage { get; set; }
        public Error(string msg)
        {
            errorMessage = msg;
        }
    }
}
