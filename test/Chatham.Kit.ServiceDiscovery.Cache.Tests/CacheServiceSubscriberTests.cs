﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Chatham.Kit.ServiceDiscovery.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Chatham.Kit.ServiceDiscovery.Cache.Tests
{
    [ExcludeFromCodeCoverage]
    public class CacheServiceSubscriberTests
    {
        [Fact]
        public async Task Endpoints_PopulatesCacheImmediately()
        {
            var fixture = new CacheServiceSubscriberFixture();
            fixture.ServiceSubscriber.Endpoints().Returns(Task.FromResult(new List<Endpoint>()));
            fixture.Throttle.Queue(Arg.Any<Func<Task<List<Endpoint>>>>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new List<Endpoint>()))
                .AndDoes(async x => await Task.Delay(1000));

            var subscriber = fixture.CreateSut();
            await subscriber.Endpoints();

            fixture.Cache.Received(1).Set(Arg.Any<object>(), Arg.Any<List<Endpoint>>());
            fixture.Cache.Received(1).Get<List<Endpoint>>(Arg.Any<string>());
        }

        [Fact]
        public async Task StartSubscription_InitialCacheSetThrowsException_ExceptionBubblesUp()
        {
            var fixture = new CacheServiceSubscriberFixture();
            fixture.ServiceSubscriber.Endpoints().Returns(Task.FromResult(new List<Endpoint>()));

            var expectedException = new Exception();

            fixture.Cache.Set(Arg.Any<object>(), Arg.Any<List<Endpoint>>()).Throws(expectedException);

            var subscriber = fixture.CreateSut();

            Exception actualException = null;
            try
            {
                await subscriber.StartSubscription();

            }
            catch (Exception ex)
            {
                actualException = ex;
            }
            
            Assert.Same(expectedException, actualException);
        }

        [Fact]
        public async Task StartSubscription_InitialServiceCallThrowsException_NothingSetInCacheAndExceptionBubblesUp()
        {
            var fixture = new CacheServiceSubscriberFixture();
            var expectedException = new Exception();
            fixture.ServiceSubscriber.Endpoints(Arg.Any<CancellationToken>()).Throws(expectedException);

            var subscriber = fixture.CreateSut();

            Exception actualException = null;
            try
            {
                await subscriber.StartSubscription();

            }
            catch (Exception ex)
            {
                actualException = ex;
            }

            Assert.Same(expectedException, actualException);

            fixture.Cache.DidNotReceive().Set(Arg.Any<object>(), Arg.Any<List<Endpoint>>());
        }

        [Fact]
        public async Task Endpoints_CalledWhenDisposed_ThrowsDisposedException()
        {
            var fixture = new CacheServiceSubscriberFixture();
            var subscriber = fixture.CreateSut();
            subscriber.Dispose();

            ObjectDisposedException actualException = null;
            try
            {
                await subscriber.Endpoints();
            }
            catch (ObjectDisposedException ex)
            {
                actualException = ex;
            }
            Assert.IsType<ObjectDisposedException>(actualException);
        }

        [Fact]
        public void Dispose_DisposedTwice_DoesNotRemoveFromCacheTwice()
        {
            var fixture = new CacheServiceSubscriberFixture();
            var subscriber = fixture.CreateSut();
            subscriber.Dispose();
            subscriber.Dispose();

            fixture.Cache.Received(1).Remove(Arg.Any<string>());
        }

        [Fact]
        public async Task SubscriptionLoop_ReceivesChangedEndpoints_UpdatesCacheAndFiresEvent()
        {
            var result1 = new List<Endpoint>
                {
                    new Endpoint
                    {
                        Host = Guid.NewGuid().ToString(),
                        Port = 123
                    }
                };
            var result2 = new List<Endpoint>
                {
                    new Endpoint
                    {
                        Host = Guid.NewGuid().ToString(),
                        Port = 321
                    }
                };

            var fixture = new CacheServiceSubscriberFixture();
            fixture.ServiceSubscriber.Endpoints()
                .Returns(Task.FromResult(result1));
            fixture.Throttle.Queue(Arg.Any<Func<Task<List<Endpoint>>>>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(result1), Task.FromResult(result2))
                .AndDoes(x => Thread.Sleep(500));

            var eventWasCalled = false;
            var subscriber = fixture.CreateSut();
            subscriber.EndpointsChanged += (sender, args) => eventWasCalled = true;

            await subscriber.Endpoints();
            await Task.Delay(1250).ContinueWith(async t =>
            {
                Received.InOrder(() =>
                {
                    fixture.Cache.Set(Arg.Any<string>(), result1);
                    fixture.Cache.Set(Arg.Any<string>(), result2);
                });

                fixture.Cache.Received(2).Set(Arg.Any<string>(), Arg.Any<List<Endpoint>>());
                await fixture.Throttle.Received(3).Queue(Arg.Any<Func<Task<List<Endpoint>>>>(), Arg.Any<CancellationToken>());
                Assert.True(eventWasCalled);
            });
        }

        [Fact]
        public async Task SubscriptionLoop_ReceivesSameEndpoints_DoesNotUpdateCacheOrFireEvent()
        {
            var result = new List<Endpoint>
                {
                    new Endpoint
                    {
                        Host = Guid.NewGuid().ToString(),
                        Port = 123
                    }
                };

            var fixture = new CacheServiceSubscriberFixture();
            fixture.ServiceSubscriber.Endpoints()
                .Returns(Task.FromResult(result));
            fixture.Throttle.Queue(Arg.Any<Func<Task<List<Endpoint>>>>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(result), Task.FromResult(result))
                .AndDoes(x => Thread.Sleep(500));

            var eventWasCalled = false;
            var subscriber = fixture.CreateSut();
            subscriber.EndpointsChanged += (sender, args) => eventWasCalled = true;

            await subscriber.Endpoints();
            await Task.Delay(1250).ContinueWith(async t =>
            {
                fixture.Cache.Received(1).Set(Arg.Any<string>(), Arg.Any<List<Endpoint>>());
                await
                    fixture.Throttle.Received(3)
                        .Queue(Arg.Any<Func<Task<List<Endpoint>>>>(), Arg.Any<CancellationToken>());
                Assert.False(eventWasCalled);
            });
        }
        
        [Fact]
        public async Task SubscriptionLoop_ReceivesDifferentCountOfEndpoints_UpdatesCacheAndFiresEvent()
        {
            var result1 = new List<Endpoint>
                {
                    new Endpoint
                    {
                        Host = Guid.NewGuid().ToString(),
                        Port = 123
                    }
                };

            var result2 = new List<Endpoint>
                {
                    new Endpoint
                    {
                        Host = Guid.NewGuid().ToString(),
                        Port = 789
                    },
                    new Endpoint
                    {
                        Host = Guid.NewGuid().ToString(),
                        Port = 456
                    }
                };

            var fixture = new CacheServiceSubscriberFixture();
            fixture.ServiceSubscriber.Endpoints()
                .Returns(Task.FromResult(result1));
            fixture.Throttle.Queue(Arg.Any<Func<Task<List<Endpoint>>>>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(result1), Task.FromResult(result2))
                .AndDoes(x => Thread.Sleep(500));

            var eventWasCalled = false;
            var subscriber = fixture.CreateSut();
            subscriber.EndpointsChanged += (sender, args) => eventWasCalled = true;

            await subscriber.Endpoints();
            await Task.Delay(1250).ContinueWith(async t =>
            {
                Received.InOrder(() =>
                {
                    fixture.Cache.Set(Arg.Any<string>(), result1);
                    fixture.Cache.Set(Arg.Any<string>(), result2);
                });

                fixture.Cache.Received(2).Set(Arg.Any<string>(), Arg.Any<List<Endpoint>>());
                await fixture.Throttle.Received(3).Queue(Arg.Any<Func<Task<List<Endpoint>>>>(), Arg.Any<CancellationToken>());
                Assert.True(eventWasCalled);
            });
        }

        [Fact]
        public async Task SubscriptionLoop_WithoutUpdateEvent_UpdatesCacheWithoutFiringEventAndDying()
        {
            var result1 = new List<Endpoint>
                {
                    new Endpoint
                    {
                        Host = Guid.NewGuid().ToString(),
                        Port = 123
                    }
                };

            var result2 = new List<Endpoint>
                {
                    new Endpoint
                    {
                        Host = Guid.NewGuid().ToString(),
                        Port = 789
                    },
                    new Endpoint
                    {
                        Host = Guid.NewGuid().ToString(),
                        Port = 456
                    }
                };

            var fixture = new CacheServiceSubscriberFixture();
            fixture.ServiceSubscriber.Endpoints()
                .Returns(Task.FromResult(result1));
            fixture.Throttle.Queue(Arg.Any<Func<Task<List<Endpoint>>>>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(result1), Task.FromResult(result2))
                .AndDoes(x => Thread.Sleep(500));
            
            var subscriber = fixture.CreateSut();

            await subscriber.Endpoints();
            await Task.Delay(1250).ContinueWith(async t =>
            {
                Received.InOrder(() =>
                {
                    fixture.Cache.Set(Arg.Any<string>(), result1);
                    fixture.Cache.Set(Arg.Any<string>(), result2);
                });

                fixture.Cache.Received(2).Set(Arg.Any<string>(), Arg.Any<List<Endpoint>>());
                await fixture.Throttle.Received(3).Queue(Arg.Any<Func<Task<List<Endpoint>>>>(), Arg.Any<CancellationToken>());
            });
        }
        
    }
}