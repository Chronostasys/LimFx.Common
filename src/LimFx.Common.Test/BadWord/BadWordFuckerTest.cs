using DocumentFormat.OpenXml.Office2010.ExcelAc;
using LimFx.Business.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace LimFx.Common.Test
{
    [TestClass]
    public class BadWordFuckerTest
    {
        BadWordService badWordService;
        [TestInitialize]
        public void Init()
        {
            badWordService = new BadWordService();
        }

        [TestMethod]
        public void TestBadWordFucker()
        {
            var temp = badWordService.BadwordsFucker(GetNewBad(), fuckAll: false);
            Assert.AreEqual("****", temp.bad1);
            Assert.AreNotEqual("****", temp.bad2);
            Assert.AreEqual("****", temp.bad3);
            Assert.AreEqual("****", temp.bad4[0]);
            Assert.AreEqual("****", temp.bad5[0]);
        }
        [TestMethod]
        public void TestBadWordFuckerDeep()
        {
            var temp1 = badWordService.BadwordsFuckerDeep(new Bad { innerBad = GetNewBad(), innerGood = GetNewBad() },fuckAll:false);
            var temp = temp1.innerBad;
            Assert.AreEqual("****", temp.bad1);
            Assert.AreEqual("****", temp.bad3);
            Assert.AreEqual("****", temp.bad4[0]);
            Assert.AreEqual("****", temp.bad5[0]);
            temp = temp1.innerGood;
            Assert.AreNotEqual("****", temp.bad1);
            Assert.AreNotEqual("****", temp.bad3);
            Assert.AreNotEqual("****", temp.bad4[0]);
            Assert.AreNotEqual("****", temp.bad5[0]);
        }
        Bad GetNewBad()
        {
            return new Bad
            {
                bad1 = "fuck",
                bad2 = "fuCK",
                bad3 = "FUck",
                bad4 = new[] { "fuck", "fuCK" },
                bad5 = new List<string> { "fuck" }
            };
        }
    }
    class Bad
    {
        public string bad1 { get; set; }
        [BadWordIgnore]
        public string bad2 { get; set; }
        public string bad3 { get; set; }
        public string[] bad4 { get; set; }
        public List<string> bad5 { get; set; }
        public Bad innerBad { get; set; }
        [BadWordIgnore]
        public Bad innerGood { get; set; }
    }
}
