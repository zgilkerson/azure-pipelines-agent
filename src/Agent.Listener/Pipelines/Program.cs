using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ConsoleApp2.TextTemplating;
using ConsoleApp2.Types;
using ConsoleApp2.Yaml;
using YamlDotNet.Serialization;

namespace ConsoleApp2
{
    public class PipelineResource
    {
        [YamlMember(Alias = "name")]
        public String Name { get; set; }

        [YamlMember(Alias = "type")]
        public String Type { get; set; }

        [YamlMember(Alias = "data")]
        public IDictionary<String, Object> Data { get; set; }
    }

    public abstract class PipelineJobStep
    {
        public String Name { get; set; }
    }

    public class ImportStep : PipelineJobStep
    {
    }

    public class ExportStep : PipelineJobStep
    {
        public String ResourceType { get; set; }

        public IDictionary<String, String> Inputs { get; set; }
    }

    public class GroupStep : PipelineJobStep
    {
    }

    public class TaskReference
    {
        public String Name { get; set; }

        public String Version { get; set; }
    }

    public class TaskStep : PipelineJobStep
    {
        public TaskReference Reference { get; set; }

        public IDictionary<String, String> Inputs { get; set; }
    }

    public class PipelineJob
    {
    }

    public class PipelineJobTemplate
    {
        public PipelineJobTemplate()
        {
            this.Steps = new List<PipelineJobStep>();
        }

        [YamlMember(Alias = "name")]
        public String Name { get; set; }

        [YamlMember(Alias = "target")]
        public IDictionary<String, String> Target { get; set; }

        [YamlMember(Alias = "variables")]
        public VariableGroupTemplate Variables { get; set; }

        [YamlMember(Alias = "steps")]
        public List<PipelineJobStep> Steps { get; set; }

        [YamlMember(Alias = "with_items")]
        public IteratorValueTemplate WithItems { get; set; }

        public IList<PipelineJob> ApplyInputs(IDictionary<String, Object> inputs)
        {
            return new List<PipelineJob>();
        }
    }

    // public class Pipeline
    // {
    //     public IDictionary<String, PipelineValue> Inputs { get; set; }

    //     public List<PipelineResource> Resources { get; set; }

    //     public List<PipelineJob> Jobs { get; set; }
    // }

    public sealed class PipelineTemplate
    {
        // [YamlMember(Alias = "inputs")]
        // public IDictionary<String, PipelineValue> Inputs { get; set; }

        [YamlMember(Alias = "resources")]
        public List<PipelineResource> Resources { get; set; }

        [YamlMember(Alias = "jobs")]
        public List<PipelineJobTemplate> Jobs { get; set; }

        // public Pipeline Resolve(PipelineTemplateContext context)
        // {
        //     throw new NotImplementedException();
        // }
    }

    public sealed class Pipeline
    {
        // [YamlMember(Alias = "inputs")]
        // public IDictionary<String, PipelineValue> Inputs { get; set; }

        [YamlMember(Alias = "resources")]
        public List<PipelineResource> Resources { get; set; }

        [YamlMember(Alias = "jobs")]
        public List<PipelineJobTemplate> Jobs { get; set; }

        [YamlMember(Alias = "template")]
        public PipelineTemplateReference Template { get; set; }

        // public Pipeline Resolve(PipelineTemplateContext context)
        // {
        //     throw new NotImplementedException();
        // }
    }

    public sealed class PipelineTemplateReference
    {
        [YamlMember(Alias = "name")]
        public String Name { get; set; }

        [YamlMember(Alias = "parameters")]
        public IDictionary<String, Object> Parameters { get; set; }
    }

