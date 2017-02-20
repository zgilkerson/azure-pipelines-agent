using System;
using System.Collections.Generic;
using System.Reflection;
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
            // if (type == typeof(String) ||
            //     type == typeof(String[]) ||
            //     type == typeof(List<String>) ||
            //     type == typeof(Dictionary<String, String>))
            // {
            //     Console.WriteLine($"PipelineValue accepts {type.Name}");
            //     return true;
            // }

            // Console.WriteLine($"PipelineValue not accepts {type.Name}");
            // return false;
        }

        public Object ReadYaml(
            IParser parser, 
            Type type)
        {
            var stringType = parser.Allow<Scalar>();
            if (stringType != null)
            {
                Console.WriteLine($"Read PipelineValue from String: '{stringType.Value}'");
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

                    Console.WriteLine($"Read PipelineValue from StringDictionaryArrayValue");
                    retVal = new StringDictionaryArrayValue(items);
                }
                else if (parser.Accept<Scalar>())
                {
                    var items = new List<string>();
                    while (!parser.Accept<SequenceEnd>())
                    {
                        items.Add(parser.Expect<Scalar>().Value);
                    }

                    Console.WriteLine($"Read PipelineValue from StringArrayValue");
                    retVal = new StringArrayValue(items);
                }

                parser.Expect<SequenceEnd>();

                return retVal;
            }

            var mapping = parser.Allow<MappingStart>();
            if (mapping != null)
            {
                Console.WriteLine($"Read PipelineValue from StringDictionaryValue");
                return new StringDictionaryValue(parser.ReadMappingOfStringString());
            }

            Console.WriteLine("Unable to read PipelineValue");
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
