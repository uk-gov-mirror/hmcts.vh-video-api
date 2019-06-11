using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Video.API.Mappings;
using VideoApi.Contract.Requests;
using VideoApi.Contract.Responses;
using VideoApi.DAL.Commands;
using VideoApi.DAL.Commands.Core;
using VideoApi.DAL.Exceptions;
using VideoApi.DAL.Queries;
using VideoApi.DAL.Queries.Core;
using Task = VideoApi.Domain.Task;

namespace Video.API.Controllers
{
    [Consumes("application/json")]
    [Produces("application/json")]
    [Route("conferences")]
    public class TasksController : ControllerBase
    {
        private readonly IQueryHandler _queryHandler;
        private readonly ICommandHandler _commandHandler;

        public TasksController(IQueryHandler queryHandler, ICommandHandler commandHandler)
        {
            _queryHandler = queryHandler;
            _commandHandler = commandHandler;
        }

        /// <summary>
        /// Get tasks for a conference
        /// </summary>
        /// <param name="conferenceId">The id of the conference to retrieve tasks from</param>
        /// <returns></returns>
        [HttpGet("{conferenceId}/tasks")]
        [SwaggerOperation(OperationId = "GetTasksForConference")]
        [ProducesResponseType(typeof(List<TaskResponse>), (int) HttpStatusCode.OK)]
        public async Task<IActionResult> GetTasksForConference(Guid conferenceId)
        {
            var query = new GetTasksForConferenceQuery(conferenceId);
            try
            {
                var tasks = await _queryHandler.Handle<GetTasksForConferenceQuery, List<Task>>(query);
                var mapper = new TaskToResponseMapper();
                var response = tasks.Select(mapper.MapTaskToResponse);
                return Ok(response);
            }
            catch (ConferenceNotFoundException)
            {
                return NotFound();
            }

        }

        /// <summary>
        /// Update existing tasks
        /// </summary>
        /// <param name="conferenceId">The id of the conference to update</param>
        /// <param name="taskId">The id of the task to update</param>
        /// <param name="updateTaskRequest">username of who completed the task</param>
        /// <returns></returns>
        [HttpPatch("{conferenceId}/tasks/{taskId}")]
        [SwaggerOperation(OperationId = "UpdateTaskStatus")]
        [ProducesResponseType(typeof(TaskResponse), (int) HttpStatusCode.OK)]
        [ProducesResponseType((int) HttpStatusCode.NotFound)]
        [ProducesResponseType((int) HttpStatusCode.BadRequest)]
        public async Task<IActionResult> UpdateTaskStatus(Guid conferenceId, long taskId,
            [FromBody] UpdateTaskRequest updateTaskRequest)
        {
            var command = new UpdateTaskCommand(conferenceId, taskId, updateTaskRequest.UpdatedBy);
            
            try
            {
                await _commandHandler.Handle(command);
                var query = new GetTasksForConferenceQuery(conferenceId);
                var tasks = await _queryHandler.Handle<GetTasksForConferenceQuery, List<Task>>(query);
                var task = tasks.Single(x => x.Id == taskId);
                var response = new TaskToResponseMapper().MapTaskToResponse(task);
                return Ok(response);
            }
            catch (TaskNotFoundException)
            {
                return NotFound();
            }
        }
    }
}