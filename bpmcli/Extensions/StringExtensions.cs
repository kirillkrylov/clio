namespace bpmcli.Extensions
{
    using System.Collections.Generic;
    using System.Linq;

    public static class StringExtensions
    {
        public static IEnumerable<string> ParseArray(this string input) {
			return input
                .Split(',')
                .Select(p => p.Trim())
                .ToList();
		}
    }
}