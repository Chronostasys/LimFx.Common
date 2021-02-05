# 如何使用和https://limfx.pro 相同的后端技术？
## limfx基础公开功能  
limfx的后端技术栈为**mongodb + Asp. Net Core 3.1**，我们的开发人员在开发过程中封装了一部分通用的功能，主要包括mongodb的快速CRUD操作，多线程后台Email发送服务，脏字过滤，访问频率限制等，供其他开发者使用，以加快开发速度。这部分代码目前并没有开源，但未来有开源计划。 
## 前言
这个项目尽可能的封装了我们认为开发中常用的功能，并且加入了中文的xml注释，所以如果你觉得文档没说清楚，不必担心，代码提示里会告诉你具体的用法。![](2020-04-16-10-00-23.png) 
## 快速开始  
这些功能已经封装进入[LimFx.Common](https://www.nuget.org/packages/LimFx.Common/)nuget包，在创建新项目之后，请安装该nuget包至项目。  
  
------------------------------------  
>注意：limfx的nuget包支持.Net standard 2.1，所以请创建.Net Core 3.0版本以上的项目  
  
### 脏字过滤功能  
  
见[LimFx的BadWordService使用说明](https://www.limfx.pro/ReadArticle/60/limfx-de-badwordservice-shi-yong-shuo-ming)  
  

### EmailService使用说明  
  
见[LimFx的EmailService使用说明](https://www.limfx.pro/ReadArticle/35/limfx-de-emailservice-shi-yong-shuo-ming)  
  

### DBQueryService使用说明  

见[LimFx的DBQuryService使用说明](https://www.limfx.pro/ReadArticle/38/limfx-de-dbquryservice-shi-yong-shuo-ming)  

### EsDBQueryService使用说明
见[EsDBQueryService设计文档](https://www.limfx.pro/readarticle/330/esdbqueryservice-she-ji-wen-dang)

### RateLimiter(访问限制)使用说明  

在`StartUp.cs`的``Configure``方法中

```csharp
app.UseRateLimiter(1000, 10, 3600000, 100, 1000);
```

第一个参数是ip限制的重设时间，毫秒单位；第二个参数是在重设时间内的某一ip最大允许请求数，若超过该数字则该ip会进入黑名单；第三个参数是进入黑名单后的封禁时间，毫秒单位；第四个是总请求限制数；第五个参数是总请求限制的重设时间，单位为毫秒，该期间内总请求数超过第四个参数的话触发保护，在重设前不再接受请求。  

### LimFxErrorHandler使用文档
见[此处](https://www.limfx.pro/ReadArticle/457/limfxerrorhandler-shi-yong-wen-dang)
  
### EnhancedRateLimiter(强化版访问限制)使用说明

- 此方法可以通过在controller或路径方法上添加`RateLimitAttribute`来详细自定义**某路径**在一定时间内被一个ip的上限访问次数
- 使用此方法需要使用asp的身份认证服务，代码中应当先注入授权和认证服务，例如：
  ```csharp
    services.AddAuthentication(op=> 
    {
        op.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    }).AddCookie();
    services.AddAuthorization(op =>
    {
        op.InvokeHandlersAfterFailure = false;
    });
  ```

#### 设置
在`StartUp.cs`的``ConfigureServices``方法中

```csharp
services.AddEnhancedRateLimiter(ClaimTypes.UserData, 1000, 1000);
```
* 第一个参数是用于鉴别不同用户的claim，默认为``ClaimTypes.UserData``。建议往这个claim里保存能唯一标识用户的信息，例如ip  
* 第二个参数是重设所有请求记录的间隔，毫秒单位  
* 第三个参数是黑名单屏蔽时间，毫秒单位  

#### 使用

1. 在controller上使用  
   ![](2020-03-05-14-23-20.png)
   注意：
   1. `EnhancedRateLimiter`使用asp身份认证层实现，而`RateLimitAttribute`继承自`AuthorizeAttribute`，所以使用它默认带有`AuthorizeAttribute`的效果。即该功能不应该使用在不需要任何身份认证的路径。
   2. 同1，`RateLimitAttribute`实际上可以完全代替`AuthorizeAttribute`，因此下边两个图等效
   ![](2020-03-05-14-29-25.png)
   ![](2020-03-05-14-29-41.png)
2. 在endpoint方法上使用
   ![](2020-03-05-14-33-53.png)
3. 在controller上使用，计数器仍然然会对每个方法单独计数。例如1中的配置，它会使任何一个用户无法在1秒内对articlecontroller中的某一个endpoint方法发出三个以上的请求，但是该用户对articlecontroller所有endpoints的一秒内请求总和可以超过三次


### LimFx性能benchmark  

[LimFx的benchmark结果](https://www.limfx.pro/ReadArticle/32/limfx-de-benchmark-jie-guo)  
  


