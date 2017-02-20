// using System;
// using System.Collections.Generic;
// using ConsoleApp2.Types;
// using YamlDotNet.Core;
// using YamlDotNet.Core.Events;
// using YamlDotNet.Serialization;

// namespace ConsoleApp2.Yaml
// {
//     class StringYamlConverter : IYamlTypeConverter
//     {
//         public bool Accepts(Type type)
//         {
//             return type == typeof(String);;
//         }

//         public Object ReadYaml(
//             IParser parser, 
//             Type type)
//         {
//             var stringType = parser.Allow<Scalar>();
//             if (stringType != null)
//             {
//                 return stringType.Value;
//             }

//             return null;
//         }

//         public void WriteYaml(
//             IEmitter emitter, 
//             Object value, 
//             Type type)
//         {
//             throw new NotImplementedException();
//         }
//     }
// }
