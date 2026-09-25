using System.Globalization;
using System.Text;

namespace PetClinix.BuildingBlocks.Infrastructure;

public static class TextSearch
{
    public static string Normalize(string term)
    {
        return term
            .Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Aggregate(new StringBuilder(), (sb, c) => sb.Append(c), sb => sb.ToString());
    }
}