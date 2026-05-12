# Multi-Builder

 I wrote this because my org uses a ton of microservices that need to run at the same time, and I couldn't stand having 30 swagger windows open at once.

## Configuration

Multi-Builder uses a JSON configuration file to define which projects to manage.

### Config File Locations
The tool searches for `multi-builder.json` in this order:
1. Custom path via `--config <path>`
2. Current working directory
3. User profile directory (`%USERPROFILE%\multi-builder.json`)

### Example Configuration

```json
{
  "Directories": [
    "%USERPROFILE%\\Code\\MyApp\\Services\\Gateway",
    "%USERPROFILE%\\Code\\MyApp\\Services\\Auth",
    "%USERPROFILE%\\Code\\MyApp\\Services\\Users",
    "%USERPROFILE%\\Code\\MyApp\\Services\\Orders",
    "C:\\Projects\\MyApp\\WebUI"
  ],
  "ConcurrentBuildProcesses": 3,
  "MaxRetryAttempts": 4,
  "BuildCommand": "dotnet build -c Debug",
  "RunCommand": "dotnet run --no-build --no-restore",
  "DumpBuildOutputToFile": false,
  "MaxGitBranchLength": 32
}
```

### Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `Directories` | **(required)** | List of project directories to manage. Supports environment variables like `%USERPROFILE%` |
| `ConcurrentBuildProcesses` | `2` | Number of projects that can build simultaneously |
| `MaxRetryAttempts` | `4` | How many times to retry a failed build |
| `HideCursorSeconds` | `300` | Auto-hide cursor after inactivity (0 = never) |
| `BuildCommand` | `dotnet build -c Debug` | Command to build projects |
| `RunCommand` | `dotnet run --no-build --no-restore` | Command to run projects |
| `DumpBuildOutputToFile` | `false` | Save build output to log files |
| `MaxGitBranchLength` | `32` | Max characters for Git branch display |

### Command Line Options
```powershell
# Use specific config file
multi-builder --config C:\my-projects\services.json

# Override config with command line
multi-builder -d "C:\Project1" "C:\Project2" -c 4

# Generate sample config
multi-builder --generate-config

# Available options:
#   --config, -cfg              Path to JSON config file
#   --directories, -d           Project directories (overrides config)
#   --concurrent-build-processes, -c   Number of concurrent builds
#   --hide-cursor, -hr          Hide cursor timeout (seconds)
#   --git-branch-length         Max git branch display length
#   --build-command, -bc        Custom build command
#   --run-command, -rc          Custom run command
#   --generate-config           Generate sample config file
```

### Environment Variables
Use Windows environment variables in your config paths:
- `%USERPROFILE%` - User's home directory
- `%APPDATA%` - Application data folder
- Any other Windows environment variable

### Log Files
When you view logs (press `L` or `O`), Multi-Builder:
1. Writes logs to `{ProjectName}_build.log` or `{ProjectName}_run.log` in the project directory
2. Opens the file in Notepad++ (or default text editor)
3. Updates the file each time you view logs

### Smart Retry Logic
Multi-Builder detects build failures caused by resource contention (like locked DLLs) and automatically retries them. Perfect for large solutions with shared dependencies.

### Git Integration
Each project displays its active Git branch, making it easy to verify you're working on the correct branch across all services.