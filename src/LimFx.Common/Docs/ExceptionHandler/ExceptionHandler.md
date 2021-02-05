# LimFxErrorHandler使用文档
  
本功能从属于[LimFx.Common](https://www.limfx.pro/ReadArticle/34/ru-he-shi-yong-he-httpslimfxpro-xiang-tong-de-hou-duan-ji-shu)  
  
本功能为asp开发者提供一种便利的错误处理模式，能够直接根据asp**处理请求时抛出的**不同异常返回对应的状态码和错误提示信息。  

## 优势
- 轻量，高效
- 可自定义
- 对没出现异常的请求没有任何影响

## 使用
ErrorHandler功能被封装位asp的中间件，该方法提供两种重载，分别是
```csharp
public static IApplicationBuilder UseLimFxExceptionHandler(this IApplicationBuilder app,
    IErrorLogger errorLogger, Func<Exception,int> statusCodeSelector=null);
public static IApplicationBuilder UseLimFxExceptionHandler(this IApplicationBuilder app,
    Func<Exception, int> statusCodeSelector = null);
```
最简单的使用方法只需要在Startup.cs中的`Configure`方法里加入  
```csharp
app.UseLimFxExceptionHandler();
```  


第一种重载需要接收一个errorlogger，用于输出errorlog，errorlogger需要有`LogErrorAsync`方法.
```csharp
public interface IErrorLogger
{
    ValueTask LogErrorAsync(Exception err, HttpContext context);
}
```
两个重载中的可选参数`statusCodeSelector`是接收报错的`exception`之后，根据`exception`类型返回
应当设定的http状态码的委托。如果不传入函数，那么中间件会使用默认的状态码设定函数：
```csharp
context.Response.StatusCode = err switch
{
    _403Exception e => StatusCodes.Status403Forbidden,
    _400Exception e => StatusCodes.Status400BadRequest,
    _401Exception e => StatusCodes.Status401Unauthorized,
    _500Exception e => StatusCodes.Status500InternalServerError,
    _404Exception e => StatusCodes.Status404NotFound,
    _429Exception e => StatusCodes.Status429TooManyRequests,
    IllegleBotPathException e => context.Response.StatusCode = StatusCodes.Status200OK,
    _ => StatusCodes.Status500InternalServerError,
};
```
除了被判断为500的错误，其他错误会返回它们携带的message作为错误提示信息，例如：
```json
{
  "type": "https://tools.ietf.org/html/rfc7231",
  "title": "Unauthorized",
  "status": 401,
  "traceId": "0HLVNRL5M6E6O:0000000C",
  "errors": {
    "errorMessage": "Password is not strong enough!"
  }
}
```
而500错误统一返回`internal server error`作为错误信息。  

## 版本要求
- netstandard 2.1或以上
- asp.net core 3.0及以上

