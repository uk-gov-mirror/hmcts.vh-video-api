using System;
using VideoApi.Domain.Enums;

namespace VideoApi.Contract.Requests
{
    public class AddAlertRequest
    {
        /// <summary>
        /// The type of alert to raise
        /// </summary>
        public TaskType? Type { get; set; }
        
        /// <summary>
        /// The alert body/message
        /// </summary>
        public string Body { get; set; }
    }
}