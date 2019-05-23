using FluentAssertions;
using NUnit.Framework;
using Testing.Common.Helper.Builders.Domain;
using VideoApi.Domain.Enums;
using VideoApi.Domain.Validations;

namespace VideoApi.UnitTests.Domain.Participants
{
    public class UpdateTestCallResultTests
    {
        [Test]
        public void should_add_test_call_result()
        {
            var participant = new ParticipantBuilder().WithUserRole(UserRole.Individual)
                .WithCaseTypeGroup("Claimant")
                .Build();

            var testCallResult = new TestCallResult(true, TestScore.Good);
            participant.UpdateTestCallResult(true, TestScore.Good);
            participant.GetTestCallResult().Should().BeEquivalentTo(testCallResult);
        }
    }
}