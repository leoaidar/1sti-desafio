using System.Text.RegularExpressions;

namespace DocumentClassificationService.Utils
{
    public static class JsonSanitizer
    {
        public static string CleanMarkdown(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;

            // Remove tags ```json ou ``` em qualquer lugar da string e faz o trim
            var cleaned = Regex.Replace(input, @"```(json)?\n?|```", "", RegexOptions.IgnoreCase);
            
            return cleaned.Trim();
        }
    }
}
