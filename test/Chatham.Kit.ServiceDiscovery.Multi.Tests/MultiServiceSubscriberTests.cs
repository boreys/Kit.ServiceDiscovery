﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chatham.Kit.ServiceDiscovery.Abstractions;
using Chatham.Kit.ServiceDiscovery.Fixed;
using Xunit;

namespace Chatham.Kit.ServiceDiscovery.Multi.Tests
{
    public class MultiServiceSubscriberTests
    {
        [Fact]
        public async Task SingleSubscriber_ReturnsEndpoints()
        {
            var subscriber = new FixedSubscriber(new List<Endpoint> {new Endpoint()});
            var multiSubscriber = new MultiServiceSubscriber(new List<IServiceSubscriber> { subscriber });
            var endpoints = await multiSubscriber.Endpoints();
            Assert.NotEmpty(endpoints);
            Assert.Equal(1, endpoints.Count);
            Assert.Equal(await subscriber.Endpoints(), endpoints);
        }

        [Fact]
        public async Task AddMultipleSubscribers_FirstSubscriberReturnsData()
        {
            var subscriber1 = new FixedSubscriber(new List<Endpoint> { new Endpoint { Host = Guid.NewGuid().ToString() } });
            var subscriber2 = new FixedSubscriber(new List<Endpoint> { new Endpoint { Host = Guid.NewGuid().ToString() } });
            var multiSubscriber = new MultiServiceSubscriber(new List<IServiceSubscriber> { subscriber1, subscriber2 });
            var endpoints = await multiSubscriber.Endpoints();
            Assert.NotEmpty(endpoints);
            Assert.Equal(1, endpoints.Count);
            Assert.Equal(await subscriber1.Endpoints(), endpoints);
        }

        [Fact]
        public async Task AddMultipleSubscribers_SecondSubscriberReturnsData()
        {
            var subscriber1 = new FixedSubscriber(new List<Endpoint>());
            var subscriber2 = new FixedSubscriber(new List<Endpoint> { new Endpoint { Host = Guid.NewGuid().ToString() } });
            var multiSubscriber = new MultiServiceSubscriber(new List<IServiceSubscriber> { subscriber1, subscriber2 });
            var endpoints = await multiSubscriber.Endpoints();
            Assert.NotEmpty(endpoints);
            Assert.Equal(1, endpoints.Count);
            Assert.Equal(await subscriber2.Endpoints(), endpoints);
        }

        [Fact]
        public async Task AddMultipleSubscribers_NoneReturnReturnsEmptyList()
        {
            var subscriber1 = new FixedSubscriber(new List<Endpoint>());
            var subscriber2 = new FixedSubscriber(new List<Endpoint>());
            var multiSubscriber = new MultiServiceSubscriber(new List<IServiceSubscriber> { subscriber1, subscriber2 });
            var endpoints = await multiSubscriber.Endpoints();
            Assert.Empty(endpoints);
        }
    }
}
