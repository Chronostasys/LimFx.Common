using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ToolGood.Words;

namespace LimFx.Business.Services
{
    public class BadWordService
    {
        public IllegalWordsSearch StringSearch { get; }
        public BadWordService()
        {
            StringSearch = new IllegalWordsSearch();
            StringSearch.UseIgnoreCase = true;
            StringSearch.SetKeywords(censoredWords);
        }



        /// <summary>
        /// a function iterate through
        /// all string or string array property of a class
        /// and replace all bad words in it
        /// this method is using reflection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        /// <param name="type"></param>
        /// <param name="fuckAll">是否检查所有可能字段，为true的时候无视属性的<see cref="BadWordIgnoreAttribute"/></param>
        /// <returns></returns>
        public T BadwordsFucker<T>(T t, Type type = null, bool fuckAll = false)
        {
            type = type == null ? typeof(T) : type;
            if (t == null)
            {
                return default(T);
            }
            foreach (var item in type.GetProperties())
            {
                object val = new object();
                try
                {
                    val = item.GetValue(t);
                }
                catch (Exception)
                {
                    continue;
                }
                if (val == null)
                {
                    continue;
                }
                if (!fuckAll)
                {
                    var attr = item.GetCustomAttribute<BadWordIgnoreAttribute>();
                    if (attr != null)
                    {
                        continue;
                    }
                }
                if (val is string)
                {
                    val = StringSearch.Replace(val as string);
                    try
                    {
                        item.SetValue(t, val);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
                else if(val is string[])
                {
                    var value = val as string[];
                    for (int i = 0; i < value.Length; i++)
                    {
                        value[i] = StringSearch.Replace(value[i]);
                    }
                }
                else if(val is IList<string>)
                {
                    var strings = val as IList<string>;
                    for (int i = 0; i < strings.Count; i++)
                    {
                        strings[i] = StringSearch.Replace(strings[i]);
                    }
                }
            }
            return t;
        }
        /// <summary>
        /// a function iterate through
        /// all string or string array property of a class
        /// and its property classes
        /// and replace all bad words in it
        /// this method is using reflection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        /// <param name="type"></param>
        /// <param name="maxDepth">最大深度，负数视为无穷</param>
        /// <param name="fuckAll">是否检查所有可能字段，为true的时候无视属性的<see cref="BadWordIgnoreAttribute"/></param>
        /// <returns></returns>
        public T BadwordsFuckerDeep<T>(T t, Type type = null, int maxDepth = -1, bool fuckAll = false)
        {
            int j = 0;
            type = type == null ? typeof(T) : type;
            if (t == null)
            {
                return default(T);
            }
            foreach (var item in type.GetProperties())
            {
                object val = new object();
                try
                {
                    val = item.GetValue(t);
                }
                catch (Exception)
                {
                    continue;
                }
                if (val == null)
                {
                    continue;
                }
                if (!fuckAll)
                {
                    var attr = item.GetCustomAttribute<BadWordIgnoreAttribute>();
                    if (attr != null)
                    {
                        continue;
                    }
                }
                if (val is string)
                {
                    var value = StringSearch.Replace((string)val);
                    item.SetValue(t, value);
                }
                else if (val is string[])
                {
                    var value = (string[])val;
                    for (int i = 0; i < value.Length; i++)
                    {
                        value[i] = StringSearch.Replace(value[i]);
                    }
                }
                else if (val is IList<string>)
                {
                    var value = (IList<string>)val;
                    for (int i = 0; i < value.Count; i++)
                    {
                        value[i] = StringSearch.Replace(value[i]);
                    }
                }
                else if (j < maxDepth | maxDepth < 0)
                {
                    j++;
                    BadwordsFuckerDeep(val, item.PropertyType, maxDepth - j);
                    j--;
                }
            }
            return t;
        }
        public void AddBadWords(params string[] badwords)
        {
            censoredWords.AddRange(badwords);
        }
        public void SetBadWords(List<string> badWords)
        {
            censoredWords = badWords;
        }

        public List<string> censoredWords
        {
            get;
            set;
        } = new List<string>()
                {
                    "gosh",
                    "drat",
                    "darn*",
                    "艹",
                    "你妈",
                    "傻逼",
                    "废物",
                    "cao",
                    "婊子",
                    "肥猪",
                    "妈的",
                    "fuck",
                    "shit"
                };
    }
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class BadWordIgnoreAttribute : Attribute
    {
    }
}
