using System;
using System.Collections.Generic;

namespace ConsoleApp2.Types
{
    public enum PipelineValueKind
    {
        String,
        StringArray,
        StringMapping,
        StringMappingArray,
        VariableReference,
    }

    public abstract class PipelineValue
    {
        protected PipelineValue(Object value)
        {
            this.Value = value;
        }

        public abstract PipelineValueKind Kind
        {
            get;
        }

        protected Object Value
        {
            get;
            set;
        }

        public virtual T Resolve<T>(PipelineTemplateContext context)
        {
            return (T)this.Value;
        }

        public static implicit operator PipelineValue(String value)
        {
            return new StringValue(value);
        }

        public static implicit operator PipelineValue(String[] value)
        {
            return new StringArrayValue(value);
        }

        public static implicit operator PipelineValue(List<String> value)
        {
            return new StringArrayValue(value);
        }

        public static implicit operator PipelineValue(Dictionary<String, String> value)
        {
            return new StringDictionaryValue(value);
        }
    }

    public class StringValue : PipelineValue
    {
        public StringValue(String value)
            : base(value)
        {
        }

        public override PipelineValueKind Kind
        {
            get
            {
                return PipelineValueKind.String;
            }
        }

        public new String Value
        {
            get
            {
                return (String)base.Value;
            }
            set
            {
                base.Value = value;
            }
        }

        public override T Resolve<T>(PipelineTemplateContext context)
        {
            return context.ResolveValue<T>(this.Value);
        }
    }

    public sealed class StringArrayValue : PipelineValue
    {
        public StringArrayValue(IList<String> value)
            : base(value)
        {
        }

        public override PipelineValueKind Kind
        {
            get
            {
                return PipelineValueKind.StringArray;
            }
        }

        public new IList<String> Value
        {
            get
            {
                return (IList<String>)base.Value;
            }
            set
            {
                base.Value = value;
            }
        }
    }

    public sealed class StringDictionaryValue : PipelineValue
    {
        public StringDictionaryValue(IDictionary<String, String> value)
            : base(value)
        {
        }

        public override PipelineValueKind Kind
        {
            get
            {
                return PipelineValueKind.StringMapping;
            }
        }

        public new IDictionary<String, String> Value
        {
            get
            {
                return (IDictionary<String, String>)base.Value;
            }
            set
            {
                base.Value = value;
            }
        }
    }

    public sealed class StringDictionaryArrayValue : PipelineValue
    {
        public StringDictionaryArrayValue(IList<IDictionary<String, String>> value)
            : base(value)
        {
        }

        public override PipelineValueKind Kind
        {
            get
            {
                return PipelineValueKind.StringMappingArray;
            }
        }

        public new IList<IDictionary<String, String>> Value
        {
            get
            {
                return (IList<IDictionary<String, String>>)base.Value;
            }
            set
            {
                base.Value = value;
            }
        }
    }
}
