using System;
using System.Collections.Generic;
using ConsoleApp2.Types;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace ConsoleApp2.Yaml
{
    class PipelineValueYamlConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type)
        {
            return typeof(PipelineValue).IsAssignableFrom(type);
        }

        public Object ReadYaml(
            IParser parser, 
            Type type)
        {
            var stringType = parser.Allow<Scalar>();
            if (stringType != null)
            {
                return new StringValue(stringType.Value);
            }

            var stringArray = parser.Allow<SequenceStart>();
            if (stringArray != null)
            {
                PipelineValue retVal = null;

                if (parser.Accept<MappingStart>())
                {
                    var items = new List<IDictionary<String, String>>();
                    while (!parser.Accept<SequenceEnd>())
                    {
                        items.Add(parser.ReadMappingOfStringString());
                    }

                    retVal = new StringDictionaryArrayValue(items);
                }
                else if (parser.Accept<Scalar>())
                {
                    var items = new List<string>();
                    while (!parser.Accept<SequenceEnd>())
                    {
                        items.Add(parser.Expect<Scalar>().Value);
                    }

                    retVal = new StringArrayValue(items);
                }

                parser.Expect<SequenceEnd>();

                return retVal;
            }

            var mapping = parser.Allow<MappingStart>();
            if (mapping != null)
            {
                return new StringDictionaryValue(parser.ReadMappingOfStringString());
            }

            return null;
        }

        public void WriteYaml(
            IEmitter emitter, 
            Object value, 
            Type type)
        {
            throw new NotImplementedException();
        }
    }
}
