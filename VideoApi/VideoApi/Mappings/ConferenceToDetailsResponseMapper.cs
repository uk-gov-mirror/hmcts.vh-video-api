using System.Collections.Generic;
using System.Linq;
using VideoApi.Contract.Responses;
using VideoApi.Domain;
using VideoApi.Extensions;

namespace VideoApi.Mappings
{
    public static class ConferenceToDetailsResponseMapper
    {
        public static ConferenceDetailsResponse MapConferenceToResponse(Conference conference,
            string pexipSelfTestNode)
        {
            var allInterpreterRooms = conference.Rooms.OfType<InterpreterRoom>().ToList();
            var interpreterRooms = allInterpreterRooms.Select(RoomToCivilianRoomResponseMapper.MapToResponse).ToList();
            var response = new ConferenceDetailsResponse
            {
                Id = conference.Id,
                HearingId = conference.HearingRefId,
                CaseType = conference.CaseType,
                CaseNumber = conference.CaseNumber,
                CaseName = conference.CaseName,
                ScheduledDateTime = conference.ScheduledDateTime,
                StartedDateTime = conference.ActualStartTime,
                ClosedDateTime = conference.ClosedDateTime,
                ScheduledDuration = conference.ScheduledDuration,
                CurrentStatus = conference.GetCurrentStatus().MapToContractEnum(),
                Participants = MapParticipants(conference.Participants, allInterpreterRooms),
                MeetingRoom = MeetingRoomToResponseMapper.MapVirtualCourtToResponse(conference.GetMeetingRoom()),
                Endpoints = conference.GetEndpoints().Select(EndpointToResponseMapper.MapEndpointResponse).ToList(),
                HearingVenueName = conference.HearingVenueName,
                AudioRecordingRequired = conference.AudioRecordingRequired,
                CivilianRooms = interpreterRooms
            };

            if (response.MeetingRoom != null)
            {
                response.MeetingRoom.PexipSelfTestNode = pexipSelfTestNode;
            }
            
            return response;
        }

        private static List<ParticipantDetailsResponse> MapParticipants(IList<Participant> participants,
            List<InterpreterRoom> interpreterRooms)
        {
            return participants.Select(x =>
            {
                var interpreterRoom =
                    interpreterRooms.SingleOrDefault(r => r.DoesParticipantExist(new RoomParticipant(x.Id)));
                return ParticipantToDetailsResponseMapper.MapParticipantToResponse(x, interpreterRoom);
            }).ToList();
        }
    }
}
