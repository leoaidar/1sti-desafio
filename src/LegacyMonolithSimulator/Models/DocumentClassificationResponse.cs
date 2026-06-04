using System.Collections.Generic;

namespace LegacyMonolithSimulator.Models
{
    public class DocumentClassificationResponse
    {
        public string? Classification { get; set; }
        public double Confidence { get; set; }
        public string? ModelVersion { get; set; }
        public List<string> Reasons { get; set; } = new List<string>();
    }
}
