using System;
using System.Collections.Generic;

public class Command
{
    public List<string> Invocations { get; set; }
    public string HelpString { get; set; }
    public Action<CommandParameters> Action { get; set; }
}

public class CommandParameters
{
    public IEnumerable<int>? ProjectNumbers { get; set; }
}
