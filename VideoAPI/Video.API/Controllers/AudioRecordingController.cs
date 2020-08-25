using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;
using Video.API.Mappings;
using VideoApi.Contract.Responses;
using VideoApi.DAL.Exceptions;
using VideoApi.DAL.Queries;
using VideoApi.DAL.Queries.Core;
using VideoApi.Domain;
using VideoApi.Services.Contracts;
using VideoApi.Services.Exceptions;
using Task = System.Threading.Tasks.Task;

namespace Video.API.Controllers
{
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("conferences")]
    [ApiController]
    public class AudioRecordingController : ControllerBase
    {
        private readonly IAudioPlatformService _audioPlatformService;
        private readonly IStorageService _storageService;
        private readonly ILogger<AudioRecordingController> _logger;
        private readonly IQueryHandler _queryHandler;

        public AudioRecordingController(IAudioPlatformService audioPlatformService, IStorageService storageService,
            ILogger<AudioRecordingController> logger, IQueryHandler queryHandler)
        {
            _audioPlatformService = audioPlatformService;
            _storageService = storageService;
            _logger = logger;
            _queryHandler = queryHandler;
        }

        /// <summary>
        /// Gets the audio application info for the conference by hearingId
        /// </summary>
        /// <param name="hearingId">The HearingRefId of the conference to retrieve the audio application info</param>
        /// <returns></returns>
        [HttpGet("audioapplications/{hearingId}")]
        [SwaggerOperation(OperationId = "GetAudioApplication")]
        [ProducesResponseType(typeof(AudioApplicationInfoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAudioApplicationAsync(Guid hearingId)
        {
            _logger.LogDebug("GetAudioApplication");

            var response = await _audioPlatformService.GetAudioApplicationInfoAsync(hearingId);

            if (response == null) return NotFound();

            return Ok(AudioRecordingMapper.MapToAudioApplicationInfo(response));
        }

        /// <summary>
        /// Creates the audio application for the conference by hearingId
        /// </summary>
        /// <param name="hearingId">The HearingRefId of the conference to create the audio application info</param>
        /// <returns></returns>
        [HttpPost("audioapplications/{hearingId}")]
        [SwaggerOperation(OperationId = "CreateAudioApplication")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateAudioApplicationAsync(Guid hearingId)
        {
            _logger.LogDebug("CreateAudioApplication");

            var response = await _audioPlatformService.CreateAudioApplicationAsync(hearingId);

            if (!response.Success || response.StatusCode == HttpStatusCode.Conflict)
            {
                return StatusCode((int)response.StatusCode, response.Message);
            }

            return Ok();
        }

        /// <summary>
        /// Creates the audio application and associated stream for the conference by hearingId
        /// </summary>
        /// <param name="hearingId">The HearingRefId of the conference to create the audio recording stream</param>
        /// <returns>The ingest url for other applications to stream to the endpoint</returns>
        [HttpPost("audioapplications/audiostream/{hearingId}")]
        [SwaggerOperation(OperationId = "CreateAudioApplicationWithStream")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateAudioApplicationWithStreamAsync(Guid hearingId)
        {
            _logger.LogDebug("CreateAudioApplicationWithStream");

            var response = await _audioPlatformService.CreateAudioApplicationWithStreamAsync(hearingId);

            return response.Success ? Ok(response.IngestUrl) : StatusCode((int)response.StatusCode, response.Message);
        }

        /// <summary>
        /// Deletes the audio application for the conference by hearingId
        /// </summary>
        /// <param name="hearingId">The HearingRefId of the conference to stop the audio recording application</param>
        /// <returns></returns>
        [HttpDelete("audioapplications/{hearingId}")]
        [SwaggerOperation(OperationId = "DeleteAudioApplication")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAudioApplicationAsync(Guid hearingId)
        {
            _logger.LogDebug("DeleteAudioApplication");

            try
            {
                await EnsureAudioFileExists(hearingId);
            }
            catch (Exception ex) when (ex is AudioPlatformFileNotFoundException || ex is ConferenceNotFoundException)
            {
                _logger.LogError(ex, ex.Message);
                return NotFound();
            }

            var response = await _audioPlatformService.DeleteAudioApplicationAsync(hearingId);

            if (!response.Success)
            {
                return StatusCode((int)response.StatusCode, response.Message);
            }

            return NoContent();

        }

        /// <summary>
        /// Gets the audio stream for the conference by hearingId
        /// </summary>
        /// <param name="hearingId">The HearingRefId of the conference to get the audio recording stream</param>
        /// <returns>AudioStreamInfoResponse</returns>
        [HttpGet("audiostreams/{hearingId}")]
        [SwaggerOperation(OperationId = "GetAudioStreamInfo")]
        [ProducesResponseType(typeof(AudioStreamInfoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAudioStreamInfoAsync(Guid hearingId)
        {
            _logger.LogDebug("GetAudioStreamInfo");

            var response = await _audioPlatformService.GetAudioStreamInfoAsync(hearingId);

            if (response == null) return NotFound();

            return Ok(AudioRecordingMapper.MapToAudioStreamInfo(response));
        }

        /// <summary>
        /// Gets the audio stream for monitoring the conference by hearingId
        /// </summary>
        /// <param name="hearingId">The HearingRefId of the conference to monitor the audio recording stream</param>
        /// <returns>AudioStreamInfoResponse</returns>
        [HttpGet("audiostreams/{hearingId}/monitoring")]
        [SwaggerOperation(OperationId = "GetAudioStreamMonitoringInfo")]
        [ProducesResponseType(typeof(AudioStreamMonitoringInfo), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAudioStreamMonitoringInfoAsync(Guid hearingId)
        {
            _logger.LogDebug("GetAudioStreamMonitoringInfo");

            var response = await _audioPlatformService.GetAudioStreamMonitoringInfoAsync(hearingId);

            if (response == null) return NotFound();

            return Ok(AudioRecordingMapper.MapToAudioStreamMonitoringInfo(response));
        }

        /// <summary>
        /// Creates the audio stream for the conference by hearingId
        /// </summary>
        /// <param name="hearingId">The HearingRefId of the conference to create the audio recording stream</param>
        /// <returns></returns>
        [HttpPost("audiostreams/{hearingId}")]
        [SwaggerOperation(OperationId = "CreateAudioStream")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateAudioStreamAsync(Guid hearingId)
        {
            _logger.LogDebug("CreateAudioStream");

            var response = await _audioPlatformService.CreateAudioStreamAsync(hearingId);

            return response.Success ? Ok(response.IngestUrl) : StatusCode((int)response.StatusCode, response.Message);
        }

        /// <summary>
        /// Deletes the audio stream for the conference by hearingId
        /// </summary>
        /// <param name="hearingId">The HearingRefId of the conference to stop the audio stream</param>
        /// <returns></returns>
        [HttpDelete("audiostreams/{hearingId}")]
        [SwaggerOperation(OperationId = "DeleteAudioStream")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAudioStreamAsync(Guid hearingId)
        {
            _logger.LogDebug("DeleteAudioStream");

            var response = await _audioPlatformService.DeleteAudioStreamAsync(hearingId);

            if (!response.Success)
            {
                return StatusCode((int)response.StatusCode, response.Message);
            }

            return NoContent();
        }

        /// <summary>
        /// Get the audio recording link for a given hearing.
        /// </summary>
        /// <param name="hearingId">The hearing id.</param>
        /// <returns> AudioRecordingResponse with the link - AudioFileLink</returns>
        [HttpGet("audio/{hearingId}")]
        [SwaggerOperation(OperationId = "GetAudioRecordingLink")]
        [ProducesResponseType(typeof(AudioRecordingResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetAudioRecordingLinkAsync(Guid hearingId)
        {
            _logger.LogInformation($"Getting audio recording link for hearing: {hearingId}");
            var filePath = $"{hearingId}.mp4";
            try
            {
                await EnsureAudioFileExists(hearingId);
                var audioFileLink = await _storageService.CreateSharedAccessSignature(filePath, TimeSpan.FromDays(14));
                return Ok(new AudioRecordingResponse { AudioFileLink = audioFileLink });

            }
            catch (Exception ex) when (ex is AudioPlatformFileNotFoundException || ex is ConferenceNotFoundException)
            {
                _logger.LogError(ex, ex.Message);
                return NotFound();
            }

        }

        private async Task EnsureAudioFileExists(Guid hearingId)
        {
            var conference = await _queryHandler.Handle<GetConferenceByHearingRefIdQuery, Conference>(
                new GetConferenceByHearingRefIdQuery(hearingId));
            if (conference == null)
            {
                throw new ConferenceNotFoundException(hearingId);
            }
            var filePath = $"{hearingId}.mp4";
            
            if (conference.ActualStartTime.HasValue && !await _storageService.FileExistsAsync(filePath))
            {
                var msg = $"Audio recording file not found for hearing: {hearingId}";
                throw new AudioPlatformFileNotFoundException(msg, HttpStatusCode.NotFound);
            }
        }
    }
}
