using System;

namespace DocumentClassificationService.Models
{
    public class DocumentClassificationRequest
    {
        public string DocumentId { get; set; }
        public string DocumentType { get; set; }
        public string Content { get; set; }
        public string SourceVertical { get; set; }
    }
}
