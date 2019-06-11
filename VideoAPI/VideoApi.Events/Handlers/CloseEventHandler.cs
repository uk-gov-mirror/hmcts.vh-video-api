using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using VideoApi.DAL.Commands;
using VideoApi.DAL.Commands.Core;
using VideoApi.DAL.Queries.Core;
using VideoApi.Domain.Enums;
using VideoApi.Events.Handlers.Core;
using VideoApi.Events.Hub;
using VideoApi.Events.Models;
using VideoApi.Events.ServiceBus;

namespace VideoApi.Events.Handlers
{
    public class CloseEventHandler : EventHandlerBase
    {
        public CloseEventHandler(IQueryHandler queryHandler, ICommandHandler commandHandler,
            IServiceBusQueueClient serviceBusQueueClient, IHubContext<EventHub, IEventHubClient> hubContext) : base(
            queryHandler, commandHandler, serviceBusQueueClient, hubContext)
        {
        }

        public override EventType EventType => EventType.Close;

        protected override async Task PublishStatusAsync(CallbackEvent callbackEvent)
        {
            var conferenceState = ConferenceState.Closed;
            await PublishConferenceStatusMessage(conferenceState);

            var command =
                new UpdateConferenceStatusCommand(SourceConference.Id, conferenceState);
            await CommandHandler.Handle(command);
            
            var hearingEventMessage = new HearingEventMessage
            {
                ConferenceId = SourceConference.Id,
                ConferenceStatus = conferenceState
            };

            await ServiceBusQueueClient.AddMessageToQueue(hearingEventMessage);
        }
    }
}