using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSwag.Annotations;
using VideoApi.Contract.Requests;
using VideoApi.Contract.Responses;
using VideoApi.DAL.Commands;
using VideoApi.DAL.Commands.Core;
using VideoApi.DAL.Exceptions;
using VideoApi.DAL.Queries;
using VideoApi.DAL.Queries.Core;
using VideoApi.Domain;
using VideoApi.Domain.Enums;
using VideoApi.Mappings;
using VideoApi.Extensions;
using VideoApi.Services.Contracts;
using VideoApi.Services.Kinly;

namespace VideoApi.Controllers
{
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("consultations")]
    [ApiController]
    public class ConsultationController : ControllerBase
    {
        private readonly IQueryHandler _queryHandler;
        private readonly ICommandHandler _commandHandler;
        private readonly ILogger<ConsultationController> _logger;
        private readonly IConsultationService _consultationService;

        public ConsultationController(
            IQueryHandler queryHandler,
            ILogger<ConsultationController> logger,
            IConsultationService consultationService,
            ICommandHandler commandHandler)
        {
            _queryHandler = queryHandler;
            _logger = logger;
            _consultationService = consultationService;
            _commandHandler = commandHandler;
        }

        /// <summary>
        /// Raise or answer to a private consultation request with another participant
        /// </summary>
        /// <param name="request">Private consultation request with or without an answer</param>
        /// <returns></returns>
        [HttpPost]
        [OpenApiOperation("RespondToConsultationRequestAsync")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> RespondToConsultationRequestAsync(ConsultationRequestResponse request)
        {
            var getConferenceByIdQuery = new GetConferenceByIdQuery(request.ConferenceId);
            var conference = await _queryHandler.Handle<GetConferenceByIdQuery, Conference>(getConferenceByIdQuery);
            if (conference == null)
            {
                _logger.LogWarning("Unable to find conference");
                return NotFound();
            }

            var requestedBy = conference.GetParticipants().SingleOrDefault(x => x.Id == request.RequestedBy);
            if (request.RequestedBy != Guid.Empty)
            {
                // Participants other than VHO
                if (requestedBy == null)
                {
                    _logger.LogWarning("Unable to find participant request by with id {RequestedBy}", request.RequestedBy);
                    return NotFound();
                }
            }
            
            var requestedFor = conference.GetParticipants().SingleOrDefault(x => x.Id == request.RequestedFor);
            if (requestedFor == null)
            {
                _logger.LogWarning("Unable to find participant request for with id {RequestedFor}", request.RequestedFor);
                return NotFound();
            }

            if (string.IsNullOrEmpty(request.RoomLabel))
            {
                _logger.LogWarning("Please provide a room label");
                return NotFound();
            }

            if (request.Answer != ConsultationAnswer.Accepted)
            {
                _logger.LogWarning($"Answered {request.Answer}");
                return NoContent();
            }

            var command = new SaveEventCommand(conference.Id, Guid.NewGuid().ToString(), EventType.Consultation,
                DateTime.UtcNow, null, null, $"Adding {requestedFor.DisplayName} to {request.RoomLabel}", null)
            {
                ParticipantId = request.RequestedBy
            };
            await _commandHandler.Handle(command);
            await _consultationService.JoinConsultationRoomAsync(request.ConferenceId, requestedFor.Id, request.RoomLabel);

            return NoContent();
        }

        /// <summary>
        /// Start a private consultation with a video endpoint
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("endpoint")]
        [OpenApiOperation("StartConsultationWithEndpoint")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> StartConsultationWithEndpointAsync(EndpointConsultationRequest request)
        {
            var getConferenceByIdQuery = new GetConferenceByIdQuery(request.ConferenceId);
            var conference = await _queryHandler.Handle<GetConferenceByIdQuery, Conference>(getConferenceByIdQuery);

            if (conference == null)
            {
                _logger.LogWarning("Unable to find conference");
                return NotFound($"Unable to find conference {request.ConferenceId}");
            }

            var endpoint = conference.GetEndpoints().SingleOrDefault(x => x.Id == request.EndpointId);
            if (endpoint == null)
            {
                _logger.LogWarning("Unable to find endpoint");
                return NotFound($"Unable to find endpoint {request.EndpointId}");
            }

            var defenceAdvocate = conference.GetParticipants().SingleOrDefault(x => x.Id == request.DefenceAdvocateId);
            if (defenceAdvocate == null)
            {
                _logger.LogWarning("Unable to find defence advocate");
                return NotFound($"Unable to find defence advocate {request.DefenceAdvocateId}");
            }

            if (string.IsNullOrWhiteSpace(endpoint.DefenceAdvocate))
            {
                const string message = "Endpoint does not have a defence advocate linked";
                _logger.LogWarning(message);
                return Unauthorized(message);
            }

            if (!endpoint.DefenceAdvocate.Trim().Equals(defenceAdvocate.Username.Trim(), StringComparison.CurrentCultureIgnoreCase))
            {
                const string message = "Defence advocate is not allowed to speak to requested endpoint";
                _logger.LogWarning(message);
                return Unauthorized(message);
            }

            var room = await _consultationService.CreateNewConsultationRoomAsync(request.ConferenceId);
            await _consultationService.JoinConsultationRoomAsync(request.ConferenceId, defenceAdvocate.Id, room.Label);
            await _consultationService.JoinConsultationRoomAsync(request.ConferenceId, endpoint.Id, room.Label);

            return Ok();
        }

        [HttpPost("lockroom")]
        [OpenApiOperation("LockRoom")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> LockRoomRequestAsync(LockRoomRequest request)
        {
            try
            {
                var lockRoomCommand = new LockRoomCommand(request.ConferenceId, request.RoomLabel, request.Lock);
                await _commandHandler.Handle(lockRoomCommand);
                return Ok();
            }
            catch (RoomNotFoundException ex)
            {
                _logger.LogError(ex, "Room doest not exist in conference {conferenceId} with label {label}", request.ConferenceId, request.RoomLabel);
                return NotFound("Room does not exist");
            }
        }

        [HttpPost("createconsultation")]
        [OpenApiOperation("CreatePrivateConsultation")]
        [ProducesResponseType(typeof(RoomResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> StartNewConsultationRequestAsync(StartConsultationRequest request)
        {
            try
            {
                var room = await _consultationService.CreateNewConsultationRoomAsync(request.ConferenceId);
                await _consultationService.JoinConsultationRoomAsync(request.ConferenceId, request.RequestedBy, room.Label);

                var response = RoomToDetailsResponseMapper.MapRoomToResponse(room);
                return Ok(response);
            }
            catch (ConferenceNotFoundException ex)
            {
                _logger.LogError(ex,
                    "Cannot create consultation for conference: {conferenceId} as the conference does not exist",
                    request.ConferenceId);
                return NotFound("Conference does not exist");
            }
            catch (ParticipantNotFoundException ex)
            {
                _logger.LogError(ex,
                    "Cannot create consultation with participant: {participantId} as the participant does not exist",
                    request.RequestedBy);
                return NotFound("Participant doesn't exist");
            }
            catch (KinlyApiException ex)
            {
                _logger.LogError(ex,
                    "Unable to create a consultation room for ConferenceId: {conferenceId}",
                    request.ConferenceId);
                return BadRequest("Consultation room creation failed");
            }
        }

        [HttpPost("start")]
        [OpenApiOperation("StartPrivateConsultation")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> StartConsultationRequestAsync(StartConsultationRequest request)
        {
            try
            {
                var room = await _consultationService.GetAvailableConsultationRoomAsync(request.ConferenceId,
                    request.RoomType.MapToDomainEnum());
                await _consultationService.JoinConsultationRoomAsync(request.ConferenceId, request.RequestedBy, room.Label);

                return Accepted();
            }
            catch (ConferenceNotFoundException ex)
            {
                _logger.LogError(ex,
                    "Cannot create consultation for conference: {ConferenceId} as the conference does not exist",
                    request.ConferenceId);
                return NotFound("Conference does not exist");
            }
            catch (ParticipantNotFoundException ex)
            {
                _logger.LogError(ex,
                    "Cannot create consultation with participant: {ParticipantId} as the participant does not exist",
                    request.RequestedBy);
                return NotFound("Participant doesn't exist");
            }
            catch (KinlyApiException ex)
            {
                _logger.LogError(ex,
                    "Unable to create a consultation room for ConferenceId: {ConferenceId}",
                    request.ConferenceId);
                return BadRequest("Consultation room creation failed");
            }
        }

        /// <summary>
        /// Leave a consultation
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("leave")]
        [OpenApiOperation("LeaveConsultation")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> LeaveConsultationAsync(LeaveConsultationRequest request)
        {
            var getConferenceByIdQuery = new GetConferenceByIdQuery(request.ConferenceId);
            var conference = await _queryHandler.Handle<GetConferenceByIdQuery, Conference>(getConferenceByIdQuery);

            if (conference == null)
            {
                _logger.LogWarning("Unable to find conference");
                return NotFound();
            }

            var participant = conference.GetParticipants().SingleOrDefault(x => x.Id == request.ParticipantId);
            if (participant == null)
            {
                _logger.LogWarning("Unable to find participant request by id");
                return NotFound();
            }

            if (!participant.CurrentVirtualRoomId.HasValue)
            {
                return BadRequest("Participant is not in a consultation");
            }

            await _consultationService.LeaveConsultationAsync(conference.Id, participant.Id, participant.GetCurrentRoom(), RoomType.WaitingRoom.ToString());
            return Ok();
        }
    }
}
