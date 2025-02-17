using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VideoApi.DAL.Commands;
using VideoApi.DAL.Commands.Core;
using VideoApi.DAL.Queries;
using VideoApi.DAL.Queries.Core;
using VideoApi.Domain;
using VideoApi.Domain.Enums;
using VideoApi.Services.Contracts;
using VideoApi.Services.Kinly;
using Task = System.Threading.Tasks.Task;

namespace VideoApi.Services
{
    public class ConsultationService : IConsultationService
    {
        private readonly IKinlyApiClient _kinlyApiClient;
        private readonly ILogger<ConsultationService> _logger;
        private readonly ICommandHandler _commandHandler;
        private readonly IQueryHandler _queryHandler;

        public ConsultationService(IKinlyApiClient kinlyApiClient, ILogger<ConsultationService> logger,
            ICommandHandler commandHandler, IQueryHandler queryHandler)
        {
            _kinlyApiClient = kinlyApiClient;
            _logger = logger;
            _commandHandler = commandHandler;
            _queryHandler = queryHandler;
        }

        public async Task<ConsultationRoom> CreateNewConsultationRoomAsync(Guid conferenceId, VirtualCourtRoomType roomType = VirtualCourtRoomType.Participant, bool locked = false)
        {
            var consultationRoomParams = new CreateConsultationRoomParams
            {
                Room_label_prefix = roomType.ToString()
            };
            var createConsultationRoomResponse = await CreateConsultationRoomAsync(conferenceId.ToString(), consultationRoomParams);
            var createRoomCommand = new CreateConsultationRoomCommand(conferenceId, createConsultationRoomResponse.Room_label, roomType, locked);
            await _commandHandler.Handle(createRoomCommand);
            var room = new ConsultationRoom(conferenceId, createConsultationRoomResponse.Room_label, roomType, locked);
            return room;
        }

        public async Task<ConsultationRoom> GetAvailableConsultationRoomAsync(Guid conferenceId, VirtualCourtRoomType roomType)
        {
            var query = new GetAvailableConsultationRoomsByRoomTypeQuery(roomType, conferenceId);
            var listOfRooms = await _queryHandler.Handle<GetAvailableConsultationRoomsByRoomTypeQuery, List<ConsultationRoom>>(query);
            var room = listOfRooms?.FirstOrDefault(x => x.Type.Equals(roomType));
            if (room == null)
            {
                var consultationRoomParams = new CreateConsultationRoomParams
                {
                    Room_label_prefix = roomType.ToString()
                };
                var createConsultationRoomResponse =
                    await CreateConsultationRoomAsync(conferenceId.ToString(),
                        consultationRoomParams);
                var createRoomCommand = new CreateConsultationRoomCommand(conferenceId,
                    createConsultationRoomResponse.Room_label,
                    roomType,
                    false);
                await _commandHandler.Handle(createRoomCommand);
                room = new ConsultationRoom(conferenceId, createConsultationRoomResponse.Room_label, roomType, false);
            }

            return room;
        }
        
        private Task<CreateConsultationRoomResponse> CreateConsultationRoomAsync(string virtualCourtRoomId,
            CreateConsultationRoomParams createConsultationRoomParams)
        {
            _logger.LogTrace(
                "Creating a consultation for VirtualCourtRoomId: {VirtualCourtRoomId} with prefix {CreateConsultationRoomParamsPrefix}",
                virtualCourtRoomId, createConsultationRoomParams.Room_label_prefix);

            return _kinlyApiClient.CreateConsultationRoomAsync(virtualCourtRoomId, createConsultationRoomParams);
        }


        public async Task EndpointTransferToRoomAsync(Guid conferenceId, Guid endpointId, string room)
        {
            var conference =
                await _queryHandler.Handle<GetConferenceByIdQuery, Conference>(
                    new GetConferenceByIdQuery(conferenceId));
            var endpint = conference.GetEndpoints().Single(x => x.Id == endpointId);

            await TransferParticipantAsync(conferenceId, endpointId.ToString(), endpint.GetCurrentRoom(), room);
        }

        public async Task ParticipantTransferToRoomAsync(Guid conferenceId, Guid participantId, string room)
        {
            var conference =
                await _queryHandler.Handle<GetConferenceByIdQuery, Conference>(
                    new GetConferenceByIdQuery(conferenceId));
            var participant = conference.GetParticipants().Single(x => x.Id == participantId);

            var kinlyParticipantId = participant.GetParticipantRoom()?.Id.ToString() ?? participantId.ToString(); 
            await TransferParticipantAsync(conferenceId, kinlyParticipantId, participant.GetCurrentRoom(), room);
        }
        
        public async Task LeaveConsultationAsync(Guid conferenceId, Guid participantId, string fromRoom, string toRoom)
        {
            var conference =
                await _queryHandler.Handle<GetConferenceByIdQuery, Conference>(
                    new GetConferenceByIdQuery(conferenceId));
            
            var participant = conference.GetParticipants().Single(x => x.Id == participantId);

            var kinlyParticipantId = participant.GetParticipantRoom()?.Id.ToString() ?? participantId.ToString(); 
            await TransferParticipantAsync(conferenceId, kinlyParticipantId, fromRoom, toRoom);

            var lastLinkedParticipant =
                await RetrieveLastParticipantIfLinkedAndLeftAlone(conference, participant, fromRoom); 
            if (lastLinkedParticipant != null)
            {
                await TransferParticipantAsync(conferenceId, lastLinkedParticipant.GetParticipantRoom()?.Id.ToString(), fromRoom, toRoom);
            }
        }

        private async Task<Participant> RetrieveLastParticipantIfLinkedAndLeftAlone(Conference conference, Participant participant,
            string fromRoomLabel)
        {
            if (participant.GetParticipantRoom() != null)
            {
                return null;
            }

            var roomQuery = new GetConsultationRoomByIdQuery(conference.Id, fromRoomLabel);
            var consultationRoom =
                await _queryHandler.Handle<GetConsultationRoomByIdQuery, ConsultationRoom>(roomQuery);
            var remainingParticipants =
                consultationRoom.RoomParticipants.Where(x => x.ParticipantId != participant.Id).ToList();
            if (remainingParticipants.Count != 2)
            {
                return null;
            }

            var participantIds = remainingParticipants.Select(x => x.ParticipantId);

            var firstRemaining = conference.Participants.First(x => participantIds.Contains(x.Id));
            return firstRemaining.LinkedParticipants.Any(x => participantIds.Contains(x.LinkedId)) ? firstRemaining : null;
        }

        private async Task TransferParticipantAsync(Guid conferenceId, string participantId, string fromRoom,
            string toRoom)
        {
            _logger.LogTrace(
                "Transferring participant {ParticipantId} from {FromRoom} to {ToRoom} in conference: {ConferenceId}",
                participantId, fromRoom, toRoom, conferenceId);

            var request = new TransferParticipantParams
            {
                From = fromRoom,
                To = toRoom,
                Part_id = participantId
            };

            await _kinlyApiClient.TransferParticipantAsync(conferenceId.ToString(), request);
        }

    }
}
