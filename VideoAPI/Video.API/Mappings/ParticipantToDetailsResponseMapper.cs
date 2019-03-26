using System.Collections.Generic;
using VideoApi.Contract.Responses;
using VideoApi.Domain;

namespace Video.API.Mappings
{
    public class ParticipantToDetailsResponseMapper
    {
        public List<ParticipantDetailsResponse> MapParticipantsToResponse(IEnumerable<Participant> participants)
        {
            var response = new List<ParticipantDetailsResponse>();
            foreach (var participant in participants)
            {
                var paResponse = new ParticipantDetailsResponse
                {
                    Id = participant.Id,
                    Name = participant.Name,
                    Username = participant.Username,
                    DisplayName = participant.DisplayName,
                    UserRole = participant.UserRole,
                    CaseTypeGroup = participant.CaseTypeGroup,
                    CurrentStatus =
                        new ParticipantStatusResponseMapper().MapCurrentParticipantStatusToResponse(participant)
                };
                response.Add(paResponse);
            }

            return response;
        }
    }

    public class VirtualCourtToResponseMapper
    {
        public VirtualCourtResponse MapVirtualCourtToResponse(VirtualCourt virtualCourt)
        {
            if (virtualCourt == null) return null;

            return new VirtualCourtResponse
            {
                AdminUri = virtualCourt.AdminUri,
                JudgeUri = virtualCourt.JudgeUri,
                ParticipantUri = virtualCourt.ParticipantUri,
                PexipNode = virtualCourt.PexipNode
            };
        }
    }
}