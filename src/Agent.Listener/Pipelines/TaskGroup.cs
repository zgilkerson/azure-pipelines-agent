using System;
using System.Collections.Generic;

namespace ConsoleApp2
{
    public class TaskGroup
    {
        public IDictionary<String, String> Inputs { get; set; }

        public List<TaskStep> Tasks { get; set; }
    }
}
