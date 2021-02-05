using LimFx.Business.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace LimFx.Business.Services
{
    /// <summary>
    /// Class which provide basic functions for database query
    /// can be inherit by sepecific database service class.
    /// </summary>
    /// <typeparam name="T">Database Class</typeparam>
    public interface IDBQueryServices<T> where T : IEntity, ISearchAble
    {
        /// <summary>
        /// 就是<strong>Builders&lt;T&gt;.Filter</strong>
        /// </summary>
        static FilterDefinitionBuilder<T> filterBuilder { get; } = Builders<T>.Filter;
        /// <summary>
        /// 数据库集合，用于自定义查询
        /// </summary>
        IMongoCollection<T> collection { get; }
        /// <summary>
        /// 获取满足特定条件的document个数
        /// </summary>
        /// <param name="filter">条件</param>
        /// <returns>文档数</returns>
        ValueTask<int> GetNumAsync(FilterDefinition<T> filter);
        /// <summary>
        /// 获取满足特定条件的document个数
        /// </summary>
        /// <param name="filter">条件</param>
        /// <returns>文档数</returns>
        ValueTask<int> GetNumAsync(Expression<Func<T, bool>> filter);
        /// <summary>
        /// 通过id获取实体
        /// </summary>
        /// <param name="Id">实体id</param>
        /// <param name="filter">条件</param>
        /// <returns></returns>
        ValueTask<T> FindFirstAsync(Guid Id, FilterDefinition<T> filter = null);
        /// <summary>
        /// 获取符合条件的第一个实体的映射类
        /// </summary>
        /// <typeparam name="TProject">映射类</typeparam>
        /// <param name="filter">条件</param>
        /// <param name="projection">映射，相当于linq中select接受的lambda</param>
        /// <param name="sort">排序方式</param>
        /// <returns>映射类</returns>
        ValueTask<TProject> FindFirstAsync<TProject>(Expression<Func<T, bool>> filter, Expression<Func<T, TProject>> projection, SortDefinition<T> sort = null);
        /// <summary>
        /// 获取符合条件的第一个实体的映射类
        /// </summary>
        /// <typeparam name="TProject">映射类</typeparam>
        /// <param name="filter">条件</param>
        /// <param name="projection">映射，相当于linq中select接受的lambda</param>
        /// <param name="sort">排序方式</param>
        /// <returns>映射类</returns>
        ValueTask<TProject> FindFirstAsync<TProject>(FilterDefinition<T> filter, Expression<Func<T, TProject>> projection, SortDefinition<T> sort = null);
        /// <summary>
        /// 插入一个或多个实体<strong>请勿插入空值</strong>
        /// </summary>
        /// <param name="t">实体</param>
        /// <returns></returns>
        ValueTask AddAsync(params T[] t);
        /// <summary>
        /// 插入一个或多个实体<strong>请勿插入空值</strong>
        /// </summary>
        /// <param name="t">实体</param>
        /// <returns></returns>
        ValueTask AddAsync(List<T> t);
        /// <summary>
        /// 根据Id删除<strong>不是真的删除，仅仅把isdelete标记为删除</strong>
        /// </summary>
        /// <param name="id">目标实例id</param>
        /// <returns></returns>
        ValueTask DeleteAsync(params Guid[] id);

        /// <summary>
        /// 获取满足特定条件的一定数量的结果
        /// </summary>
        /// <typeparam name="TProject">映射的类型</typeparam>
        /// <param name="expression">从实体类型获取映射类型的表达式</param>
        /// <param name="page">页数</param>
        /// <param name="pageSize">页长</param>
        /// <param name="filter">筛选条件</param>
        /// <param name="query">搜索词</param>
        /// <returns>查询结果</returns>
        ValueTask<IEnumerable<TProject>> GetAsync<TProject>(
            Expression<Func<T, TProject>> expression, int page, int? pageSize
            , FilterDefinition<T> filter = null, string query = null);

        /// <summary>
        /// 获取满足特定条件的一定数量的结果
        /// </summary>
        /// <typeparam name="TProject">映射的类型</typeparam>
        /// <param name="projection">从实体类型获取映射类型的表达式</param>
        /// <param name="page">页数</param>
        /// <param name="pageSize">页长</param>
        /// <param name="filter">筛选条件</param>
        /// <param name="query">搜索词</param>
        /// <returns>查询结果</returns>
        ValueTask<IEnumerable<TProject>> GetAsync<TProject>(
            ProjectionDefinition<T, TProject> projection, int page, int? pageSize
            , FilterDefinition<T> filter = null, string query = null);
        /// <summary>
        /// 获取满足特定条件的一定数量的结果
        /// </summary>
        /// <typeparam name="TProject">映射的类型</typeparam>
        /// <param name="projection">从实体类型获取映射类型的表达式</param>
        /// <param name="page">页数</param>
        /// <param name="pageSize">页长</param>
        /// <param name="sortDefinition">排序方式</param>
        /// <param name="filter">筛选条件</param>
        /// <param name="query">搜索词</param>
        /// <returns>查询结果</returns>
        ValueTask<IEnumerable<TProject>> GetAsync<TProject>(
            ProjectionDefinition<T, TProject> projection, int page, int? pageSize, SortDefinition<T> sortDefinition
            , FilterDefinition<T> filter = null, string query = null);
        /// <summary>
        /// 获取满足特定条件的一定数量的结果
        /// </summary>
        /// <typeparam name="TProject">映射的类型</typeparam>
        /// <param name="expression">从实体类型获取映射类型的表达式</param>
        /// <param name="isDescending">是否逆序</param>
        /// <param name="page">页数</param>
        /// <param name="pageSize">页长</param>
        /// <param name="orderBy">排序方式</param>
        /// <param name="filter">筛选条件</param>
        /// <param name="query">搜索词</param>
        /// <returns>查询结果</returns>
        ValueTask<IEnumerable<TProject>> GetAsync<TProject>(Expression<Func<T, TProject>> expression, int page, int? pageSize, string orderBy, bool isDescending = true, FilterDefinition<T> filter = null, string query = null);
        /// <summary>
        /// 获取满足特定条件的一定数量的结果
        /// </summary>
        /// <typeparam name="TProject">映射的类型</typeparam>
        /// <param name="expression">从实体类型获取映射类型的表达式，相当于linq中select接受的lambda</param>
        /// <param name="isDescending">是否逆序</param>
        /// <param name="page">页数</param>
        /// <param name="pageSize">页长</param>
        /// <param name="orderBy">排序方式</param>
        /// <param name="filter">筛选条件</param>
        /// <param name="query">搜索词</param>
        /// <returns>查询结果</returns>
        ValueTask<IEnumerable<TProject>> GetAsync<TProject>(Expression<Func<T, TProject>> expression, int page, int? pageSize, Expression<Func<T, object>> orderBy, bool isDescending = true, FilterDefinition<T> filter = null, string query = null);
        /// <summary>
        /// 获取满足特定条件的一定数量的结果
        /// </summary>
        /// <typeparam name="TProject">映射的类型</typeparam>
        /// <param name="expression">从实体类型获取映射类型的表达式，相当于linq中select接受的lambda</param>
        /// <param name="isDescending">是否逆序</param>
        /// <param name="page">页数</param>
        /// <param name="pageSize">页长</param>
        /// <param name="orderBy">排序方式</param>
        /// <param name="filter">筛选条件</param>
        /// <param name="query">搜索词</param>
        /// <returns>查询结果</returns>
        ValueTask<IEnumerable<TProject>> GetAsync<TProject>(Expression<Func<T, TProject>> expression, int page, int? pageSize, string orderBy, bool isDescending = true, Expression<Func<T, bool>> filter = null, string query = null);
        /// <summary>
        /// 搜索，并返回一定数量的结果
        /// <strong>此方法已弃用，使用<code>GetAsync</code>代替之</strong>
        /// </summary>
        /// <typeparam name="TProject">映射的类型</typeparam>
        /// <param name="expression">从实体类型获取映射类型的表达式，相当于linq中select接受的lambda</param>
        /// <param name="query">搜索的关键词</param>
        /// <param name="page">页数</param>
        /// <param name="pageSize">页长</param>
        /// <param name="orderBy">排序方式</param>
        /// <param name="filter">筛选条件</param>
        /// <returns>查询结果</returns>
        [Obsolete]
        ValueTask<IEnumerable<TProject>> SearchAsync<TProject>(Expression<Func<T, TProject>> expression, string query, int page = 0, int? pageSize = 10, string orderBy = "_id", FilterDefinition<T> filter = null);
        /// <summary>
        /// update某实体的特定几种属性
        /// </summary>
        /// <param name="id">update目标的id</param>
        /// <param name="updateDefinition"></param>
        /// <param name="updateTime">是否更新刷新时间</param>
        /// <returns></returns>
        ValueTask UpDateAsync(Guid id, UpdateDefinition<T> updateDefinition, bool updateTime = true);
        /// <summary>
        /// update某实体的特定几种属性
        /// </summary>
        /// <param name="filter">用于确定被更新实例的filter</param>
        /// <param name="updateDefinition"></param>
        /// <param name="updateTime">是否更新刷新时间</param>
        /// <returns></returns>
        ValueTask UpDateAsync(FilterDefinition<T> filter, UpdateDefinition<T> updateDefinition, bool updateTime = true);
        /// <summary>
        /// update某实体的特定几种属性
        /// </summary>
        /// <param name="filter">用于确定被更新实例的filter</param>
        /// <param name="updateDefinition"></param>
        /// <param name="updateTime">是否更新刷新时间</param>
        /// <returns></returns>
        ValueTask UpDateAsync(Expression<Func<T, bool>> filter, UpdateDefinition<T> updateDefinition, bool updateTime = true);
        /// <summary>
        /// 是否存在满足特定条件的文档
        /// </summary>
        /// <param name="filter">筛选条件</param>
        /// <returns></returns>
        ValueTask<bool> AnyAsync(FilterDefinition<T> filter);
        /// <summary>
        /// 是否存在满足特定条件的文档
        /// </summary>
        /// <param name="filter">筛选条件</param>
        /// <returns></returns>
        ValueTask<bool> AnyAsync(Expression<Func<T, bool>> filter);
        /// <summary>
        /// 删除满足特定条件的文档
        /// </summary>
        /// <param name="filter">筛选条件</param>
        /// <returns></returns>
        ValueTask DeleteAsync(Expression<Func<T, bool>> filter);
        /// <summary>
        /// 删除满足特定条件的文档
        /// </summary>
        /// <param name="filter">筛选条件</param>
        /// <returns></returns>
        ValueTask DeleteAsync(FilterDefinition<T> filter);
    }


    /// <summary>
    /// 使用es辅助字符串搜索的mongodb查询类
    /// </summary>
    /// <typeparam name="T">Modol Class</typeparam>
    /// <typeparam name="TEsSearch">存入es的model class</typeparam>
    public interface IEsDBQueryServices<T, TEsSearch>: IDBQueryServices<T>
        where T : IEntity, ISearchAble, ITokenedEntity
        where TEsSearch : class, ISearchAble, IGuidEntity
    {
        /// <summary>
        /// 在添加和更新实例之前应该被调用的方法
        /// Add的调用已经封装好了，只有update需要手动调用
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        void BeforeAddAndUpdate(T t);
        /// <summary>
        /// 在添加和更新实例之后应该被调用的方法
        /// Add的调用已经封装好了，只有update需要手动调用
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        ValueTask AfterAddAndUpdate(IEnumerable<T> t);


        /// <summary>
        /// 为所有符合条件的文档添加es、mongodb搜索索引
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        ValueTask IndexAllAsync(FilterDefinition<T> filter);




        /// <summary>
        /// update某实体的特定几种属性
        /// 每次更新应当update<code>T.Tokens</code>
        /// 需要手动在更新前调用<see cref="IEsDBQueryServices{T, TEsSearch}.BeforeAddAndUpdate(T)"/>
        /// 更新后 <see cref="IEsDBQueryServices{T, TEsSearch}.AfterAddAndUpdate(IEnumerable{T})"/>
        /// </summary>
        /// <param name="id">update目标的id</param>
        /// <param name="updateDefinition"></param>
        /// <param name="updateTime">是否更新刷新时间</param>
        /// <returns></returns>
        new ValueTask UpDateAsync(Guid id, UpdateDefinition<T> updateDefinition, bool updateTime = true);
        /// <summary>
        /// update某实体的特定几种属性
        /// 每次更新应当update<code>T.Tokens</code>
        /// 需要手动在更新前调用<see cref="IEsDBQueryServices{T, TEsSearch}.BeforeAddAndUpdate(T)"/>
        /// 更新后 <see cref="IEsDBQueryServices{T, TEsSearch}.AfterAddAndUpdate(IEnumerable{T})"/>
        /// </summary>
        /// <param name="filter">用于确定被更新实例的filter</param>
        /// <param name="updateDefinition"></param>
        /// <param name="updateTime">是否更新刷新时间</param>
        /// <returns></returns>
        new ValueTask UpDateAsync(FilterDefinition<T> filter, UpdateDefinition<T> updateDefinition, bool updateTime = true);
        /// <summary>
        /// update某实体的特定几种属性
        /// 每次更新应当update<code>T.Tokens</code>
        /// 需要手动在更新前调用<see cref="IEsDBQueryServices{T, TEsSearch}.BeforeAddAndUpdate(T)"/>
        /// 更新后 <see cref="IEsDBQueryServices{T, TEsSearch}.AfterAddAndUpdate(IEnumerable{T})"/>
        /// </summary>
        /// <param name="filter">用于确定被更新实例的filter</param>
        /// <param name="updateDefinition"></param>
        /// <param name="updateTime">是否更新刷新时间</param>
        /// <returns></returns>
        new ValueTask UpDateAsync(Expression<Func<T, bool>> filter, UpdateDefinition<T> updateDefinition, bool updateTime = true);
    }
}