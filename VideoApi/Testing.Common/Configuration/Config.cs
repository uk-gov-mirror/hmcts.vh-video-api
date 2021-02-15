using VideoApi.Common.Configuration;
using VideoApi.Common.Security.Kinly;


namespace Testing.Common.Configuration
{
    public class Config
    {
        public AzureAdConfiguration AzureAdConfiguration { get; set; }
        public ConnectionStringsConfig DbConnection { get; set; }
        public ServicesConfiguration Services { get; set; }
        public WowzaConfiguration Wowza { get; set; }
        public CvpConfiguration Cvp { get; set; }
        public AudioRecordingTestIdConfiguration AudioRecordingTestIds { get; set; }
        public KinlyConfiguration KinlyConfiguration { get; set; }
    }
}
