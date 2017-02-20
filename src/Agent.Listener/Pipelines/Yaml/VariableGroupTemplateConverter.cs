using System;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace ConsoleApp2.Yaml
{
    public sealed class VariableGroupTemplateConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type)
        {
            return typeof(VariableGroupTemplate).Equals(type);
        }

        public object ReadYaml(IParser parser, Type type)
        {
            if (parser.Accept<Scalar>())
            {
                // ex. {{ ref to some named variable group }}
                // won't have this block at all if go mustache route
                return new VariableGroupTemplate(parser.Expect<Scalar>().Value);
            }
            else
            {
                return new VariableGroupTemplate(parser.ReadMappingOfStringString());
            }
        }

        public void WriteYaml(IEmitter emitter, object value, Type type)
        {
            throw new NotImplementedException();
        }
    }
}
