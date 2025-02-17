using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Castle.Core.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSwag.Annotations;
using VideoApi.Contract.Requests;
using VideoApi.Contract.Responses;
using VideoApi.DAL.Commands;
using VideoApi.DAL.Commands.Core;
using VideoApi.DAL.DTOs;
using VideoApi.DAL.Exceptions;
using VideoApi.DAL.Queries;
using VideoApi.DAL.Queries.Core;
using VideoApi.Domain;
using VideoApi.Extensions;
using VideoApi.Mappings;
using VideoApi.Services.Contracts;

namespace VideoApi.Controllers
{
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("conferences")]
    [ApiController]
    public class ParticipantsController : ControllerBase
    {
        private readonly IQueryHandler _queryHandler;
        private readonly ICommandHandler _commandHandler;
        private readonly IVideoPlatformService _videoPlatformService;
        private readonly ILogger<ParticipantsController> _logger;

        public ParticipantsController(ICommandHandler commandHandler, IQueryHandler queryHandler,
            IVideoPlatformService videoPlatformService, ILogger<ParticipantsController> logger)
        {
            _commandHandler = commandHandler;
            _queryHandler = queryHandler;
            _videoPlatformService = videoPlatformService;
            _logger = logger;
        }

        /// <summary>
        /// Add participants to a conference
        /// </summary>
        /// <param name="conferenceId">The id of the conference to add participants to</param>
        /// <param name="request">Details of the participant</param>
        /// <returns></returns>
        [HttpPut("{conferenceId}/participants")]
        [OpenApiOperation("AddParticipantsToConference")]
        [ProducesResponseType((int) HttpStatusCode.NoContent)]
        [ProducesResponseType((int) HttpStatusCode.NotFound)]
        [ProducesResponseType((int) HttpStatusCode.BadRequest)]
        public async Task<IActionResult> AddParticipantsToConferenceAsync(Guid conferenceId,
            AddParticipantsToConferenceRequest request)
        {
            _logger.LogDebug("AddParticipantsToConference");
            var participants = request.Participants.Select(x =>
                    new Participant(x.ParticipantRefId, x.Name.Trim(), x.FirstName.Trim(), x.LastName.Trim(),
                        x.DisplayName.Trim(), x.Username.ToLowerInvariant().Trim(), x.UserRole.MapToDomainEnum(),
                        x.HearingRole, x.CaseTypeGroup, x.ContactEmail, x.ContactTelephone)
                    {
                        Representee = x.Representee
                    })
                .ToList();
            
            var linkedParticipants = request.Participants
                .SelectMany(x => x.LinkedParticipants)
                .Select(x => new LinkedParticipantDto()
                {
                    ParticipantRefId = x.ParticipantRefId, 
                    LinkedRefId = x.LinkedRefId, 
                    Type = x.Type.MapToDomainEnum()
                }).ToList();

            try
            {
                var addParticipantCommand = new AddParticipantsToConferenceCommand(conferenceId, participants, linkedParticipants);
                await _commandHandler.Handle(addParticipantCommand);

                return NoContent();
            }
            catch (ConferenceNotFoundException ex)
            {
                _logger.LogError(ex, "Unable to find conference");
                return NotFound();
            }
        }

        /// <summary>
        /// Update participant details
        /// </summary>
        /// <param name="conferenceId">Id of conference to look up</param>
        /// <param name="participantId">Id of participant to remove</param>
        /// <param name="request">The participant information to update</param>
        /// <returns></returns>
        [HttpPatch("{conferenceId}/participants/{participantId}", Name = "UpdateParticipantDetails")]
        [OpenApiOperation("UpdateParticipantDetails")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> UpdateParticipantDetailsAsync(Guid conferenceId, Guid participantId, UpdateParticipantRequest request)
        {
            _logger.LogDebug("UpdateParticipantDetails");
            try
            {
                var linkedParticipants = request.LinkedParticipants.Select(x => new LinkedParticipantDto()
                    {
                        ParticipantRefId = x.ParticipantRefId, 
                        LinkedRefId = x.LinkedRefId, 
                        Type = x.Type.MapToDomainEnum()
                    }).ToList();
                
                var updateParticipantDetailsCommand = new UpdateParticipantDetailsCommand(conferenceId, participantId,
                    request.Fullname, request.FirstName, request.LastName, request.DisplayName, request.Representee,
                    request.ContactEmail, request.ContactTelephone, linkedParticipants);
                if (!request.Username.IsNullOrEmpty())
                {
                    updateParticipantDetailsCommand.Username = request.Username;
                }
                await _commandHandler.Handle(updateParticipantDetailsCommand);

                return NoContent();
            }
            catch (ConferenceNotFoundException ex)
            {
                _logger.LogError(ex, "Unable to find conference");
                return NotFound();
            }
            catch (ParticipantNotFoundException ex)
            {
                _logger.LogError(ex, "Unable to find participant");
                return NotFound();
            }
        }

        /// <summary>
        /// Remove participants from a conference
        /// </summary>
        /// <param name="conferenceId">The id of the conference to remove participants from</param>
        /// <param name="participantId">The id of the participant to remove</param>
        /// <returns></returns>
        [HttpDelete("{conferenceId}/participants/{participantId}")]
        [OpenApiOperation("RemoveParticipantFromConference")]
        [ProducesResponseType((int) HttpStatusCode.NoContent)]
        [ProducesResponseType((int) HttpStatusCode.NotFound)]
        [ProducesResponseType((int) HttpStatusCode.BadRequest)]
        public async Task<IActionResult> RemoveParticipantFromConferenceAsync(Guid conferenceId, Guid participantId)
        {
            _logger.LogDebug("RemoveParticipantFromConference");
            var getConferenceByIdQuery = new GetConferenceByIdQuery(conferenceId);
            var queriedConference =
                await _queryHandler.Handle<GetConferenceByIdQuery, Conference>(getConferenceByIdQuery);

            if (queriedConference == null)
            {
                _logger.LogWarning("Unable to find conference");
                return NotFound();
            }

            var participant = queriedConference.GetParticipants().SingleOrDefault(x => x.Id == participantId);
            if (participant == null)
            {
                _logger.LogWarning("Unable to find participant");
                return NotFound();
            }

            var participants = new List<Participant> {participant};
            var command = new RemoveParticipantsFromConferenceCommand(conferenceId, participants);
            await _commandHandler.Handle(command);
            return NoContent();
        }

        /// <summary>
        /// Get the test call result for a participant
        /// </summary>
        /// <param name="conferenceId">The id of the conference</param>
        /// <param name="participantId">The id of the participant</param>
        /// <returns></returns>
        [HttpGet("{conferenceId}/participants/{participantId}/selftestresult")]
        [OpenApiOperation("GetTestCallResultForParticipant")]
        [ProducesResponseType(typeof(TestCallScoreResponse), (int) HttpStatusCode.OK)]
        [ProducesResponseType((int) HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetTestCallResultForParticipantAsync(Guid conferenceId, Guid participantId)
        {
            _logger.LogDebug("GetTestCallResultForParticipant");
            
            var testCallResult = await _videoPlatformService.GetTestCallScoreAsync(participantId);
            
            if (testCallResult == null)
            {
                _logger.LogWarning("Unable to find test call result");
                return NotFound();
            }

            var command = new UpdateSelfTestCallResultCommand(conferenceId, participantId, testCallResult.Passed, testCallResult.Score);
            
            await _commandHandler.Handle(command);
            
            _logger.LogDebug("Saving test call result");
            
            var response = TaskCallResultResponseMapper.MapTaskToResponse(testCallResult);
            
            return Ok(response);
        }

        /// <summary>
        /// Retrieves the independent self test result without saving it
        /// </summary>
        /// <param name="participantId">The id of the participant</param>
        /// <returns></returns>
        [HttpGet("independentselftestresult")]
        [OpenApiOperation("GetIndependentTestCallResult")]
        [ProducesResponseType(typeof(TestCallScoreResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetIndependentTestCallResultAsync(Guid participantId)
        {
            _logger.LogDebug("GetIndependentTestCallResult");
            var testCallResult = await _videoPlatformService.GetTestCallScoreAsync(participantId);
            if (testCallResult == null)
            {
                _logger.LogWarning("Unable to find test call result");
                return NotFound();
            }
            var response = TaskCallResultResponseMapper.MapTaskToResponse(testCallResult);
            return Ok(response);
        }

        /// <summary>
        /// Get the Heartbeat Data For Participant
        /// </summary>
        /// <param name="conferenceId">The id of the conference</param>
        /// <param name="participantId">The id of the participant</param>
        /// <returns></returns>
        [HttpGet("{conferenceId}/participant/{participantId}/heartbeatrecent")]
        [OpenApiOperation("GetHeartbeatDataForParticipant")]
        [ProducesResponseType(typeof(IEnumerable<ParticipantHeartbeatResponse>), (int) HttpStatusCode.OK)]
        [ProducesResponseType((int) HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetHeartbeatDataForParticipantAsync(Guid conferenceId, Guid participantId)
        {
            _logger.LogDebug("GetHeartbeatDataForParticipantAsync");

            var query = new GetHeartbeatsFromTimePointQuery(conferenceId, participantId, TimeSpan.FromMinutes(15));
            
            var heartbeats = await _queryHandler.Handle<GetHeartbeatsFromTimePointQuery, IList<Heartbeat>>(query);
            
            var responses = HeartbeatToParticipantHeartbeatResponseMapper.MapHeartbeatToParticipantHeartbeatResponse(heartbeats);
            
            return Ok(responses);
        }

        /// <summary>
        /// Post the Heartbeat Data For Participant
        /// </summary>
        /// <param name="conferenceId">The id of the conference</param>
        /// <param name="participantId">The id of the participant</param>
        /// <param name="request">The AddHeartbeatRequest</param>
        /// <returns></returns>
        [HttpPost("{conferenceId}/participant/{participantId}/heartbeat")]
        [OpenApiOperation("SaveHeartbeatDataForParticipant")]
        [ProducesResponseType((int) HttpStatusCode.NoContent)]
        [ProducesResponseType((int) HttpStatusCode.NotFound)]
        [ProducesResponseType((int) HttpStatusCode.BadRequest)]
        public async Task<IActionResult> SaveHeartbeatDataForParticipantAsync(Guid conferenceId, Guid participantId, AddHeartbeatRequest request)
        {
            _logger.LogDebug("SaveHeartbeatDataForParticipantAsync");

            if (request == null)
            {
                _logger.LogWarning("AddHeartbeatRequest is null");
                return BadRequest();
            }

            var command = new SaveHeartbeatCommand
            (
                conferenceId, participantId, request.OutgoingAudioPercentageLost,
                request.OutgoingAudioPercentageLostRecent, request.IncomingAudioPercentageLost,
                request.IncomingAudioPercentageLostRecent, request.OutgoingVideoPercentageLost,
                request.OutgoingVideoPercentageLostRecent, request.IncomingVideoPercentageLost,
                request.IncomingVideoPercentageLostRecent, DateTime.UtcNow, request.BrowserName, request.BrowserVersion,
                request.OperatingSystem, request.OperatingSystemVersion
            );

            await _commandHandler.Handle(command);

            return NoContent();
        }

        /// <summary>
        /// Get a list of distinct first name of judges
        /// </summary>
        /// <returns></returns>
        [HttpGet("participants/Judge/firstname")]
        [OpenApiOperation("GetDistinctJudgeNames")]
        [ProducesResponseType(typeof(JudgeNameListResponse), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetDistinctJudgeNamesAsync()
        {
            _logger.LogDebug("GetDistinctJudgeNames");
            var query = new GetDistinctJudgeListByFirstNameQuery();
            var judgeFirstNames = await _queryHandler.Handle<GetDistinctJudgeListByFirstNameQuery, List<string>>(query);
            return Ok(new JudgeNameListResponse { FirstNames = judgeFirstNames });
        }

        /// <summary>
        /// Get a list of participants for a given conference Id
        /// </summary>
        /// <param name="conferenceId">The conference Id</param>
        /// <returns>The list of participants</returns>
        [HttpGet("{conferenceId}/participants")]
        [OpenApiOperation("GetParticipantsByConferenceId")]
        [ProducesResponseType(typeof(List<ParticipantSummaryResponse>), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetParticipantsByConferenceId(Guid conferenceId)
        {
            _logger.LogDebug("GetParticipantsByConferenceId");

            var query = new GetConferenceByIdQuery(conferenceId);
            var conference = await _queryHandler.Handle<GetConferenceByIdQuery, Conference>(query);
            if (conference == null)
            {
                _logger.LogWarning("Unable to find conference");
                return NotFound();
            }

            var participantRooms = conference.Rooms.OfType<ParticipantRoom>().ToList();
            var participants = conference.Participants.Select(x =>
            {
                var participantRoom =
                    participantRooms.SingleOrDefault(r => r.DoesParticipantExist(new RoomParticipant(x.Id)));
                return ParticipantToSummaryResponseMapper.MapParticipantToSummary(x, participantRoom);
            }).ToList();
            return Ok(participants);
        }
    }
}
