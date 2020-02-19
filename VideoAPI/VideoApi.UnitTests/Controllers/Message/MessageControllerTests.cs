
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Net;
using Video.API.Controllers;
using VideoApi.Contract.Requests;
using VideoApi.DAL.Commands;
using VideoApi.DAL.Commands.Core;
using VideoApi.DAL.Queries.Core;
using Task = System.Threading.Tasks.Task;

namespace VideoApi.UnitTests.Controllers
{
    public class MessageControllerTests
    {
        private Mock<IQueryHandler> queryHandler;
        private Mock<ICommandHandler> commandHandler;
        private Mock<ILogger<MessageController>> logger;
        private MessageController messageController;

        [SetUp]
        public void TestInitialize()
        {
            queryHandler = new Mock<IQueryHandler>();
            commandHandler = new Mock<ICommandHandler>();
            logger = new Mock<ILogger<MessageController>>();

            messageController = new MessageController(queryHandler.Object,commandHandler.Object,logger.Object);
        }

        [Test]
        public async Task Should_successfully_save_given_message_request_and_return_ok_result()
        {
            var request = new AddMessageRequest
            {
                From = "Display From",
                MessageText = "Test message text"
            };

            var result = await messageController.SaveMessage(Guid.NewGuid(), request);

            var typedResult = (OkObjectResult)result;
            typedResult.StatusCode.Should().Be((int)HttpStatusCode.OK);
            typedResult.Value.Should().Be("Message saved");
            commandHandler.Verify(c => c.Handle(It.IsAny<AddMessageCommand>()), Times.Once);
        }
    }
}
