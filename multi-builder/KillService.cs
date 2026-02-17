using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class KillService
{
    public void KillProject(ManagedProject managedProject, bool verbose = false)
    {
        this.KillProcessSafely(managedProject.BuildProcess, $"{managedProject.Name} build process", verbose);
        this.KillProcessSafely(managedProject.RunProcess, $"{managedProject.Name} run process", verbose);
    }

    private void KillProcessSafely(Process? process, string description, bool verbose = false)
    {
        try
        {
            if (process == null || process.HasExited) return;

            if (verbose)
                AnsiConsole.MarkupLine($"[yellow]Terminating {description}...[/]");

            // Try graceful shutdown first (if the process supports it)
            try
            {
                process.CloseMainWindow();
                if (process.WaitForExit(2000)) // Wait 2 seconds for graceful exit
                {
                    if (verbose)
                        AnsiConsole.MarkupLine($"[green]{description} exited gracefully[/]");
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
                if (verbose)
                    AnsiConsole.MarkupLine($"[green]{description} terminated[/]");
            }
            else
            {
                if (verbose)
                    AnsiConsole.MarkupLine($"[red]{description} did not respond to termination[/]");
            }
        }
        catch (Exception ex)
        {
            if (verbose)
                AnsiConsole.MarkupLine($"[red]Error killing {description}: {ex.Message}[/]");
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
