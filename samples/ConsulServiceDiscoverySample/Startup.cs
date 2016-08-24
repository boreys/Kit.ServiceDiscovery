﻿using Chatham.ServiceDiscovery.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Chatham.ServiceDiscovery.Consul;

namespace ConsulServiceDiscoverySample
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddConsulServiceDiscovery();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, IServiceSubscriberFactory serviceSubscriberFactory)
        {
            loggerFactory.AddConsole();

            if (env.IsDevelopment())
            {
                 app.UseDeveloperExceptionPage();
            }

            var serviceSubscriber = serviceSubscriberFactory.CreateSubscriber("FooService");
            app.Run(async context =>
            {
                var endpoints = await serviceSubscriber.EndPoints();
                await context.Response.WriteAsync(string.Join(",", endpoints));
            });
        }
    }
}