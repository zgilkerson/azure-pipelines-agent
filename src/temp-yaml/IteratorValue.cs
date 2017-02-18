using System;
using System.Collections.Generic;

namespace ConsoleApp2
{
    public abstract class IteratorValue
    {
        public abstract T Resolve<T>(PipelineTemplateContext context);
    }

    public sealed class IteratorValueLiteral
    {

    }

    public sealed class IteratorValueExpression
    {

    }

    public class IteratorValueTemplate
    {
        public IteratorValueTemplate(String value)
        {
            m_templateValue = value;
        }

        public IteratorValueTemplate(List<String> value)
        {
            m_stringArrayLiteral = value;
        }

        public IteratorValueTemplate(List<Dictionary<String, String>> value)
        {
            m_mappingArrayLiteral = value;
        }

        public T Resolve<T>(PipelineTemplateContext context)
        {
            if (m_stringArrayLiteral != null)
            {
                return (T)(Object)m_stringArrayLiteral;
            }
            else if (m_mappingArrayLiteral != null)
            {
                return (T)(Object)m_mappingArrayLiteral;
            }
            else
            {
                return context.ResolveValue<T>(m_templateValue);
            }
        }

        private String m_templateValue;
        private List<String> m_stringArrayLiteral;
        private List<Dictionary<String, String>> m_mappingArrayLiteral;
    }
}
