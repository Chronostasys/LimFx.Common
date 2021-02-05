using LimFx.Business.Exceptions;
using LimFx.Business.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Abstractions;

namespace LimFx.Common.Test
{
    [TestClass]
    public class LimFxRateLimiterTest
    {
        ActionExecutingContext ctx127;
        Mock<ConnectionInfo> connectionInfoMock;
        Mock<HttpContext> httpCtxMock;
        [TestInitialize]
        public void Init()
        {
            httpCtxMock = new Mock<HttpContext>();
            connectionInfoMock = new Mock<ConnectionInfo>();
            connectionInfoMock.Setup(i => i.RemoteIpAddress).Returns(System.Net.IPAddress.Parse("127.0.0.1"));
            httpCtxMock.SetupGet(ctx => ctx.Connection).Returns(connectionInfoMock.Object);
            httpCtxMock.Setup(ctx => ctx.Request.Path).Returns(new PathString("/demo"));
            var modelState = new ModelStateDictionary();
            modelState.AddModelError("name", "invalid");
            var actionContext = new ActionContext(
                httpCtxMock.Object,
                Mock.Of<RouteData>(),
                Mock.Of<ActionDescriptor>(),
                modelState
            );
            var actionExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                Mock.Of<Controller>()
            );
            ctx127 = actionExecutingContext;
        }
        [TestMethod]
        public async Task TestRateLimitBlock()
        {
            LimFxRateLimitAttribute.Init(1000, 3000);
            connectionInfoMock.Setup(i => i.RemoteIpAddress).Returns(System.Net.IPAddress.Parse("127.0.0.1"));
            var rt = new LimFxRateLimitAttribute(3);
            for (int i = 0; i < 3; i++)
            {
                rt.OnActionExecuting(ctx127);
            }
            Assert.ThrowsException<_429Exception>(() => rt.OnActionExecuting(ctx127));
            await Task.Delay(1500);
            Assert.ThrowsException<_429Exception>(() => rt.OnActionExecuting(ctx127));
            await Task.Delay(1500);
            rt.OnActionExecuting(ctx127);
        }
        [TestMethod]
        public async Task TestRateLimitMultipleUser()
        {
            LimFxRateLimitAttribute.Init(1000, 3000);
            connectionInfoMock.Setup(i => i.RemoteIpAddress).Returns(System.Net.IPAddress.Parse("127.0.0.1"));
            var rt = new LimFxRateLimitAttribute(3);
            for (int i = 0; i < 3; i++)
            {
                rt.OnActionExecuting(ctx127);
            }
            Assert.ThrowsException<_429Exception>(() => rt.OnActionExecuting(ctx127));
            await Task.Delay(1500);
            Assert.ThrowsException<_429Exception>(() => rt.OnActionExecuting(ctx127));
            connectionInfoMock.Setup(i => i.RemoteIpAddress).Returns(System.Net.IPAddress.Parse("127.0.0.2"));
            rt.OnActionExecuting(ctx127);
        }
        [TestMethod]
        public void TestRateLimitMultipleEndPoints()
        {
            LimFxRateLimitAttribute.Init(1000, 3000);
            httpCtxMock.Setup(ctx => ctx.Request.Path).Returns(new PathString("/demo"));
            connectionInfoMock.Setup(i => i.RemoteIpAddress).Returns(System.Net.IPAddress.Parse("127.0.0.1"));
            var rt = new LimFxRateLimitAttribute(3);
            for (int i = 0; i < 3; i++)
            {
                rt.OnActionExecuting(ctx127);
            }
            httpCtxMock.Setup(ctx => ctx.Request.Path).Returns(new PathString("/demo1"));
            rt.OnActionExecuting(ctx127);
        }
    }
}
