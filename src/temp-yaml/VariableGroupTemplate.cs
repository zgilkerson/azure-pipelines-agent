using System;
using System.Collections.Generic;

namespace ConsoleApp2
{
    public class VariableGroupTemplate
    {
        public VariableGroupTemplate(String templateValue)
        {
            m_templateValue = templateValue;
        }

        public VariableGroupTemplate(IDictionary<String, String> literalValue)
        {
            m_literalValue = literalValue;
        }

        public IDictionary<String, String> Resolve(PipelineTemplateContext context)
        {
            if (m_literalValue != null)
            {
                return m_literalValue;
            }
            else
            {
                return context.ResolveValue<IDictionary<String, String>>(m_templateValue);
            }
        }

        private readonly String m_templateValue;
        private readonly IDictionary<String, String> m_literalValue;
    }
}
