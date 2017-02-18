using System;
using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace ConsoleApp2.Yaml
{
    internal class PipelineIteratorValueYamlConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type)
        {
            return typeof(IteratorValueTemplate).IsAssignableFrom(type);
        }

        public object ReadYaml(
            IParser parser,
            Type type)
        {
            var stringToken = parser.Allow<Scalar>();
            if (stringToken == null)
            {
                var arrayToken = parser.Allow<SequenceStart>();
                if (arrayToken == null)
                {
                    throw new SyntaxErrorException(parser.Current.Start, parser.Current.End, "Expected a string, string array, or mapping");
                }

                // first determine the mode we are reading in
                var simpleArray = parser.Accept<Scalar>();
                var mappingArray = parser.Accept<MappingStart>();

                List<String> simpleValues = new List<string>();
                List<Dictionary<String, String>> mappingValues = new List<Dictionary<string, string>>();
                while (parser.Peek<SequenceEnd>() == null)
                {
                    if (simpleArray)
                    {
                        simpleValues.Add(parser.Expect<Scalar>().Value);
                    }
                    else
                    {
                        mappingValues.Add(parser.ReadMappingOfStringString());
                    }
                }

                parser.Expect<SequenceEnd>();

                return simpleArray ? new IteratorValueTemplate(simpleValues) : new IteratorValueTemplate(mappingValues);
            }
            else
            {
                return new IteratorValueTemplate(stringToken.Value);
            }
        }

        public void WriteYaml(IEmitter emitter, object value, Type type)
        {
            throw new NotImplementedException();
        }
    }
}
