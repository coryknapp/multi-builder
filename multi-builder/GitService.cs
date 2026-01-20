using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class GitService
{
    private readonly OptionService OptionService;

    public GitService(OptionService optionService)
    {
        this.OptionService = optionService;
    }

    public async Task<string?> GetActiveBranchDisplayNameAsync(string directoryPath)
    {
        var branchName = await this.GetActiveBranchNameAsync(directoryPath);

        return branchName != null ? this.TruncateBranchName(branchName) : null;
    }

    public async Task<string?> GetActiveBranchNameAsync(string directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
        {
            return null;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --abbrev-ref HEAD",
                WorkingDirectory = directoryPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                return output.Trim();
            }

            return null;
        }
        catch (Exception)
        {
            // Git command failed or git is not available
            return null;
        }
    }

    private string TruncateBranchName(string branchName)
    {
        if (branchName.Length <= this.OptionService.MaxGitBranchLength)
        {
            return branchName;
        }
        return branchName.Substring(0, this.OptionService.MaxGitBranchLength - 3) + "...";
    }
}
