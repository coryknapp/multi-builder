using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;

public class KillService
{
    private readonly IList<ManagedProject> managedProjects;

    public KillService(IList<ManagedProject> managedProjects)
    {
        this.managedProjects = managedProjects;
    }

    public void KillProject(ManagedProject managedProject)
    {
        this.KillProcessSafely(managedProject.BuildProcess);
        this.KillProcessSafely(managedProject.RunProcess);
    }

    public void KillAllProjects()
    {
        foreach (var project in managedProjects)
        {
            this.KillProject(project);
        }
    }

    private void KillProcessSafely(Process? process)
    {
        try
        {
            if (process == null || process.HasExited) return;

            // Try graceful shutdown first (if the process supports it)
            try
            {
                process.CloseMainWindow();
                if (process.WaitForExit(2000)) // Wait 2 seconds for graceful exit
                {
                    return;
                }
            }
            catch
            {
                // CloseMainWindow might fail, continue to Kill
            }

            // Force kill the entire process tree
            process.Kill(true); // true = kill entire process tree

            // Wait a bit to ensure it's dead
            if (process.WaitForExit(3000))
            {
                return;
            }
        }
        catch (Exception ex)
        {
        }
        finally
        {
            try
            {
                process?.Dispose();
            }
            catch { /* Ignore disposal errors */ }
        }
    }
}
