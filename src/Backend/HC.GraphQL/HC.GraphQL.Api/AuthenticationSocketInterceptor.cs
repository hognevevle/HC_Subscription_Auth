using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using HotChocolate.AspNetCore;
using HotChocolate.AspNetCore.Subscriptions;
using HotChocolate.AspNetCore.Subscriptions.Messages;
using HotChocolate.Execution;

namespace HC.GraphQL.Api
{
    public class AuthenticationSocketInterceptor : ISocketSessionInterceptor
    {
        // This is the key to the auth token in the HTTP Context
        public static readonly string HTTP_CONTEXT_WEBSOCKET_AUTH_KEY = "websocket-auth-token";

        // This is the key that apollo uses in the connection init request
        public static readonly string WEBOCKET_PAYLOAD_AUTH_KEY = "authToken";

        // private readonly IAuthenticationSchemeProvider _schemes;
        // public AuthenticationSocketInterceptor(IAuthenticationSchemeProvider schemes)
        // {
        //     _schemes = schemes;
        // }

        // public async Task<ConnectionStatus> OnOpenAsync(
        //     HttpContext context,
        //     IReadOnlyDictionary<string, object> properties,
        //     CancellationToken cancellationToken)
        // {
        //     if (properties.TryGetValue(WEBOCKET_PAYLOAD_AUTH_KEY, out object token) &&
        //         token is string stringToken)
        //     {
        //         context.Items[HTTP_CONTEXT_WEBSOCKET_AUTH_KEY] = stringToken;
        //         context.Features.Set<IAuthenticationFeature>(new AuthenticationFeature
        //         {
        //             OriginalPath = context.Request.Path,
        //             OriginalPathBase = context.Request.PathBase
        //         });
        //         // Give any IAuthenticationRequestHandler schemes a chance to handle the request
        //         var handlers = context.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
        //         foreach (var scheme in await _schemes.GetRequestHandlerSchemesAsync())
        //         {
        //             var handler = handlers.GetHandlerAsync(context, scheme.Name) as IAuthenticationRequestHandler;
        //             if (handler != null && await handler.HandleRequestAsync())
        //             {
        //                 return ConnectionStatus.Reject();
        //             }
        //         }
        //         var defaultAuthenticate = await _schemes.GetDefaultAuthenticateSchemeAsync();
        //         if (defaultAuthenticate != null)
        //         {
        //             var result = await context.AuthenticateAsync(defaultAuthenticate.Name);
        //             if (result?.Principal != null)
        //             {
        //                 context.User = result.Principal;
        //                 return ConnectionStatus.Accept();
        //             }
        //         }
        //     }
        //     return ConnectionStatus.Reject();
        // }

        public async ValueTask<ConnectionStatus> OnConnectAsync(ISocketConnection connection,
            InitializeConnectionMessage message,
            CancellationToken cancellationToken)
        {
            var schemes = connection.RequestServices.GetService<IAuthenticationSchemeProvider>();

            if (schemes is null)
            {
                Console.WriteLine("Failed to get authentication scheme provider");
                return ConnectionStatus.Reject();
            }

            if (message.Payload?[WEBOCKET_PAYLOAD_AUTH_KEY] is not string stringToken)
            {
                Console.WriteLine("authToken was missing from request");
                return ConnectionStatus.Reject();
            }

            connection.HttpContext.Items[HTTP_CONTEXT_WEBSOCKET_AUTH_KEY] = stringToken;
            connection.HttpContext.Features.Set<IAuthenticationFeature>(new AuthenticationFeature
            {
                OriginalPath = connection.HttpContext.Request.Path,
                OriginalPathBase = connection.HttpContext.Request.PathBase
            });
            // Give any IAuthenticationRequestHandler schemes a chance to handle the request
            var handlers = connection.HttpContext.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
            foreach (var scheme in await schemes.GetRequestHandlerSchemesAsync())
            {
                var handler =
                    handlers.GetHandlerAsync(connection.HttpContext, scheme.Name) as IAuthenticationRequestHandler;
                if (handler != null && await handler.HandleRequestAsync())
                {
                    return ConnectionStatus.Reject();
                }
            }

            var defaultAuthenticate = await schemes.GetDefaultAuthenticateSchemeAsync();
            if (defaultAuthenticate != null)
            {
                var result = await connection.HttpContext.AuthenticateAsync(defaultAuthenticate.Name);
                if (result?.Principal != null)
                {
                    connection.HttpContext.User = result.Principal;
                    return ConnectionStatus.Accept();
                }
            }

            Console.WriteLine($"Failed to authenticate token {stringToken}");
            return ConnectionStatus.Reject();
        }

        public async ValueTask OnRequestAsync(ISocketConnection connection, IQueryRequestBuilder requestBuilder,
            CancellationToken cancellationToken)
        {
            requestBuilder.TrySetServices(connection.RequestServices);
            requestBuilder.TryAddProperty(nameof(HttpContext), connection.HttpContext);
            requestBuilder.TryAddProperty(nameof(ClaimsPrincipal), connection.HttpContext.User);
        }

        public async ValueTask OnCloseAsync(ISocketConnection connection, CancellationToken cancellationToken)
        {
            //
        }
    }
}