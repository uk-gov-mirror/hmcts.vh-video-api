using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using Testing.Common.Helper.Builders.Domain;
using Video.API.Mappings;
using VideoApi.Common.Security.Kinly;
using VideoApi.Domain.Enums;

namespace VideoApi.UnitTests.Mappings
{
    public class ConferenceToSummaryMapperTests
    {
        [Test]
        public void Should_map_all_properties()
        {
            var conference = new ConferenceBuilder()
                .WithConferenceStatus(ConferenceState.InSession)
                .WithConferenceStatus(ConferenceState.Paused)
                .WithConferenceStatus(ConferenceState.Closed)
                .WithMeetingRoom("https://poc.node.com", "user@email.com")
                .WithParticipant(UserRole.Judge, "Judge")
                .WithParticipants(3)
                .Build();

            const string conferencePhoneNumber = "+441234567890";
            var configuration = Builder<KinlyConfiguration>.CreateNew()
                .With(x => x.ConferencePhoneNumber = conferencePhoneNumber).Build();

            var response = ConferenceForAdminResponseMapper.MapConferenceToSummaryResponse(conference, configuration);
            response.Should().BeEquivalentTo(conference, options => options
                .Excluding(x => x.HearingRefId)
                .Excluding(x => x.Participants)
                .Excluding(x => x.ConferenceStatuses)
                .Excluding(x => x.State)
                .Excluding(x => x.InstantMessageHistory)
                .Excluding(x => x.IngestUrl)
                .Excluding(x => x.AudioRecordingRequired)
                .Excluding(x => x.Id)
                .Excluding(x => x.ActualStartTime)
                .Excluding(x => x.MeetingRoom)
                .Excluding(x => x.Endpoints)
            );
            
            response.StartedDateTime.Should().Be(conference.ActualStartTime);
            response.Status.Should().BeEquivalentTo(conference.GetCurrentStatus());
            response.ClosedDateTime.Should().HaveValue().And.Be(conference.ClosedDateTime);
            response.TelephoneConferenceId.Should().Be(conference.MeetingRoom.TelephoneConferenceId);
            response.TelephoneConferenceNumber.Should().Be(conferencePhoneNumber);
        }
    }
}
