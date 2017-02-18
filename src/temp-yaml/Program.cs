using System;
using System.Collections.Generic;
using System.IO;
using ConsoleApp2.Types;
using ConsoleApp2.Yaml;
using YamlDotNet.Serialization;

namespace ConsoleApp2
{
    public class PipelineResource
    {
        [YamlMember(Alias = "name")]
        public String Name
        {
            get;
            set;
        }

        [YamlMember(Alias = "type")]
        public String Type
        {
            get;
            set;
        }

        [YamlMember(Alias = "data")]
        public IDictionary<String, Object> Data
        {
            get;
            set;
        }
    }

    public abstract class PipelineJobStep
    {
        public String Name
        {
            get;
            set;
        }
    }

    public class ImportStep : PipelineJobStep
    {
    }

    public class ExportStep : PipelineJobStep
    {
        public String ResourceType
        {
            get;
            set;
        }

        public IDictionary<String, String> Inputs
        {
            get;
            set;
        }
    }

    public class GroupStep : PipelineJobStep
    {
    }

    public class TaskReference
    {
        public String Name
        {
            get;
            set;
        }

        public String Version
        {
            get;
            set;
        }
    }

    public class TaskStep : PipelineJobStep
    {
        public TaskReference Reference
        {
            get;
            set;
        }

        public IDictionary<String, String> Inputs
        {
            get;
            set;
        }
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
        public String Name
        {
            get;
            set;
        }

        [YamlMember(Alias = "target")]
        public IDictionary<String, String> Target
        {
            get;
            set;
        }

        [YamlMember(Alias = "variables")]
        public VariableGroupTemplate Variables
        {
            get;
            set;
        }

        [YamlMember(Alias = "steps")]
        public List<PipelineJobStep> Steps
        {
            get;
            set;
        }

        [YamlMember(Alias = "with_items")]
        public IteratorValueTemplate WithItems
        {
            get;
            set;
        }

        public IList<PipelineJob> ApplyInputs(IDictionary<String, Object> inputs)
        {
            return new List<PipelineJob>();
        }
    }

    public class Pipeline
    {
        public IDictionary<String, PipelineValue> Inputs
        {
            get;
            set;
        }

        public List<PipelineResource> Resources
        {
            get;
            set;
        }

        public List<PipelineJob> Jobs
        {
            get;
            set;
        }
    }

    public class PipelineTemplate
    {
        [YamlMember(Alias = "inputs")]
        public IDictionary<String, PipelineValue> Inputs
        {
            get;
            set;
        }

        [YamlMember(Alias = "resources")]
        public List<PipelineResource> Resources
        {
            get;
            set;
        }

        [YamlMember(Alias = "jobs")]
        public List<PipelineJobTemplate> Jobs
        {
            get;
            set;
        }

        public Pipeline Resolve(PipelineTemplateContext context)
        {
            throw new NotImplementedException();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            DeserializerBuilder dsBuilder = new DeserializerBuilder();
            dsBuilder.WithTypeConverter(new PipelineStepYamlConverter());
            dsBuilder.WithTypeConverter(new PipelineValueYamlConverter());
            dsBuilder.WithTypeConverter(new PipelineIteratorValueYamlConverter());
            dsBuilder.WithTypeConverter(new VariableGroupTemplateConverter());

            var deserializer = dsBuilder.Build();

            var pipelineTemplate = deserializer.Deserialize<PipelineTemplate>(File.ReadAllText("template.yaml"));

            var matrix = new List<IDictionary<String, PipelineValue>>();
            matrix.Add(new Dictionary<String, PipelineValue>() { { "buildConfiguration", "release" }, { "dotnet", "1.0" } });
            matrix.Add(new Dictionary<String, PipelineValue>() { { "buildConfiguration", "release" }, { "dotnet", "1.1" } });

            var context = new PipelineTemplateContext();
            context.Inputs.Add("matrix", matrix);

            var pipeline = pipelineTemplate.Resolve(context);
        }
    }
}
