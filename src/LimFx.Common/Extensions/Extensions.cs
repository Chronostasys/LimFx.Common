using AutoMapper;
using LimFx.Business.Exceptions;
using LimFx.Business.Models;
using LimFx.Business.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Mime;
using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.Net.Http.Headers;

namespace LimFx.Business.Extensions
{
    public static class Extensions
    {
        private static FindOptions findOptions = new FindOptions { Collation = new Collation("en", strength: CollationStrength.Primary) };
        public static async ValueTask IncreAsync<T>(this IMongoCollection<T> collection,
            Guid id, Expression<Func<T, int>> projection, int step = 1) where T : IEntity
        {
            await Task.Yield();
            await collection.UpdateManyAsync(t=>t.Id == id, Builders<T>.Update.Inc(projection, step));
        }
        public static async ValueTask IncreAsync<T>(this IMongoCollection<T> collection,
            Guid id, Expression<Func<T, float>> projection, float step = 1) where T : IEntity
        {
            await Task.Yield();
            await collection.UpdateManyAsync(t => t.Id == id, Builders<T>.Update.Inc(projection, step));
        }
        public static async ValueTask DecreAsync<T>(this IMongoCollection<T> collection,
            Guid id, Expression<Func<T, int>> projection, int step = 1) where T : IEntity
        {
            await Task.Yield();
            await collection.UpdateManyAsync(t => t.Id == id, Builders<T>.Update.Inc(projection, -step));
        }
        public static async ValueTask DecreAsync<T>(this IMongoCollection<T> collection,
            FilterDefinition<T> filter, Expression<Func<T, int>> projection, int step = 1) where T : IEntity
        {
            await Task.Yield();
            await collection.UpdateManyAsync(filter, Builders<T>.Update.Inc(projection, -step));
        }
        public static async ValueTask DecreAsync<T>(this IMongoCollection<T> collection,
            Guid id, Expression<Func<T, float>> projection, float step = 1) where T : IEntity
        {
            await Task.Yield();
            await collection.UpdateManyAsync(t => t.Id == id, Builders<T>.Update.Inc(projection, -step));
        }
        public static async ValueTask IncreAsync<T>(this IMongoCollection<T> collection,
            Expression<Func<T, bool>> filter, Expression<Func<T, int>> projection, int step = 1) where T : IEntity
        {
            await Task.Yield();
            await collection.UpdateManyAsync(filter, Builders<T>.Update.Inc(projection, step));
        }
        public static async ValueTask DecreAsync<T>(this IMongoCollection<T> collection,
            Expression<Func<T, bool>> filter, Expression<Func<T, int>> projection, int step = 1) where T : IEntity
        {
            await Task.Yield();
            await collection.UpdateManyAsync(filter, Builders<T>.Update.Inc(projection, -step));
        }
        public static FilterDefinition<T> FilterKeyWords<T>(this FilterDefinition<T> filter,
            string keys) where T : IKeyWords
        {
            try
            {
                return filter & (keys == null ? Builders<T>.Filter.Empty : Builders<T>.Filter.AnyIn(t => t.KeyWords, JsonSerializer.Deserialize<IEnumerable<string>>(keys)));
            }
            catch (Exception e)
            {

                throw new _400Exception(exception: e);
            }
        }
        public static int CalculateScore(this IScorable scorable)
        {
            var baseScore = scorable.Stars * 50 + scorable.Awesomes * 5 + scorable.AdminScore + 2;
            scorable.Score = baseScore / ((DateTime.UtcNow - scorable.CreateTime).Days + 1);
            return scorable.Score;
        }
        public static IFindFluent<TDocument, TDocument> FindIgnoreCase<TDocument>(this IMongoCollection<TDocument> collection, FilterDefinition<TDocument> filter)
        {
            return collection.Find(filter, findOptions);
        }
        public static IApplicationBuilder UseLimFxExceptionHandler(this IApplicationBuilder app,
            IErrorLogger errorLogger)
        {
            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {

                    context.Response.ContentType = "text/html";

                    var exceptionHandlerPathFeature =
                        context.Features.Get<IExceptionHandlerPathFeature>();
                    var err = exceptionHandlerPathFeature.Error;



                    await errorLogger.LogErrorAsync(err, context);
                    await StatuscodeSetter(context, err);
                });
            });
            return app;
        }
        public static async ValueTask StatuscodeSetter(HttpContext context, Exception err=null)
        {

            err = err ?? context.Features.Get<IExceptionHandlerPathFeature>().Error;
            context.Response.StatusCode = err switch
            {
                HttpException e => e.StatusCode,
                _ => StatusCodes.Status500InternalServerError,
            };
            var limfxErr = new LimFxError()
            {
                title = ReasonPhrases.GetReasonPhrase(context.Response.StatusCode),
                status = context.Response.StatusCode,
                traceId = context.TraceIdentifier,
                errors = new Dictionary<string, string>
                {
                    {"errorMessage", context.Response.StatusCode == StatusCodes.Status500InternalServerError ?
                        "internal server error!" : err.Message}
                }
            };
            context.Response.ContentType = MediaTypeNames.Application.Json;
            await context.Response.WriteAsync(JsonSerializer.Serialize(limfxErr));

            await context.Response.WriteAsync(new string(' ', 512)); // IE padding
        }
        public static IApplicationBuilder UseLimFxExceptionHandler(this IApplicationBuilder app)
        {
            app.UseExceptionHandler(errorApp =>
            {
                
                errorApp.Run(async context =>
                {
                    await StatuscodeSetter(context);
                });
            });
            return app;
        }
    }
}
