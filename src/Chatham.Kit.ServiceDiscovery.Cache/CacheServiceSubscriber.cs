﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Chatham.Kit.ServiceDiscovery.Abstractions;
using Microsoft.Extensions.Logging;
using System.Linq;
using Chatham.Kit.ServiceDiscovery.Cache.Internal;

namespace Chatham.Kit.ServiceDiscovery.Cache
{
    public class CacheServiceSubscriber : ICacheServiceSubscriber
    {
        private bool _disposed;

        private readonly ILogger _log;
        private readonly ICacheClient _cache;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly IServiceSubscriber _serviceSubscriber;

        private readonly string _id = Guid.NewGuid().ToString();

        private Task _subscriptionTask;
        private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1, 1);
        private readonly IThrottle _throttle;

        public string ServiceName => _serviceSubscriber.ServiceName;
        public event EventHandler EndpointsChanged;

        public CacheServiceSubscriber(ILoggerFactory loggerFactory, IServiceSubscriber serviceSubscriber,
            ICacheClient cache, IThrottle throttle)
        {
            _log = loggerFactory.CreateLogger(nameof(CacheServiceSubscriber));
            _cache = cache;

            _serviceSubscriber = serviceSubscriber;
            _throttle = throttle;
        }

        public Task<List<Endpoint>> Endpoints()
        {
            return Endpoints(CancellationToken.None);
        }

        public async Task<List<Endpoint>> Endpoints(CancellationToken ct)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CacheServiceSubscriber));
            }

            await StartSubscription(ct);

            return _cache.Get<List<Endpoint>>(_id);
        }

        public Task StartSubscription()
        {
            return StartSubscription(CancellationToken.None);
        }

        public async Task StartSubscription(CancellationToken ct)
        {
            if (_subscriptionTask == null)
            {
                await _mutex.WaitAsync(ct);
                try
                {
                    if (_subscriptionTask == null)
                    {
                        var serviceUris = await _serviceSubscriber.Endpoints(ct).ConfigureAwait(false);
                        _cache.Set(_id, serviceUris);
                        _subscriptionTask = SubscriptionLoop(serviceUris);
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError($"Error fetching endpoints for {ServiceName}: {ex}");
                    throw;
                }
                finally
                {
                    _mutex.Release();
                }
            }
        }

        private Task SubscriptionLoop(List<Endpoint> previousEndpoints)
        {
            return Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    _log.LogTrace($"Iteration of subscription loop for {ServiceName}.");
                    try
                    {
                        var currentEndpoints =
                            await await _throttle.Queue(_serviceSubscriber.Endpoints, _cts.Token)
                                .ConfigureAwait(false);

                        if (!EndpointListsMatch(previousEndpoints, currentEndpoints))
                        {
                            _log.LogDebug($"Received updated endpoints for {ServiceName}");
                            _cache.Set(_id, currentEndpoints);
                            EndpointsChanged?.Invoke(this, EventArgs.Empty);
                            previousEndpoints = currentEndpoints;
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.LogError($"Error fetching endpoints for {ServiceName}: {ex}");
                    }
                }
            }, _cts.Token);
        }

        private static bool EndpointListsMatch(List<Endpoint> endpoints1, List<Endpoint> endpoints2)
        {
            if (endpoints1.Count != endpoints2.Count)
            {
                return false;
            }

            var filteredSequence = endpoints1.Where(endpoints2.Contains);
            return filteredSequence.Count() == endpoints1.Count;
        }

        ~CacheServiceSubscriber()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                if (!_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }
                _cts.Dispose();
                _throttle.Dispose();
            }

            _cache.Remove(_id);

            _disposed = true;
        }
    }
}
