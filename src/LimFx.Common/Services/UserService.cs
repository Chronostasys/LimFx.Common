using AutoMapper;
using EzPasswordValidator.Validators;
using HashLibrary;
using LimFx.Business.Exceptions;
using LimFx.Business.Extensions;
using LimFx.Business.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Nest;
using OpenXmlPowerTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
namespace LimFx.Business.Services
{
    /// <summary>
    /// 2019/10/22
    /// manage the Users
    /// </summary>
    public class UserService<TUser, TUserSearch> : EsDBQueryServices<TUser, TUserSearch>, IUserService<TUser> 
        where TUser : IEntity, IUser, ISearchAble, IPraiseAble, ITokenedEntity, new()
        where TUserSearch : class, IGuidEntity, ISearchAble
    {
        public UserService(IBaseDbSettings settings, string collectionName, IElasticClient client, string esIndex, IMapper mapper)
            : base(settings.ConnectionString, settings.DatabaseName, collectionName, client, esIndex, mapper)
        {
            createIndex:
            try
            {
                collection.Indexes.CreateOne(new CreateIndexModel<TUser>(Builders<TUser>.IndexKeys.Ascending(l => l.Email), new CreateIndexOptions() { Unique = true }));
                //Collation _caseInsensitiveCollation = new Collation("en", strength: CollationStrength.Primary);
                //collection.Indexes.CreateOne(new CreateIndexModel<TUser>(Builders<TUser>.IndexKeys.Ascending(l => l.KeyWords),
                //    new CreateIndexOptions() { Collation = _caseInsensitiveCollation }));
                //collection.Indexes.CreateOne(new CreateIndexModel<TUser>(Builders<TUser>.IndexKeys.Ascending(l => l.Followers)));
                //collection.Indexes.CreateOne(new CreateIndexModel<TUser>(Builders<TUser>.IndexKeys.Ascending(l => l.Awesomes)));
                //collection.Indexes.CreateOne(new CreateIndexModel<TUser>(Builders<TUser>.IndexKeys.Ascending(l => l.Roles)));
                //collection.Indexes.CreateOne(new CreateIndexModel<TUser>(Builders<TUser>.IndexKeys.Ascending(l => l.IsEmailConfirmed)));
                //collection.Indexes.CreateOne(new CreateIndexModel<TUser>(Builders<TUser>.IndexKeys.Ascending(l => l.Score)));
            }
            catch (Exception)
            {
                collection.Indexes.DropAll();
                goto createIndex;
            }
        }
        public override ValueTask<IEnumerable<TProject>> GetAsync<TProject>(Expression<Func<TUser, TProject>> expression, int page, int? pageSize, FilterDefinition<TUser> filter = null, string query = null)
        {
            filter = filter ?? Builders<TUser>.Filter.Empty;
            filter &= filterBuilder.Eq(u => u.IsEmailConfirmed, true);
            return base.GetAsync(expression, page, pageSize, filter, query);
        }
        public override ValueTask<IEnumerable<TProject>> GetAsync<TProject>(Expression<Func<TUser, TProject>> expression, int page, int? pageSize, string orderBy, bool isDescending = true, FilterDefinition<TUser> filter = null, string query = null)
        {
            filter = filter ?? Builders<TUser>.Filter.Empty;
            filter &= filterBuilder.Eq(u => u.IsEmailConfirmed, true);
            return base.GetAsync(expression, page, pageSize, orderBy, isDescending, filter, query);
        }
        public static FilterDefinition<TUser> userFilter = filterBuilder.AnyEq(u => u.Roles, Roles.User);
        public async ValueTask ValidateEmailAsync(string email)
        {
            try
            {
                await collection.UpdateOneAsync(filterBuilder.Eq(b => b.Email, email), Builders<TUser>.Update
                    .Set(b => b.IsEmailConfirmed, true));
            }
            catch (Exception)
            {
                throw new _401Exception("Cannot find the email!");
            }
        }
        public async ValueTask PraiseUserAsync(Guid id)
        {
            await collection.IncreAsync(id, u => u.Awesomes);
        }
        public async ValueTask UnPraiseUserAsync(Guid id)
        {
            await collection.DecreAsync(id, u => u.Awesomes);
        }
        public async ValueTask FollowUserAsync(Guid userId, Guid id)
        {
            var t1 = collection.IncreAsync(u => u.Id == userId, u => u.Follows);
            var t2 = collection.IncreAsync(id, u => u.Followers);
            await t1;
            await t2;
        }
        public async ValueTask UnFollowUserAsync(Guid userId, Guid id)
        {
            var t1 = collection.DecreAsync(u => u.Id == userId, u => u.Follows);
            var t2 = collection.DecreAsync(id, u => u.Followers);
            await t1;
            await t2;
        }
        PasswordValidator validator = new PasswordValidator();
        public void ValidatePassword(string password)
        {
            validator.SetLengthBounds(8, 50);
            validator.AddCheck(EzPasswordValidator.Checks.CheckTypes.Numbers);
            validator.AddCheck(EzPasswordValidator.Checks.CheckTypes.Letters);
            if (!validator.Validate(password))
            {
                throw new _401Exception("Password is not strong enough!");
            }
        }
        /// <summary>
        /// 2019/10/22
        /// Add a user to table
        /// usually used to registe
        /// </summary>
        /// <returns></returns>
        public async ValueTask<TUser> AddUserAsync(TUser user, bool isanonymous = false)
        {
            ValidatePassword(user.PassWordHash);
            if (string.IsNullOrEmpty(user.Email) ||
                string.IsNullOrEmpty(user.Name) ||
                string.IsNullOrEmpty(user.PassWordHash))
            {
                throw new _401Exception("register data should not be null!");
            }
            if (!isanonymous)
            {
                user.IsEmailConfirmed = false;
            }
            else
            {
                user.IsEmailConfirmed = true;
            }
            user.AvatarUrl = $"https://cdn.limfx.pro/img/ran/{Math.Abs(user.Id.GetHashCode()%993)+1}";
            var a = HashedPassword.New(user.PassWordHash);
            user.PassWordHash = a.Hash + a.Salt;
            user.SecurityStamp = DateTime.UtcNow.ToString();
            try
            {
                await AddAsync(user);
            }
            catch (Exception e)
            {

                throw new _401Exception("Email has been taken!", e);
            }
            return user;
        }
        public async ValueTask<TUser> GetUserByEmailAsync(string email)
        {
            try
            {
                return (await (await collection.FindAsync(u => u.Email == email)).FirstAsync());
            }
            catch (Exception e)
            {
                throw new _401Exception("Cannot find the email!", e);
            }
        }
        public async ValueTask<TUser> LogOutAsync(HttpContext httpContext)
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return await SignInAsAnonymous(httpContext);
        }
        public async ValueTask<TUser> SignInAsAnonymous(HttpContext httpContext)
        {

            var user = new TUser();
            user.Name = "Anonymous";
            user.Email = Guid.NewGuid().ToString();
            user.PassWordHash = "anonymous123";
            user.Roles = new List<string>() { Roles.Anonymous };
            user.IsEmailConfirmed = true;
            user.SecurityStamp = DateTime.UtcNow.ToString();

            await AddUserAsync(user, true);
            user.PassWordHash = "anonymous123";
            await SignInAsync(user, httpContext);
            return user;
        }
        public async ValueTask ChangePasswordAsync(string email, string password)
        {
            ValidatePassword(password);
            var a = HashLibrary.HashedPassword.New(password);
            var passWordHash = a.Hash + a.Salt;
            await UpDateAsync(u => u.Email == email, Builders<TUser>.Update.Set(u => u.PassWordHash, passWordHash)
                .Set(u=>u.SecurityStamp, DateTime.UtcNow.ToString()));
        }
        /// <summary>
        /// 2019/10/21 created 
        /// signIn function
        /// </summary>
        /// <param name="user">should at least contain email and password!</param>
        /// <param name="httpContext">current httpcontext</param>
        /// <returns>indicates whether the signin operation is successful</returns>
        public async ValueTask<bool> SignInAsync(TUser user, HttpContext httpContext, bool rememberMe = true, bool validatePassword = true)
        {
            var u = new TUser();
            try
            {
                u = await collection.Find(a => a.Email == user.Email).FirstAsync();
            }
            catch (Exception)
            {

                throw new _401Exception("Cannot find the Email!");
            }

            if (!u.IsEmailConfirmed/* && services.env.IsDevelopment()*/)
            {
                throw new _403Exception("Email Not Confirmed, or you are reseting the password");
            }
            bool auth = true;
            if (validatePassword)
            {
                var hash = u.PassWordHash.Substring(0, 32);
                var salt = u.PassWordHash.Substring(32);
                var h = new HashedPassword(hash, salt);
                auth = h.Check(user.PassWordHash);
            }
            if (auth)
            {
                await SignInWithoutCheckAsync(httpContext, u, rememberMe);
                return true;
            }
            else
            {

                throw new _401Exception("Password and email do not match!");
            }
        }
        public async ValueTask<bool> ChallengeAsync(string email, string password)
        {


            try
            {
                var pwdhash = await FindFirstAsync(u => u.Email == email, u => u.PassWordHash);
                var hash = pwdhash.Substring(0, 32);
                var salt = pwdhash.Substring(32);
                var h = new HashedPassword(hash, salt);
                return h.Check(password);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async ValueTask SignInWithoutCheckAsync(HttpContext httpContext, TUser u, bool rememberMe)
        {
            var authProperties = new AuthenticationProperties
            {
                //AllowRefresh = <bool>,
                // Refreshing the authentication session should be allowed.

                //ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10),
                // The time at which the authentication ticket expires. A 
                // value set here overrides the ExpireTimeSpan option of 
                // CookieAuthenticationOptions set with AddCookie.

                IsPersistent = rememberMe
                // Whether the authentication session is persisted across 
                // multiple requests. When used with cookies, controls
                // whether the cookie's lifetime is absolute (matching the
                // lifetime of the authentication ticket) or session-based.

                //IssuedUtc = <DateTimeOffset>,
                // The time at which the authentication ticket was issued.

                //RedirectUri = <string>
                // The full path or absolute URI to be used as an http 
                // redirect response value.
            };
            //u.SecurityStamp = RandomString(10);
            var claims = new List<Claim>()
                {
                    new Claim(ClaimTypes.Email, u.Email),
                    new Claim(ClaimTypes.Name, u.Id.ToString()),
                    new Claim(ClaimTypes.NameIdentifier, u.Name.ToString()),
                    new Claim(ClaimTypes.UserData, httpContext.Connection.RemoteIpAddress?.ToString()??"null"),
                    new Claim("SecurityStamp",u.SecurityStamp??DateTime.UtcNow.ToString()),
                    new Claim("sub", u.Id.ToString())
                };
            for (int i = 0; i < u.Roles.Count; i++)
            {
                claims.Add(new Claim(ClaimTypes.Role, u.Roles[i]));
            }
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await collection.UpdateOneAsync(t=>t.Id==u.Id,Builders<TUser>.Update.Set(t => t.SecurityStamp, u.SecurityStamp));
            await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity), authProperties);
        }

