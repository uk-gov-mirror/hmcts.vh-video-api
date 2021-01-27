using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoApi.Common.Configuration;
using VideoApi.Domain;
using VideoApi.Domain.Enums;
using VideoApi.Services.Exceptions;
using VideoApi.Services.Kinly;
using Task = System.Threading.Tasks.Task;
using VideoApi.Services.Contracts;
using VideoApi.Services.Dtos;
using VideoApi.Services.Mappers;

namespace VideoApi.Services
{
    public class KinlyPlatformService : IVideoPlatformService
    {
        private readonly IKinlyApiClient _kinlyApiClient;
        private readonly ILogger<KinlyPlatformService> _logger;
        private readonly ServicesConfiguration _servicesConfigOptions;
        private readonly IKinlySelfTestHttpClient _kinlySelfTestHttpClient;
        private readonly IPollyRetryService _pollyRetryService;

        public KinlyPlatformService(IKinlyApiClient kinlyApiClient,
            IOptions<ServicesConfiguration> servicesConfigOptions,
            ILogger<KinlyPlatformService> logger,
            IKinlySelfTestHttpClient kinlySelfTestHttpClient,
            IPollyRetryService pollyRetryService)
        {
            _kinlyApiClient = kinlyApiClient;
            _logger = logger;
            _servicesConfigOptions = servicesConfigOptions.Value;
            _kinlySelfTestHttpClient = kinlySelfTestHttpClient;
            _pollyRetryService = pollyRetryService;
        }


        public async Task<MeetingRoom> BookVirtualCourtroomAsync(Guid conferenceId,
            bool audioRecordingRequired,
            string ingestUrl,
            IEnumerable<EndpointDto> endpoints)
        {
            _logger.LogInformation(
                "Booking a conference for {conferenceId} with callback {CallbackUri} at {KinlyApiUrl}", conferenceId,
                _servicesConfigOptions.CallbackUri, _servicesConfigOptions.KinlyApiUrl);

            try
            {
                var response = await _kinlyApiClient.CreateHearingAsync(new CreateHearingParams
                {
                    Virtual_courtroom_id = conferenceId.ToString(),
                    Callback_uri = _servicesConfigOptions.CallbackUri,
                    Recording_enabled = audioRecordingRequired,
                    Recording_url = ingestUrl,
                    Streaming_enabled = false,
                    Streaming_url = null,
                    Jvs_endpoint = endpoints.Select(EndpointMapper.MapToEndpoint).ToList()
                });

                return new MeetingRoom
                (response.Uris.Admin, response.Uris.Participant, response.Uris.Participant,
                    response.Uris.Pexip_node, response.Telephone_conference_id);
            }
            catch (KinlyApiException e)
            {
                if (e.StatusCode == (int) HttpStatusCode.Conflict)
                {
                    throw new DoubleBookingException(conferenceId);
                }

                throw;
            }
        }

        public async Task<MeetingRoom> GetVirtualCourtRoomAsync(Guid conferenceId)
        {
            try
            {
                var response = await _kinlyApiClient.GetHearingAsync(conferenceId.ToString());
                var meetingRoom = new MeetingRoom(response.Uris.Admin, response.Uris.Participant,
                    response.Uris.Participant, response.Uris.Pexip_node, response.Telephone_conference_id);
                return meetingRoom;
            }
            catch (KinlyApiException e)
            {
                if (e.StatusCode == (int) HttpStatusCode.NotFound)
                {
                    return null;
                }

                throw;
            }
        }

        public async Task<TestCallResult> GetTestCallScoreAsync(Guid participantId, int retryAttempts = 2)
        {
            var maxRetryAttempts = retryAttempts;
            var pauseBetweenFailures = TimeSpan.FromSeconds(5);

            var result = await _pollyRetryService.WaitAndRetryAsync<Exception, TestCallResult>
            (
                maxRetryAttempts,
                _ => pauseBetweenFailures,
                retryAttempt =>
                    _logger.LogWarning(
                        "Failed to retrieve test score for participant {participantId} at {KinlySelfTestApiUrl}. Retrying attempt {retryAttempt}",
                        participantId, _servicesConfigOptions.KinlySelfTestApiUrl, retryAttempt),
                callResult => callResult == null,
                () => _kinlySelfTestHttpClient.GetTestCallScoreAsync(participantId)
            );

            return result;
        }

        public Task TransferParticipantAsync(Guid conferenceId, Guid participantId, string fromRoom,
            string toRoom)
        {
            _logger.LogInformation(
                "Transferring participant {participantId} from {fromRoom} to {toRoom} in conference: {conferenceId}",
                participantId, fromRoom, toRoom, conferenceId);

            var request = new TransferParticipantParams
            {
                From = fromRoom,
                To = toRoom,
                Part_id = participantId.ToString()
            };

            return _kinlyApiClient.TransferParticipantAsync(conferenceId.ToString(), request);
        }

        public async Task StopPrivateConsultationAsync(Conference conference, string consultationRoom)
        {
            var participants = conference.GetParticipants()
                .Where(x => x.GetCurrentStatus().ParticipantState == ParticipantState.InConsultation &&
                            x.GetCurrentRoom() == consultationRoom);

            foreach (var participant in participants)
            {
                await TransferParticipantAsync(conference.Id, participant.Id, consultationRoom,
                    RoomType.WaitingRoom.ToString());
            }

            var endpoints = conference.GetEndpoints()
                .Where(x => x.State == EndpointState.InConsultation && x.GetCurrentRoom() == consultationRoom);
            foreach (var endpoint in endpoints)
            {
                await TransferParticipantAsync(conference.Id, endpoint.Id, consultationRoom, RoomType.WaitingRoom.ToString());
            }
        }

        public Task DeleteVirtualCourtRoomAsync(Guid conferenceId)
        {
            return _kinlyApiClient.DeleteHearingAsync(conferenceId.ToString());
        }

        public Task UpdateVirtualCourtRoomAsync(Guid conferenceId, bool audioRecordingRequired,
            IEnumerable<EndpointDto> endpoints)
        {
            return _kinlyApiClient.UpdateHearingAsync(conferenceId.ToString(),
                new UpdateHearingParams
                {
                    Recording_enabled = audioRecordingRequired,
                    Jvs_endpoint = endpoints.Select(EndpointMapper.MapToEndpoint).ToList()
                });
        }

        public Task StartHearingAsync(Guid conferenceId, Layout layout = Layout.AUTOMATIC)
        {
            return _kinlyApiClient.StartHearingAsync(conferenceId.ToString(),
                new StartHearingParams {Hearing_layout = layout});
        }

        public Task PauseHearingAsync(Guid conferenceId)
        {
            return _kinlyApiClient.PauseHearingAsync(conferenceId.ToString());
        }

        public Task EndHearingAsync(Guid conferenceId)
        {
            return _kinlyApiClient.EndHearingAsync(conferenceId.ToString());
        }

        public Task SuspendHearingAsync(Guid conferenceId)
        {
            return _kinlyApiClient.TechnicalAssistanceAsync(conferenceId.ToString());
        }

        public Task<HealthCheckResponse> GetPlatformHealthAsync()
        {
            return _kinlyApiClient.HealthCheckAsync();
        }
    }
}
