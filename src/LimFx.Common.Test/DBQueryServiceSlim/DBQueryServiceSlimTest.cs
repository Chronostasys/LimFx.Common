using LimFx.Business.Models;
using LimFx.Business.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LimFx.Common.Test
{
    class TestEntity:Entity
    {
        public string Test { get; set; }
    }
    [TestClass]
    public class DBQueryServiceSlimTest:TestBase
    {
        DBQueryServicesSlim<TestEntity> querySlimService;
        string colname = "testslimCol";
        public DBQueryServiceSlimTest()
        {

        }
        [TestInitialize]
        [TestMethod]
        public async Task TestAdd()
        {
            querySlimService = new DBQueryServicesSlim<TestEntity>(connectionString, databaseName, colname);
            await CleanUp();
            await querySlimService.AddAsync(new TestEntity
            {
                Test = "hello"
            }, new TestEntity
            {
                Test = "hello1"
            });
        }
        [TestCleanup]
        public async Task CleanUp()
        {
            await querySlimService.collection.DeleteManyAsync(Builders<TestEntity>.Filter.Eq(t => t.IsDeleted, false));
            await querySlimService.collection.UpdateOneAsync(Builders<TestEntity>.Filter.Eq(t => t.IsDeleted, true),
                Builders<TestEntity>.Update.Set(t => t.ManagedId, 1));
        }
        [TestMethod]
        public async Task TestAny()
        {
            Assert.IsTrue(await querySlimService.AnyAsync(Builders<TestEntity>.Filter.Empty));

        }
        [TestMethod]
        public async Task TestGetNum()
        {
            Assert.AreEqual(2, await querySlimService.GetNumAsync(Builders<TestEntity>.Filter.Empty));

        }
        [TestMethod]
        public async Task TestFindFirst()
        {
            var entity = await querySlimService.FindFirstAsync(Builders<TestEntity>.Filter.Eq(t => t.Test, "hello"), t => t);
            Assert.AreEqual(1, entity.ManagedId);
            var entity1 = await querySlimService.FindFirstAsync(Builders<TestEntity>.Filter.Eq(t => t.Test, "hello1"), t => t);
            Assert.AreEqual(2, entity1.ManagedId);
        }

        [TestMethod]
        public async Task TestGet()
        {
            var l1 = await querySlimService.GetAsync(t => t, 0, 1, orderBy: "ManagedId", true, filter: t => true);
            Assert.AreEqual(1, l1.Count());
            var l2 = await querySlimService.GetAsync(t => t, 0, null, orderBy: "ManagedId", true, filter: t => true);
            Assert.AreEqual(2, l2.Count());
            Assert.AreEqual(2, l2.ToList()[0].ManagedId);
            Assert.AreEqual(1, l2.ToList()[1].ManagedId);
        }

    }
}
