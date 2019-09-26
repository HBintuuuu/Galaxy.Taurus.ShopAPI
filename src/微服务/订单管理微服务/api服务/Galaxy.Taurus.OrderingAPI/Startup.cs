﻿using Autofac.Extensions.DependencyInjection;
using Galaxy.Taurus.AuthUtil;
using Galaxy.Taurus.CachedUtil;
using Galaxy.Taurus.DBUtil;
using Galaxy.Taurus.OrderingAPI.Configs;
using Galaxy.Taurus.OrderingAPI.ConsulExtensions;
using Galaxy.Taurus.OrderingAPI.Dependency;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Cors.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using System;

namespace Galaxy.Taurus.OrderingAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public Autofac.IContainer ApplicationContainer { get; private set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            IConfigurationSection configSection = Configuration.GetSection("ServiceConfig");
            ServiceConfigInfo.Init(configSection["consul"], configSection["configKey"], configSection["ip"], Convert.ToInt32(configSection["httpPort"]));
            CSRedisInitHelper.Init(ServiceConfigInfo.Single.RedisConfig.Server, Convert.ToInt32(ServiceConfigInfo.Single.RedisConfig.Port));
            DBConnectionProvider.Config(ServiceConfigInfo.Single.DBConfig.Server,
                ServiceConfigInfo.Single.DBConfig.Port,
                ServiceConfigInfo.Single.DBConfig.DBName,
                ServiceConfigInfo.Single.DBConfig.User,
                ServiceConfigInfo.Single.DBConfig.Password);

            services.AddGalaxyAuth("OrderManage");
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            services.AddCors(options =>
            {
                options.AddPolicy("any", builder =>
                {
                    builder
                        .WithOrigins("*")
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            });

            services.Configure<MvcOptions>(options =>
            {
                options.Filters.Add(new CorsAuthorizationFilterFactory("any"));
            });

            //CSRedisInitHelper.Init(ServiceConfigInfo.Single.RedisConfig.Server, System.Convert.ToInt32(ServiceConfigInfo.Single.RedisConfig.Port));

            //注册Swagger生成器，定义一个和多个Swagger 文档
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "OrderingServiceAPI", Version = "v1" });
                c.IncludeXmlComments(AppDomain.CurrentDomain.BaseDirectory + "/Galaxy.Taurus.OrderingAPI.xml");
            });

            DependencyRegister dependencyRegister = new DependencyRegister();
            ApplicationContainer = dependencyRegister.RegisterWeb(services);
            return new AutofacServiceProvider(ApplicationContainer);//第三方IOC接管 core内置DI容器   
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime lifetime)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            if (!env.IsDevelopment())
            {
                app.RegisterConsul(lifetime, new Uri(ServiceConfigInfo.ConsulUri));
            }
            app.UseAuthentication();
            //app.UseHttpsRedirection();
            app.UseMvc();

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "OrderingAPI V1");
            });
        }
    }
}
