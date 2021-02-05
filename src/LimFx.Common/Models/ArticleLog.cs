using System;
using System.Collections.Generic;
using System.Text;

namespace LimFx.Business.Models
{
    /// <summary>
    /// 这些xxxLog类是用来存用于用户messagebox的信息的
    /// 比如某人赞了你的文章，log就会在info里存一个articlelog对象，这样生成message时就知道article的基本信息
    /// 其它的xxxlog类类比
    /// </summary>
    public class ArticleLog
    {
        public long ManagedId { get; set; }
        public Guid Id { get; set; }
        public string Title { get; set; }
    }
}
