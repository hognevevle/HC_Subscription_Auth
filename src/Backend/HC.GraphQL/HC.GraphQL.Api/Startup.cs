using IdentityModel.AspNetCore.OAuth2Introspection;
using IdentityServer4.AccessTokenValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HotChocolate.Resolvers;
using Microsoft.AspNetCore.Authorization;
using static HC.GraphQL.Api.RootTypes;

namespace HC.GraphQL.Api
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
            services.AddCors(options =>
                            options.AddDefaultPolicy(builder =>
                            builder.SetIsOriginAllowed(_ => true)
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .AllowCredentials())
                        );

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = IdentityServerAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = IdentityServerAuthenticationDefaults.AuthenticationScheme;
            })
            .AddJwtBearer("WebSockets", ctx => { })
            .AddIdentityServerAuthentication(IdentityServerAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Authority = "https://localhost:5001"; //TODO: Make configurable
                options.ApiName = "api1"; //TODO: Change this to correct api name ind Config.cs for AutServer
                options.RequireHttpsMetadata = false;

                options.ForwardDefaultSelector = context =>
                {
                    if (!context.Items.ContainsKey(AuthenticationSocketInterceptor.HTTP_CONTEXT_WEBSOCKET_AUTH_KEY) &&
                            context.Request.Headers.TryGetValue("Upgrade", out var value) &&
                            value.Count > 0 &&
                            value[0] is string stringValue &&
                            stringValue == "websocket")
                    {
                        return "WebSockets";
                    }
                    return JwtBearerDefaults.AuthenticationScheme;
                };

                options.TokenRetriever = new Func<HttpRequest, string>(req =>
                {
                    if (req.HttpContext.Items.TryGetValue(
                        AuthenticationSocketInterceptor.HTTP_CONTEXT_WEBSOCKET_AUTH_KEY,
                        out object token) &&
                        token is string stringToken)
                    {
                        return stringToken;
                    }
                    var fromHeader = TokenRetrieval.FromAuthorizationHeader();
                    var fromQuery = TokenRetrieval.FromQueryString();

                    return fromHeader(req) ?? fromQuery(req);
                });
            });

            services
                .AddInMemorySubscriptions()
                .AddGraphQLServer()
                .AddQueryType<Queries>()
                .AddMutationType<Mutations>()
                .AddSubscriptionType<Subscriptions>()
                .AddAuthorization()
                .AddSocketSessionInterceptor<AuthenticationSocketInterceptor>()
                .AddHttpRequestInterceptor((context, executor, builder, token) =>
                {
                    builder.TryAddProperty(nameof(HttpContext), context);
                    builder.TryAddProperty(nameof(ClaimsPrincipal), context.User);

                    return default;
                });
            
            services.AddAuthorization(opts =>
            {
                opts.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new BobRequirement())
                    .Build();
            });

            // services.AddSingleton<ISocketConnectionInterceptor<HttpContext>, AuthenticationSocketInterceptor>();
            
            // services.AddQueryRequestInterceptor((
            //     HttpContext context,
            //     IQueryRequestBuilder requestBuilder,
            //     CancellationToken _) =>
            // {
            //     requestBuilder.TryAddProperty(nameof(HttpContext), context);
            //     requestBuilder.TryAddProperty(nameof(ClaimsPrincipal), context.GetUser());
            //
            //     return Task.CompletedTask;
            // });
        }
        
        public class BobRequirement : IAuthorizationRequirement { }

        public class BobRequirementHandler : AuthorizationHandler<BobRequirement, IResolverContext>
        {
            protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, BobRequirement requirement, IResolverContext resource)
            {
                //sub: bbe27e7b-7061-4f9d-9225-2a6425fbeff3 == Bob
                if (context.User.Claims?.FirstOrDefault(i => i.Type == "sub")?.Value != "bbe27e7b-7061-4f9d-9225-2a6425fbeff3")
                {
                    context.Fail();
                    return;
                }
                
                context.Succeed(requirement);
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseCors();

            app.UseRouting();

            app.UseAuthentication();

            app.UseWebSockets();
            
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGraphQL(path: "/graphql");
            });
        }
    }
}
