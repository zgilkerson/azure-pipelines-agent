using System;
using System.Collections.Generic;
using System.Reflection;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace ConsoleApp2.Yaml
{
    internal class PipelineStepYamlConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type)
        {
            return typeof(PipelineJobStep).IsAssignableFrom(type);
            // return type == typeof(ImportStep) ||
            //     type == typeof(ExportStep) ||
            //     type == typeof(GroupStep) ||
            //     type == typeof(TaskStep);
        }

        public Object ReadYaml(
            IParser parser,
            Type type)
        {
            PipelineJobStep step = null;

            parser.Expect<MappingStart>();

            var stepType = parser.Expect<Scalar>();
            if (stepType.Value.Equals("import"))
            {
                var resource = parser.Expect<Scalar>();
                step = new ImportStep { Name = resource.Value };
            }
            else if (stepType.Value.Equals("group"))
            {
                var groupName = parser.Expect<Scalar>();
                step = new GroupStep { Name = groupName.Value };
            }
            else if (stepType.Value.Equals("task"))
            {
                var taskRefString = parser.Expect<Scalar>();
                var components = taskRefString.Value.Split('@');

                var taskReference = new TaskReference
                {
                    Name = components[0],
                };

                if (components.Length == 2)
                {
                    taskReference.Version = components[1];
                }

                String name = null;
                var inputs = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
                while (parser.Peek<MappingEnd>() == null)
                {
                    var nextProperty = parser.Expect<Scalar>();
                    if (nextProperty.Value.Equals("name"))
                    {
                        name = parser.Expect<Scalar>().Value;
                    }
                    else if (nextProperty.Value.Equals("inputs"))
                    {
                        inputs = parser.ReadMappingOfStringString();
                    }
                }

                step = new TaskStep
                {
                    Name = name,
                    Inputs = inputs,
                    Reference = taskReference,
                };
            }
            else if (stepType.Value.Equals("export"))
            {
                while (parser.Peek<MappingEnd>() == null)
                {
                    parser.MoveNext();
                }

                step = new ExportStep();
            }
            else
            {
                throw new SyntaxErrorException(stepType.Start, stepType.End, $"Unknown step type {stepType.Value}");
            }

            parser.Expect<MappingEnd>();
            return step;
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
