using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Net;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

partial class Program
{
    private static void RunProject(ManagedProject managedProject)
    {
        Console.WriteLine($"Running '{RunCommand}' in '{managedProject.Name}'");

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {RunCommand}",
            WorkingDirectory = managedProject.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi };
        managedProject.RunProcess = process;

        process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                managedProject.LiveOutput.Add(args.Data);
            }

            if (managedProject.PrintOutputInRealTime)
            {
                //if (managedProject.OutputStreamReader == null)
                //{
                //    managedProject.OutputStreamReader = new LineStreamReader();
                //    managedProject.OutputStreamReader.LineReceived += (line) =>
                //    {
                //        WriteHeaderLine($"Output for {managedProject.Name}");
                //        Console.WriteLine(line);
                //    };
                //}
                WriteHeaderLine($"Output for {managedProject.Name}");
                Console.WriteLine(args.Data);
                //managedProject.OutputStreamReader.Add(args.Data);
            }
        };
        process.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                managedProject.LiveOutput.Add(args.Data);
            }

            if (managedProject.PrintOutputInRealTime)
            {
                WriteHeaderLine($"--- Error output for {managedProject.Name} ---");
                WriteErrorLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }
}
