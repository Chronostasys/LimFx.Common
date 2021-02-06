using LimFx.Business.Exceptions;
using LimFx.Business.Models;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MimeKit;
using MongoDB.Driver;
using Org.BouncyCastle.Crypto.Engines;
using RazorLight;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ToolGood.Words;

namespace LimFx.Business.Services
{
    /// <summary>
    /// 自己实现的Email类应当实现此接口
    /// </summary>
    public interface IEmail
    {
        /// <summary>
        /// 发送者的名字
        /// </summary>
        public string Sender { get; set; }
        /// <summary>
        /// 希望发送的时间
        /// </summary>
        public DateTime ExpectSendTime { get; set; }
        /// <summary>
        /// 接收者Email的集合
        /// </summary>
        public List<string> Receivers { get; set; }
        /// <summary>
        /// 主题
        /// </summary>
        public string Subject { get; set; }
        /// <summary>
        /// razor模板的相对路径
        /// </summary>
        public string RazorTemplate { get; set; }
        /// <summary>
        /// 请求发送Email的人
        /// </summary>
        public string Requester { get; set; }
    }
    /// <summary>
    /// Email服务配置选项
    /// </summary>
    public class EmailSenderOptions
    {
        private string connectionString;

        /// <summary>
        /// mongodb连接字符串
        /// </summary>
        public string ConnectionString { get{
                var inDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
                if (connectionString==null)
                {
                    return connectionString;
                }
                if (inDocker == "true")
                {
                    return connectionString.Replace("localhost", "host.docker.internal");
                }
                return connectionString;
            } set => connectionString = value; }
        /// <summary>
        /// 数据库名
        /// </summary>
        public string DatabaseName { get; set; }
        /// <summary>
        /// smtp服务器地址
        /// </summary>
        public string SmtpHost { get; set; }
        /// <summary>
        /// 发送者名称(默认值)
        /// </summary>
        public string SenderName { get; set; }
        /// <summary>
        /// smtp服务器的账号
        /// </summary>
        public string SmtpSender { get; set; }
        /// <summary>
        /// smtp服务器的密码
        /// </summary>
        public string SmtpPassword { get; set; }
        /// <summary>
        /// email数据集合的名称
        /// </summary>
        public string EmailCollectionName { get; set; }
        /// <summary>
        /// 最大允许的Email并发线程数
        /// </summary>
        public int MaxEmailThreads { get; set; } = 5;
        /// <summary>
        /// 新的Email检测间隔(只在没有Email时有效)
        /// </summary>
        public int Interval { get; set; } = 5000;
        /// <summary>
        /// 默认的Email razor模板地址
        /// </summary>
        public string TemplateDir { get; set; }
        /// <summary>
        /// 发送端口
        /// </summary>
        public int Port { get; set; } = 465;
        /// <summary>
        /// 一个用户两次发Email之间的最小间隔（单位秒）
        /// </summary>
        public int EmailInterval { get; set; } = 30;
    }

    public static class LimfxExtensions
    {
        /// <summary>
        /// 为asp.net core程序注入Email服务
        /// 默认使用mongodb存储email信息，若想使用其他的数据库，
        /// 请在调用此方法前注入实现<strong>IEmailProvider</strong>接口的Email提供服务
        /// </summary>
        /// <typeparam name="T">mongodb数据库的Email类</typeparam>
        /// <param name="collection"></param>
        /// <param name="config">设置，一般通过appsetting.json获得</param>
        /// <returns></returns>
        public static IServiceCollection AddEmailSenderService<T>(this IServiceCollection collection, IConfiguration config) where T : Entity, IEmail, ISearchAble
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (config == null) throw new ArgumentNullException(nameof(config));
            collection.Configure<EmailSenderOptions>(config);
            return collection.AddSingleton<EmailSender<T>>();
        }
        /// <summary>
        /// 手动配置Email服务的重载版本
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="options">设置Email选项的lambda</param>
        /// <returns></returns>
        public static IServiceCollection AddEmailSenderService<T>(this IServiceCollection collection, Action<EmailSenderOptions> options) where T : Entity, IEmail, ISearchAble
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (options == null) throw new ArgumentNullException(nameof(options));

