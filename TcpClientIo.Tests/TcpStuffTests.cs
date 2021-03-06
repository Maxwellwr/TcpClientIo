using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Drenalol.TcpClientIo.Attributes;
using Drenalol.TcpClientIo.Converters;
using Drenalol.TcpClientIo.Exceptions;
using Drenalol.TcpClientIo.Serialization;
using Drenalol.TcpClientIo.Stuff;
using NUnit.Framework;

namespace Drenalol.TcpClientIo
{
    public class TcpStuffTests
    {
        // ReSharper disable once ClassNeverInstantiated.Local
        private class DoesNotHaveAny
        {
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class DoesNotHaveBodyAttribute
        {
            [TcpData(0, 1, TcpDataType = TcpDataType.Id)]
            public int Key { get; set; }

            [TcpData(1, 2, TcpDataType = TcpDataType.BodyLength)]
            public int BodyLength { get; set; }
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class DoesNotHaveBodyLengthAttribute
        {
            [TcpData(0, 1, TcpDataType = TcpDataType.Id)]
            public int Key { get; set; }

            [TcpData(1, 2, TcpDataType = TcpDataType.Body)]
            public int Body { get; set; }
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class KeyDoesNotHaveSetter
        {
            [TcpData(0, 1, TcpDataType = TcpDataType.Id)]
            public int Key { get; }

            [TcpData(1, 2, TcpDataType = TcpDataType.BodyLength)]
            public int BodyLength { get; set; }

            [TcpData(3, 2, TcpDataType = TcpDataType.Body)]
            public int Body { get; set; }
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class MetaDataNotHaveSetter
        {
            [TcpData(0, 4)]
            public int Meta { get; }
        }

        [Test]
        public void ReflectionErrorsTest()
        {
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper<DoesNotHaveAny, DoesNotHaveAny>());
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper<MetaDataNotHaveSetter, MetaDataNotHaveSetter>());
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper<DoesNotHaveBodyAttribute, DoesNotHaveBodyAttribute>());
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper<DoesNotHaveBodyLengthAttribute, DoesNotHaveBodyLengthAttribute>());
            Assert.Catch(typeof(TcpException), () => new ReflectionHelper<KeyDoesNotHaveSetter, KeyDoesNotHaveSetter>());
        }

        [TestCase(10000, false)]
        [TestCase(10000, true)]
        public async Task AttributeMockSerializeDeserializeTest(int count, bool useParallel)
        {
            var serializer = new TcpSerializer<uint, AttributeMockSerialize, AttributeMockSerialize>(new List<TcpConverter>
            {
                new TcpUtf8StringConverter(),
                new TcpDateTimeConverter()
            }, i => new byte[i]);

            var mock = new AttributeMockSerialize
            {
                Id = TestContext.CurrentContext.Random.NextUInt(),
                DateTime = DateTime.Now.AddSeconds(TestContext.CurrentContext.Random.NextUInt()),
                LongNumbers = TestContext.CurrentContext.Random.NextULong(),
                IntNumbers = TestContext.CurrentContext.Random.NextUInt()
            };

            mock.BuildBody();

            var enumerable = Enumerable.Range(0, count);

            var tasks = (useParallel ? enumerable.AsParallel().Select(Selector) : enumerable.Select(Selector)).ToArray();

            await Task.WhenAll(tasks);

            Task Selector(int i) =>
                Task.Run(() =>
                {
                    var serialize = serializer.Serialize(mock);
                    _ = serializer.Deserialize(new ReadOnlySequence<byte>(serialize.Request));
                });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BaseConvertersTest(bool reverse)
        {
            var dict = new Dictionary<Type, TcpConverter>
            {
                {typeof(string), new TcpUtf8StringConverter()},
                {typeof(DateTime), new TcpDateTimeConverter()},
                {typeof(Guid), new TcpGuidConverter()}
            };

            var bitConverterHelper = new BitConverterHelper(dict);

            var str = "Hello my friend";
            var stringResult = bitConverterHelper.ConvertToBytes(str, typeof(string), reverse);
            var stringResultBack = bitConverterHelper.ConvertFromBytes(new ReadOnlySequence<byte>(stringResult), typeof(string), reverse);
            Assert.AreEqual(str, stringResultBack);

            var datetime = DateTime.Now;
            var dateTimeResult = bitConverterHelper.ConvertToBytes(datetime, typeof(DateTime), reverse);
            var dateTimeResultBack = bitConverterHelper.ConvertFromBytes(new ReadOnlySequence<byte>(dateTimeResult), typeof(DateTime), reverse);
            Assert.AreEqual(datetime, dateTimeResultBack);

            var guid = Guid.NewGuid();
            var guidResult = bitConverterHelper.ConvertToBytes(guid, typeof(Guid), reverse);
            var guidResultBack = bitConverterHelper.ConvertFromBytes(new ReadOnlySequence<byte>(guidResult), typeof(Guid), reverse);
            Assert.AreEqual(guid, guidResultBack);
        }
    }
}