    public static class Program2
    {
        public static async Task Main2(string[] args)
        {
            String filePath = args[1];
            Pipeline pipeline = await ReadAsync<Pipeline>(filePath);
            if (pipeline.Template != null)
            {
                String directoryPath = Path.GetDirectoryName(filePath);
                String templatePath = Path.Combine(directoryPath, pipeline.Template.Name);
                PipelineTemplate template = await ReadAsync<PipelineTemplate>(templatePath, mustacheContext: pipeline.Template.Parameters);
            }

            // DeserializerBuilder dsBuilder = new DeserializerBuilder();
            // dsBuilder.WithTypeConverter(new PipelineStepYamlConverter());
            // dsBuilder.WithTypeConverter(new PipelineValueYamlConverter());
            // dsBuilder.WithTypeConverter(new PipelineIteratorValueYamlConverter());
            // dsBuilder.WithTypeConverter(new VariableGroupTemplateConverter());
            // var deserializer = dsBuilder.Build();
            // var pipelineTemplate = deserializer.Deserialize<PipelineTemplate>(File.ReadAllText(args[1]));



            //"template.yaml"));

            // var matrix = new List<IDictionary<String, PipelineValue>>();
            // matrix.Add(new Dictionary<String, PipelineValue>() { { "buildConfiguration", "release" }, { "dotnet", "1.0" } });
            // matrix.Add(new Dictionary<String, PipelineValue>() { { "buildConfiguration", "release" }, { "dotnet", "1.1" } });

            // var context = new PipelineTemplateContext();
            // context.Inputs.Add("matrix", matrix);

            // var pipeline = pipelineTemplate.Resolve(context);
            //new System.IO.FileStream()
        }

        private static async Task<T> ReadAsync<T>(String path, IDictionary<String, Object> mustacheContext = null)
        {
            String mustacheReplaced;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
            {
                StreamReader reader = null;
                try
                {
                    // Read front-matter
                    IDictionary<String, Object> frontMatter = null;
                    reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
                    String line = await reader.ReadLineAsync();
                    if (!String.Equals(line, "---", StringComparison.Ordinal))
                    {
                        // No front-matter. Reset the stream.
                        reader.Dispose();
                        stream.Seek(0, SeekOrigin.Begin);
                        reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: false);
                    }
                    else
                    {
                        // Deseralize front-matter.
                        StringBuilder frontMatterBuilder = new StringBuilder();
                        while (true)
                        {
                            line = await reader.ReadLineAsync();
                            if (line == null)
                            {
                                // TODO: better error message.
                                throw new Exception("expected end of front-matter section");
                            }
                            else if (String.Equals(line, "---", StringComparison.Ordinal))
                            {
                                break;
                            }
                            else
                            {
                                frontMatterBuilder.AppendLine(line);
                            }
                        }

                        var frontMatterDeserializer = new Deserializer();
                        // todo: try/catch and better error message.
                        frontMatter = frontMatterDeserializer.Deserialize<IDictionary<String, Object>>(frontMatterBuilder.ToString());
                    }

                    // Merge the mustache replace context.
                    frontMatter = frontMatter ?? new Dictionary<String, Object>();
                    if (mustacheContext != null)
                    {
                        foreach (KeyValuePair<String, Object> pair in mustacheContext)
                        {
                            frontMatter[pair.Key] = pair.Value;
                        }
                    }

                    // Mustache-replace
                    var mustacheParser = new MustacheTemplateParser();
                    mustacheReplaced = mustacheParser.ReplaceValues(
                        template: await reader.ReadToEndAsync(),
                        replacementContext: frontMatter);
                }
                finally
                {
                    reader?.Dispose();
                    reader = null;
                }
            }


            // Deserialize
            DeserializerBuilder deserializerBuilder = new DeserializerBuilder();
            deserializerBuilder.WithTypeConverter(new PipelineStepYamlConverter());
            deserializerBuilder.WithTypeConverter(new PipelineValueYamlConverter());
            deserializerBuilder.WithTypeConverter(new PipelineIteratorValueYamlConverter());
            deserializerBuilder.WithTypeConverter(new VariableGroupTemplateConverter());
            Deserializer deserializer = deserializerBuilder.Build();
            T pipeline = deserializer.Deserialize<T>(mustacheReplaced);
            Console.WriteLine($"----------------------------------------");
            Console.WriteLine($"----------------------------------------");
            Console.WriteLine($"Loaded: {path}");
            Console.WriteLine($"----------------------------------------");
            Dump(pipeline);
            Console.WriteLine($"----------------------------------------");
            Console.WriteLine($"----------------------------------------");
            return pipeline;
        }

        private static void Dump(Object o)
        {
            var s = new Serializer();
            Console.WriteLine(s.Serialize(o));
        }

        private sealed class FileSections
        {
            public IDictionary<String, Object> FrontMatter { get; set; }

            public String Content { get; set; }
        }
    }
}
