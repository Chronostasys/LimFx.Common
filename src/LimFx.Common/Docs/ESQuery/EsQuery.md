# EsDBQueryService设计文档


本类来源于[LimFx.Common](https://www.limfx.pro/ReadArticle/34/ru-he-shi-yong-he-httpslimfxpro-xiang-tong-de-hou-duan-ji-shu)

>Es(ElasticSearch)提供了极其强大的字符串搜索功能，但是很多开发人员却因为没学习过es的用法，或不了解es而使用mongodb自带的搜索。然而mongodb自带的搜索是必须全词匹配的，意味着如果你的英文单词拼写错了很可能搜不出对应的结果。并且mongodb的community版本对于中文搜索的支持极差，基本只能全字匹配。  
本类为开发者提供了便捷使用es协同mongodb进行搜索的方法，而使用者不需要学习es的任何知识。  

## 优势
- 高度封装，使用者不需要了解es的任何知识
- 巧妙设计，搜索功能可以配合mongodb的**任意**filter、sort一起使用，就和直接用mongodb一样
- 高度可自定义，所有方法都可以在继承后重写，并且提供接口
- 性能优秀

## 设计细节详解
首先，`EsDBQueryService`是继承`DBQueryServiceSlim`的，并且重写了其中的所有`AddAsync`、`DeleteAsync`方法，添加了一个特殊的、增加了`query`参数的`GetAsync`方法（用于搜索）  
`AddAsync`相比基类，在调用基类`AddAsync`前对Entity的`SearchAbleString`属性使用es进行`Tokenize`，比如`我爱中国`会被es的smartcn分析器拆解为`['我','我爱','中国','爱','中','国']`这些token，然后我们会把这些Token存入Entity的`Tokens`属性中。  
在调用基类`AddAsync`后，我们会把Entity映射为需要存入es中的EsSearchEntity（EsSearchEntity也是泛型）然后存入es
然后再delete时，我们会同时删除es里的对应实体。  
然后在`GetAsync`中，搜索可以分为两种情况：  
1. 搜索时，orderby参数传入null或者空字符串，那么
   1. 搜索结果将按照相关性排序
   2. 搜索将会使用es
   3. 细节：先从es中获取按相关性排序的实例id，然后去mongodb查询到对应实例，再按照es中id顺序排序
   4. 这种情况建议不要使用其它filter，尽管这么做不会报错
2. 若orderby传入非空的字符串，那么
   1. 搜索按照orderby排序
   2. 搜索不使用es的查询功能
   3. 细节：将query使用es化为tokens，对entity的tokens使用anyinfilter查询，结果以orderby排序，直接返回结果
   4. 这种情况可以随意使用filter

由此可见，以搜索相关性排序的搜索性能稍差一点点。
并且，使用者需要手动重写update方法，在update之前调用`BeforeAddAndUpdate(T t)`，update之后调用`AfterAddAndUpdate(T t)`，update的时候更新Tokens。例如：
```cs
public async ValueTask UpDateProjectAsync(Project researchProject)
{
    await BeforeAddAndUpdate(researchProject);
    var ud = Builders<Project>.Update
        .Set(b => b.Name, researchProject.Name)
        .Set(b => b.Description, researchProject.Description)
        .Set(b => b.KeyWords, researchProject.KeyWords)
        .Set(b => b.SearchAbleString, researchProject.SearchAbleString)
        .Set(b => b.PrefixName, researchProject.PrefixName?.Trim())
        .Set(b => b.Tokens, researchProject.Tokens);//设置tokens
    if (!string.IsNullOrEmpty(researchProject.CoverUrl))
    {
        ud = ud.Set(b => b.CoverUrl, researchProject.CoverUrl);
    }
    await UpDateAsync(researchProject.Id, ud);
    await AfterAddAndUpdate(researchProject);
}
```  
其它使用方面与[DBQueryServiceSlim](https://www.limfx.pro/ReadArticle/38/limfx-de-dbquryservice-shi-yong-shuo-ming)**完全一样**
