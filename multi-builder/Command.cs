using System;
using System.Collections.Generic;

class Command
{
    public List<string> Invocations { get; set; }
    public string HelpString { get; set; }
    public Action<CommandParameters> Action { get; set; }
}

class CommandParameters
{
    public IEnumerable<int>? ProjectNumbers { get; set; }
}
