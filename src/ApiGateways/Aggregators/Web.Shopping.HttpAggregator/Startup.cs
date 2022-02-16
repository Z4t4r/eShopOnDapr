using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text.Json;
using Dapr.Client;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.eShopOnContainers.Web.Shopping.HttpAggregator.Filters.Basket.API.Infrastructure.Filters;
using Microsoft.eShopOnContainers.Web.Shopping.HttpAggregator.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Converters;

namespace Microsoft.eShopOnContainers.Web.Shopping.HttpAggregator
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var healthCheckBuilder = services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy())
                .AddUrlGroup(Configuration.GetServiceUri("catalog-api"), name: "catalogapi-check", tags: new string[] { "catalogapi" })
                .AddUrlGroup(Configuration.GetServiceUri("ordering-api"), name: "orderingapi-check", tags: new string[] { "orderingapi" })
                //.AddUrlGroup(Configuration.GetServiceUri("identity-api"), name: "identityapi-check", tags: new string[] { "identityapi" })
                .AddUrlGroup(Configuration.GetServiceUri("basket-api"), name: "basketapi-check", tags: new string[] { "basketapi" })
                .AddUrlGroup(Configuration.GetServiceUri("payment-api"), name: "paymentapi-check", tags: new string[] { "paymentapi" });

            services.AddCustomMvc(Configuration)
                .AddCustomAADSwagger(Configuration)
                //.AddCustomAuthentication(Configuration)
                .AddApplicationServices();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            var pathBase = Configuration["PATH_BASE"];
            if (!string.IsNullOrEmpty(pathBase))
            {
                loggerFactory.CreateLogger<Startup>().LogDebug("Using PATH BASE '{pathBase}'", pathBase);
                app.UsePathBase(pathBase);
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors("CorsPolicy");
            app.UseHttpsRedirection();

            app.UseSwagger().UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint($"{ (!string.IsNullOrEmpty(pathBase) ? pathBase : string.Empty) }/swagger/v1/swagger.json", "Purchase BFF V1");

                c.OAuthClientId(Configuration["OpenIdClientId"]);
                //c.OAuthClientSecret(string.Empty);
                //c.OAuthRealm(string.Empty);
                c.OAuthAppName("web shopping bff Swagger UI");
                c.OAuthUseBasicAuthenticationWithAccessCodeGrant(); 
            });

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/hc", new HealthCheckOptions()
                {
                    Predicate = _ => true,
                    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                });
                endpoints.MapHealthChecks("/liveness", new HealthCheckOptions
                {
                    Predicate = r => r.Name.Contains("self")
                });
            });
        }
    }

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCustomAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Remove("sub");

            var identityUrl = configuration.GetValue<string>("urls:identity");
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;

            })
            .AddJwtBearer(options =>
            {
                options.Authority = identityUrl;
                options.RequireHttpsMetadata = false;
                options.Audience = "webshoppingagg";
            });

            return services;
        }

        public static IServiceCollection AddCustomMvc(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions();

            services.AddControllers()
                .AddDapr()
                .AddNewtonsoftJson(options => options.SerializerSettings.Converters.Add(new StringEnumConverter()));

            // services.AddSwaggerGen(options =>
            // {
            //     options.SwaggerDoc("v1", new OpenApiInfo
            //     {
            //         Title = "Shopping Aggregator for Web Clients",
            //         Version = "v1",
            //         Description = "Shopping Aggregator for Web Clients"
            //     });
            //
            //     options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
            //     {
            //         Type = SecuritySchemeType.OAuth2,
            //         Flows = new OpenApiOAuthFlows()
            //         {
            //             Implicit = new OpenApiOAuthFlow()
            //             {
            //                 AuthorizationUrl = new Uri($"{configuration.GetValue<string>("IdentityUrlExternal")}/connect/authorize"),
            //                 TokenUrl = new Uri($"{configuration.GetValue<string>("IdentityUrlExternal")}/connect/token"),
            //
            //                 Scopes = new Dictionary<string, string>()
            //                 {
            //                     { "webshoppingagg", "Shopping Aggregator for Web Clients" }
            //                 }
            //             }
            //         }
            //     });
            //
            //     options.OperationFilter<AuthorizeCheckOperationFilter>();
            // });
            services.AddSwaggerGenNewtonsoftSupport();

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder
                    .SetIsOriginAllowed((host) => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
            });

            return services;
        }
        public static IServiceCollection AddCustomAADSwagger(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "swaggerAADdemo", Version = "v1" });            
                c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
                {
                    Description = "OAuth2.0 Auth Code with PKCE",
                    Name = "oauth2",
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        Implicit  = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl = new Uri(configuration["AuthorizationUrl"]),
                            TokenUrl = new Uri(configuration["TokenUrl"]),
                            Scopes = configuration.GetSection("ApiScope")
                                .GetChildren()
                                .Select(x => x.Value)
                                .ToArray()
                                .ToDictionary(a=>a, a=>a)
                        }
  
                    } 
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" },
                            Scheme = "oauth2",
                            Name = "oauth2",
                            In = ParameterLocation.Header
                            
                        },
                        configuration.GetSection("ApiScope")
                            .GetChildren()
                            .Select(x => x.Value)
                            .ToArray()
                    }
                });
            });
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"));


            return services;
        }
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddSingleton<IBasketService, BasketService>(
                _ => new BasketService(DaprClient.CreateInvokeHttpClient("basket-api")));

            services.AddSingleton<ICatalogService, CatalogService>(
                _ => new CatalogService(DaprClient.CreateInvokeHttpClient("catalog-api")));

            services.AddSingleton<IOrderingService, OrderingService>(
                _ => new OrderingService(DaprClient.CreateInvokeHttpClient("ordering-api")));

            return services;
        }
    }
}