            collection.Configure<EmailSenderOptions>(options);
            return collection.AddSingleton<EmailSender<T>>();
        }
    }
    public class EmailSender<T> where T : Entity, IEmail, ISearchAble
    {
        object clientLock = new object();
        object loggerLock = new object();
        private bool isServiceRunning = false;
        public RazorLightEngine engine;
        string host;
        string _sender;
        string _password;
        string templateDir;
        string senderName;
        int loggernum = 0;
        int interval;
        int emailInterval;
        int port;

        IEmailProvider<T> EmailQuery;
        ConcurrentQueue<T> emails;
        ConcurrentQueue<ProtocolLogger> availableLoggers = new ConcurrentQueue<ProtocolLogger>();
        ConcurrentQueue<SmtpClient> availableClients = new ConcurrentQueue<SmtpClient>();
        int max;
        public EmailSender(IOptions<EmailSenderOptions> options, IEmailProvider<T> emailProvider = null)
            : this(options.Value.ConnectionString, options.Value.DatabaseName,
                 options.Value.SmtpHost, options.Value.SmtpSender, options.Value.SmtpPassword,
                 options.Value.EmailCollectionName, options.Value.TemplateDir, options.Value.MaxEmailThreads, options.Value.Interval,
                 options.Value.SenderName, options.Value.Port, options.Value.EmailInterval, emailProvider)
        {
        }
        public EmailSender(string connectionString, string dataBaseName, string smtpHost, string sender, string password,
            string emailCollectionName, string templateDir, int maxThread = 5, int interval = 5000, string senderName = "example",
            int port = 429, int emailInterval = 30, IEmailProvider<T> emailProvider = null) : this()
        {
            this.senderName = senderName;
            this.templateDir = templateDir;
            this.port = port;
            this.emailInterval = emailInterval;
            host = smtpHost;
            _sender = sender;
            _password = password;
            this.interval = interval;
            max = maxThread;
            if (emailProvider==null)
            {
                EmailQuery = new MongoDbEmailProvider<T>(connectionString, dataBaseName, emailCollectionName);
            }
            else
            {
                EmailQuery = emailProvider;
            }
            emails = new ConcurrentQueue<T>();
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == null)
                return;
            Task.Factory.StartNew(StartEmailService, TaskCreationOptions.LongRunning);
        }
        async Task StartScanEmailAsync()
        {
            while (true)
            {
                var a = (await EmailQuery.GetEmailsAsync()).ToList();
                if (a.Count == 0)
                {
                    await Task.Delay(interval);
                    continue;
                }
                var eids = a.Select(e => e.Id).ToArray();
                await EmailQuery.DeleteAsync(eids);
                foreach (var item in a)
                {
                    emails.Enqueue(item);
                }
            }
        }
        public async Task StartEmailService()
        {
            Console.WriteLine("starting email service");
            var tScan = Task.Factory.StartNew(StartScanEmailAsync, TaskCreationOptions.LongRunning);
            if (isServiceRunning)
            {
                return;
            }
            isServiceRunning = true;
            List<Task> tasks = new List<Task>();
            while (true)
            {
                if (emails.Count==0)
                {
                    await Task.Delay(100);
                    continue;
                }
                int min;
                if (max > emails.Count)
                {
                    min = emails.Count;
                }
                else
                {
                    min = max;
                }
                for (int i = 0; i < min; i++)
                {
                    emails.TryDequeue(out T emailtmp);
                    if (i < tasks.Count)
                    {
                        tasks[i] = tasks[i].ContinueWith(t => SendToAsync(emailtmp),
                            TaskContinuationOptions.OnlyOnRanToCompletion).Unwrap();
                    }
                    else
                    {
                        var t = SendToAsync(emailtmp);
                        tasks.Add(t);
                    }
                }
                var count = emails.Count;
                for (int i = 0; i < count; i++)
                {
                    emails.TryDequeue(out T emailtmp);
                    tasks[i % min] = tasks[i % min].ContinueWith(t => SendToAsync(emailtmp),
                        TaskContinuationOptions.None).Unwrap();
                }
                // 采用2020/5/18谢宇晨提出的建议，去掉这句语句，这样可以提升性能。
                //await Task.WhenAll(tasks);
            }
        }
        private EmailSender()
        {
            //渲染模板
            var root = Directory.GetCurrentDirectory();
            engine = new RazorLightEngineBuilder()
                .UseFileSystemProject(root)
                .UseMemoryCachingProvider()
                .Build();
        }
        public async ValueTask QueueEmailAsync(T email, bool checkFrequency = true, bool immediate = false)
        {
            if (checkFrequency)
            {
                await EmailQuery.ThrowIfTooFrequentAsync(email, emailInterval);
            }
            email.Id = Guid.NewGuid();
            if (immediate)
            {
                emails.Enqueue(email);
                await EmailQuery.AddSentAsync(email);
                return;
            }
            
            await EmailQuery.AddAsync(email);
        }
        public async ValueTask QueueEmailAsync(List<T> emails)
        {
            await EmailQuery.AddAsync(emails.ToArray());
        }
        /// <summary>
        /// 下一个版本应该重构为使用队列发送，防止太多Email导致线程饥饿
        /// 1/7/2020
        /// 已重构
        /// 此方法变为private
        /// </summary>
        /// <param name="_receiver"></param>
        /// <param name="subject"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        private async ValueTask SendEmailWithTempplateAsync(List<string> _receiver, string subject, T model)
        {
            var content = await engine.CompileRenderAsync<T>(string.IsNullOrEmpty(model.RazorTemplate) ? templateDir : model.RazorTemplate, model);
            await SendMail(_receiver, subject, content, model.Sender);
        }
        private async Task SendToAsync(T state)
        {
            //await Task.Yield();
            var e = state;
            int i = 0;
            while (true)
            {
                try
                {
                    await SendEmailWithTempplateAsync(e.Receivers, e.Subject, e);
                    break;
                }
                catch (Exception e1)
                {
                    if (++i > 2)
                    {
                        var t = File.AppendAllTextAsync("smtperr.log", e1.ToString()+"\n");
                        break;
                    }
                    continue;
                }
            }

            //await t;
        }
        /// <summary>
        /// 这个方法在下一个版本应当改为private，没有模板的Email没有实际意义。
        /// </summary>
        /// <param name="_receiver"></param>
        /// <param name="subject"></param>
        /// <param name="content"></param>
        /// <param name="sender"></param>
        /// <returns></returns>
        private async ValueTask SendMail(List<string> _receiver, string subject, string content, string sender)
        {
            ProtocolLogger logger;
            SmtpClient client;
            //加锁的原因：必须是原子操作
            lock (loggerLock)
            {
                if (availableLoggers.IsEmpty)
                {
                    logger = new ProtocolLogger($"smtp{loggernum}.log");
                    loggernum++;
                }
                else
                {
                    availableLoggers.TryDequeue(out logger);
                }
            }
            lock (clientLock)
            {
                if (availableClients.IsEmpty)
                {
                    client = new SmtpClient(logger);
                }
                else
                {
                    availableClients.TryDequeue(out client);
                }
            }
            try
            {
                try
                {
                    await client.ConnectAsync(host, port, SecureSocketOptions.Auto);
                    await client.AuthenticateAsync(_sender, _password);
                }
                catch (Exception)
                {
                }
                MimeMessage mimeMessage = new MimeMessage();
                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = content;
                mimeMessage.Body = bodyBuilder.ToMessageBody();
                mimeMessage.From.Add(InternetAddress.Parse(string.IsNullOrEmpty(sender) ? senderName : sender));
                foreach (var item in _receiver)
                {
                    mimeMessage.To.Add(InternetAddress.Parse(item));
                }
                mimeMessage.Subject = subject;
                mimeMessage.Sender = new MailboxAddress(string.IsNullOrEmpty(sender) ? senderName : sender, string.IsNullOrEmpty(sender) ? senderName : sender);
                mimeMessage.Date = DateTime.Now;
                client.LocalDomain = string.IsNullOrEmpty(sender) ? senderName : sender;

                await client.SendAsync(mimeMessage);
                await client.DisconnectAsync(false);
                availableLoggers.Enqueue(logger);
                availableClients.Enqueue(client);
            }
            catch (Exception e)
            {
                if (client!=null)
                {
                    availableClients.Enqueue(client);
                }
                if (logger!=null)
                {
                    availableLoggers.Enqueue(logger);
                }
                throw e;
            }
        }

    }
}
