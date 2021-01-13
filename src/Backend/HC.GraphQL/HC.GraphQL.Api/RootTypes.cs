using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.AspNetCore.Authorization;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;
using HotChocolate.Types;

namespace HC.GraphQL.Api
{
    public class RootTypes
    {
        public class Queries
        {
            public string Hello => "Open hello response";

            [Authorize]
            public string PrivateHello => "Private hello message";
        }

        public class Mutations
        {
            public async Task<string> OpenMutation([Service]ITopicEventSender eventSender)
            {
                await eventSender.SendAsync("openSub", "Open mutation triggered");
                return "Done it";
            }

            [Authorize]
            public async Task<string> PrivateMutation([Service]ITopicEventSender eventSender)
            {
                await eventSender.SendAsync("privateSub", "Private mutation triggered");
                return "Done it in private";
            }
        }

        public class Subscriptions
        {
            [SubscribeAndResolve]
            [Topic("openSub")]
            public async Task<ISourceStream<string>> OpenSubscription([Service]ITopicEventReceiver eventReceiver, CancellationToken cancellationToken)
            {
                return await eventReceiver.SubscribeAsync<string, string>("openSub", cancellationToken);
            }

            [Authorize]
            [SubscribeAndResolve]
            public async Task<ISourceStream<string>> PrivateSubscription(
                [Service]ITopicEventReceiver eventReceiver,
                CancellationToken cancellationToken)
            {
                return await eventReceiver.SubscribeAsync<string, string>("privateSub", cancellationToken);
            }
        }
    }
}
