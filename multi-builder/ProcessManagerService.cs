using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal class ProcessManagerService
{
    private HashSet<Process> Processes = new HashSet<Process>();

    public event EventHandler ProcessExited;

    public void RegisterProcess(Process process)
    {
        lock (Processes)
        {
            Processes.Add(process);
        }

        process.EnableRaisingEvents = true;
        process.Exited += (sender, e) =>
        {
            lock (Processes)
            {
                Processes.Remove(process);
            }
            ProcessExited?.Invoke(sender, e);
        };
    }
}

public class ProcessEventArgs : EventArgs
{
    public Process Process { get; private set; }

    public ProcessEventArgs(Process process)
    {
        Process = process;
    }
}
