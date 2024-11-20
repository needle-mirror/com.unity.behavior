using System.Text.RegularExpressions;

namespace Unity.Behavior
{
    internal static class InvalidIdentifierValidator 
    {
        public const string k_InvalidIdentifierErrorMessage = 
            "Name must start with letter or \"_\".\\nIt can only contain letters, \"_\", and numbers.";
        public const string k_VariableInvalidIdentifierErrorMessage = 
            "Variable name must start with letter or \"_\".\nIt can only contain letters, \"_\", and numbers.";
        
        // Regex pattern for valid C# identifiers
        private static readonly Regex IdentifierPattern = new Regex(@"^[\p{L}_][\p{L}\p{N}_]*$", RegexOptions.Compiled);
        
        public static bool IsValidIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return false;
            }
            
            return IdentifierPattern.IsMatch(identifier);
        }

    }
}