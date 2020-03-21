using System;
using FizzWare.NBuilder;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using VideoApi.DAL.Commands;
using VideoApi.Domain;
using VideoApi.Domain.Enums;
using Task = System.Threading.Tasks.Task;

namespace VideoApi.UnitTests.Controllers.Participant
{
    public class GetTestCallResultForParticipantTests : ParticipantsControllerTestBase
    {

        [Test]
        public async Task should_return_okay_with_response()
        {
            var testResult = Builder<TestCallResult>.CreateNew()
                .WithFactory(() => new TestCallResult(true, TestScore.Good)).Build();
            
            _mockVideoPlatformService
                .Setup(x => x.GetTestCallScoreAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(testResult));

            var conferenceId = Guid.NewGuid();
            var participantId = Guid.NewGuid();
            var command =
                new UpdateSelfTestCallResultCommand(conferenceId, participantId, testResult.Passed, testResult.Score);
            _mockCommandHandler.Setup(x => x.Handle(command));
            
            var response = await _controller.GetTestCallResultForParticipantAsync(Guid.NewGuid(), Guid.NewGuid());
            var typedResult = (OkObjectResult) response;
            typedResult.Should().NotBeNull();
        }

        [Test]
        public async Task should_return_not_found()
        {
            _mockVideoPlatformService
                .Setup(x => x.GetTestCallScoreAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult<TestCallResult>(null));
            
            var response = await _controller.GetTestCallResultForParticipantAsync(Guid.NewGuid(), Guid.NewGuid());
            var typedResult = (NotFoundResult) response;
            typedResult.Should().NotBeNull();

            _mockCommandHandler.Verify(x => x.Handle(It.IsAny<UpdateSelfTestCallResultCommand>()), Times.Never);
        }
    }
}
