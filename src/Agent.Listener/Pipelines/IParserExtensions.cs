using System;
using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace ConsoleApp2
{
    internal static class IParserExtensions
    {
        /// <summary>
        /// Reads a mapping(string, string) from start to end using <c>StringComparer.OrdinalIgnoreCase</c>.
        /// </summary>
        /// <param name="parser">The parser instance from which to read</param>
        /// <returns>A dictionary instance with the specified comparer</returns>
        public static Dictionary<String, String> ReadMappingOfStringString(this IParser parser)
        {
            return parser.ReadMappingOfStringString(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reads a mapping(string, string) from start to end using the specified <c>StringComparer</c>.
        /// </summary>
        /// <param name="parser">The parser instance from which to read</param>
        /// <returns>A dictionary instance with the specified comparer</returns>
        public static Dictionary<String, String> ReadMappingOfStringString(
            this IParser parser,
            StringComparer comparer)
        {
            parser.Expect<MappingStart>();

            var mappingValue = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

            while (!parser.Accept<MappingEnd>())
            {
                mappingValue.Add(parser.Expect<Scalar>().Value, parser.Expect<Scalar>().Value);
            }

            parser.Expect<MappingEnd>();
            return mappingValue;
        }
    }
}