        private static Random random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Range(1, length).Select(_ => chars[random.Next(chars.Length)]).ToArray());
        }
        public async ValueTask<bool> CheckAuth(HttpContext context, string role)
        {
            var fb = filterBuilder;
            return await AnyAsync(fb.Eq(u => u.Id, Guid.Parse(context.User.Identity.Name))
                & fb.AnyEq(u => u.Roles, role));
        }
    }
    public class BasicUserService<TUser>: DBQueryServicesSlim<TUser> where TUser:IUser,new()
    {
        PasswordValidator validator = new PasswordValidator();
        public BasicUserService(IBaseDbSettings settings, string collectionName)
            : base(settings.ConnectionString, settings.DatabaseName, collectionName)
        {
            createIndex:
            try
            {
                collection.Indexes.CreateOne(new CreateIndexModel<TUser>(Builders<TUser>.IndexKeys.Ascending(l => l.Email), new CreateIndexOptions() { Unique = true }));
                //Collation _caseInsensitiveCollation = new Collation("en", strength: CollationStrength.Primary);
                //collection.Indexes.CreateOne(new CreateIndexModel<TUser>(Builders<TUser>.IndexKeys.Ascending(l => l.KeyWords),
                //    new CreateIndexOptions() { Collation = _caseInsensitiveCollation }));
                //collection.Indexes.CreateOne(new CreateIndexModel<TUser>(Builders<TUser>.IndexKeys.Ascending(l => l.Followers)));
                //collection.Indexes.CreateOne(new CreateIndexModel<TUser>(Builders<TUser>.IndexKeys.Ascending(l => l.Awesomes)));
                //collection.Indexes.CreateOne(new CreateIndexModel<TUser>(Builders<TUser>.IndexKeys.Ascending(l => l.Roles)));
                //collection.Indexes.CreateOne(new CreateIndexModel<TUser>(Builders<TUser>.IndexKeys.Ascending(l => l.IsEmailConfirmed)));
                //collection.Indexes.CreateOne(new CreateIndexModel<TUser>(Builders<TUser>.IndexKeys.Ascending(l => l.Score)));
            }
            catch (Exception)
            {
                collection.Indexes.DropAll();
                goto createIndex;
            }
        }
        public override ValueTask<IEnumerable<TProject>> GetAsync<TProject>(Expression<Func<TUser, TProject>> expression, int page, int? pageSize, FilterDefinition<TUser> filter = null)
        {
            filter = filter ?? Builders<TUser>.Filter.Empty;
            filter &= filterBuilder.Eq(u => u.IsEmailConfirmed, true);
            return base.GetAsync(expression, page, pageSize, filter);
        }
        public override ValueTask<IEnumerable<TProject>> GetAsync<TProject>(Expression<Func<TUser, TProject>> expression, int page, int? pageSize, string orderBy, bool isDescending = true, FilterDefinition<TUser> filter = null)
        {
            filter = filter ?? Builders<TUser>.Filter.Empty;
            filter &= filterBuilder.Eq(u => u.IsEmailConfirmed, true);
            return base.GetAsync(expression, page, pageSize, orderBy, isDescending, filter);
        }
        public static FilterDefinition<TUser> userFilter = filterBuilder.AnyEq(u => u.Roles, Roles.User);
        public async ValueTask ValidateEmailAsync(string email)
        {
            try
            {
                await collection.UpdateOneAsync(filterBuilder.Eq(b => b.Email, email), Builders<TUser>.Update
                    .Set(b => b.IsEmailConfirmed, true));
            }
            catch (Exception)
            {
                throw new _401Exception("Cannot find the email!");
            }
        }
        public void ValidatePassword(string password)
        {
            validator.SetLengthBounds(8, 50);
            validator.AddCheck(EzPasswordValidator.Checks.CheckTypes.Numbers);
            validator.AddCheck(EzPasswordValidator.Checks.CheckTypes.Letters);
            if (!validator.Validate(password))
            {
                throw new _401Exception("Password is not strong enough!");
            }
        }
        /// <summary>
        /// 2019/10/22
        /// Add a user to table
        /// usually used to registe
        /// </summary>
        /// <returns></returns>
        public async ValueTask<TUser> AddUserAsync(TUser user, bool isanonymous = false)
        {
            ValidatePassword(user.PassWordHash);
            if (string.IsNullOrEmpty(user.Email) ||
                string.IsNullOrEmpty(user.Name) ||
                string.IsNullOrEmpty(user.PassWordHash))
            {
                throw new _401Exception("register data should not be null!");
            }
            if (user.Roles is null)
            {
                throw new NullReferenceException($"Roles shall not be null!");
            }
            if (!isanonymous)
            {
                user.IsEmailConfirmed = false;
            }
            else
            {
                user.IsEmailConfirmed = true;
            }
            user.AvatarUrl = $"https://cdn.limfx.pro/img/ran/{Math.Abs(user.Id.GetHashCode() % 993) + 1}";
            var a = HashedPassword.New(user.PassWordHash);
            user.PassWordHash = a.Hash + a.Salt;
            user.SecurityStamp = DateTime.UtcNow.ToString();
            try
            {
                await AddAsync(user);
            }
            catch (Exception e)
            {

                throw new _401Exception("Email has been taken!", e);
            }
            return user;
        }
        public async ValueTask<TUser> GetUserByEmailAsync(string email)
        {
            try
            {
                return (await (await collection.FindAsync(u => u.Email == email)).FirstAsync());
            }
            catch (Exception e)
            {
                throw new _401Exception("Cannot find the email!", e);
            }
        }
        public async ValueTask<TUser> LogOutAsync(HttpContext httpContext)
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return await SignInAsAnonymous(httpContext);
        }
        public async ValueTask<TUser> SignInAsAnonymous(HttpContext httpContext)
        {

            var user = new TUser();
            user.Name = "Anonymous";
            user.Email = Guid.NewGuid().ToString();
            user.PassWordHash = "anonymous123";
            user.Roles = new List<string>() { Roles.Anonymous };
            user.IsEmailConfirmed = true;
            user.SecurityStamp = DateTime.UtcNow.ToString();

            await AddUserAsync(user, true);
            user.PassWordHash = "anonymous123";
            await SignInAsync(user, httpContext);
            return user;
        }
        public async ValueTask ChangePasswordAsync(string email, string password)
        {
            ValidatePassword(password);
            var a = HashLibrary.HashedPassword.New(password);
            var passWordHash = a.Hash + a.Salt;
            await UpDateAsync(u => u.Email == email, Builders<TUser>.Update.Set(u => u.PassWordHash, passWordHash)
                .Set(u => u.SecurityStamp, DateTime.UtcNow.ToString()));
        }
        /// <summary>
        /// 2019/10/21 created 
        /// signIn function
        /// </summary>
        /// <param name="user">should at least contain email and password!</param>
        /// <param name="httpContext">current httpcontext</param>
        /// <returns>indicates whether the signin operation is successful</returns>
        public async ValueTask<bool> SignInAsync(TUser user, HttpContext httpContext, bool rememberMe = true, bool validatePassword = true)
        {
            var u = new TUser();
            try
            {
                u = await collection.Find(a => a.Email == user.Email).FirstAsync();
            }
            catch (Exception)
            {

                throw new _401Exception("Cannot find the Email!");
            }

            if (!u.IsEmailConfirmed/* && services.env.IsDevelopment()*/)
            {
                throw new _403Exception("Email Not Confirmed, or you are reseting the password");
            }
            bool auth = true;
            if (validatePassword)
            {
                var hash = u.PassWordHash.Substring(0, 32);
                var salt = u.PassWordHash.Substring(32);
                var h = new HashedPassword(hash, salt);
                auth = h.Check(user.PassWordHash);
            }
            if (auth)
            {
                await SignInWithoutCheckAsync(httpContext, u, rememberMe);
                return true;
            }
            else
            {

                throw new _401Exception("Password and email do not match!");
            }
        }
        public async ValueTask<bool> ChallengeAsync(string email, string password)
        {


            try
            {
                var pwdhash = await FindFirstAsync(u => u.Email == email, u => u.PassWordHash);
                var hash = pwdhash.Substring(0, 32);
                var salt = pwdhash.Substring(32);
                var h = new HashedPassword(hash, salt);
                return h.Check(password);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async ValueTask SignInWithoutCheckAsync(HttpContext httpContext, TUser u, bool rememberMe)
        {
            var authProperties = new AuthenticationProperties
            {
                //AllowRefresh = <bool>,
                // Refreshing the authentication session should be allowed.

                //ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10),
                // The time at which the authentication ticket expires. A 
                // value set here overrides the ExpireTimeSpan option of 
                // CookieAuthenticationOptions set with AddCookie.

                IsPersistent = rememberMe
                // Whether the authentication session is persisted across 
                // multiple requests. When used with cookies, controls
                // whether the cookie's lifetime is absolute (matching the
                // lifetime of the authentication ticket) or session-based.

                //IssuedUtc = <DateTimeOffset>,
                // The time at which the authentication ticket was issued.

                //RedirectUri = <string>
                // The full path or absolute URI to be used as an http 
                // redirect response value.
            };
            //u.SecurityStamp = RandomString(10);
            var claims = new List<Claim>()
                {
                    new Claim(ClaimTypes.Email, u.Email),
                    new Claim(ClaimTypes.Name, u.Id.ToString()),
                    new Claim(ClaimTypes.NameIdentifier, u.Name.ToString()),
                    new Claim(ClaimTypes.UserData, httpContext.Connection.RemoteIpAddress?.ToString()??"null"),
                    new Claim("SecurityStamp",u.SecurityStamp??DateTime.UtcNow.ToString()),
                    new Claim("sub", u.Id.ToString())
                };
            for (int i = 0; i < u.Roles.Count; i++)
            {
                claims.Add(new Claim(ClaimTypes.Role, u.Roles[i]));
            }
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await collection.UpdateOneAsync(t => t.Id == u.Id, Builders<TUser>.Update.Set(t => t.SecurityStamp, u.SecurityStamp));
            await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity), authProperties);
        }

        private static Random random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Range(1, length).Select(_ => chars[random.Next(chars.Length)]).ToArray());
        }
        public async ValueTask<bool> CheckAuth(HttpContext context, string role)
        {
            var fb = filterBuilder;
            return await AnyAsync(fb.Eq(u => u.Id, Guid.Parse(context.User.Identity.Name))
                & fb.AnyEq(u => u.Roles, role));
        }
    }
}
