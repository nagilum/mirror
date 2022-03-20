namespace mirror
{
    public class CommandLineArguments
    {
        /// <summary>
        /// Timeout for each HTTP call.
        /// Defaults to 5 seconds.
        /// </summary>
        public int TimeoutMilliseconds { get; set; } = 5000;

        /// <summary>
        /// Local path for storing downloaded content and reports.
        /// Defaults to current working directory.
        /// </summary>
        public string StoragePath { get; set; } = Directory.GetCurrentDirectory();

        /// <summary>
        /// URL to parse.
        /// </summary>
        public Uri? Uri { get; set; }

        /// <summary>
        /// Parse the command-line arguments.
        /// </summary>
        /// <param name="cmdLineArgs">Command-line arguments.</param>
        public CommandLineArguments(string[] cmdLineArgs)
        {
            var skipNext = false;

            for (var i = 0; i < cmdLineArgs.Length; i++)
            {
                switch (cmdLineArgs[i])
                {
                    // Timeout for each HTTP call.
                    case "-t":
                        if (i == cmdLineArgs.Length - 1)
                        {
                            throw new Exception("The option -t must be followed by a number of milliseconds. Example: -t 5000");
                        }

                        if (!int.TryParse(cmdLineArgs[i + 1], out var timeout))
                        {
                            throw new Exception($"Unable to parse value '{cmdLineArgs[i + 1]}' as a number for timeout.");
                        }

                        this.TimeoutMilliseconds = timeout;
                        skipNext = true;
                        break;

                    // Local path for storing downloaded content and reports.
                    case "-p":
                        if (i == cmdLineArgs.Length - 1)
                        {
                            throw new Exception("The option -p must be followed by a path to use. Example: -p /mirror-storage");
                        }

                        var path = cmdLineArgs[i + 1];

                        if (!Directory.Exists(path))
                        {
                            throw new Exception($"The specified path '{path}' does not exist.");
                        }

                        this.StoragePath = path;
                        skipNext = true;
                        break;

                    // Uri.
                    default:
                        if (skipNext)
                        {
                            skipNext = false;
                            continue;
                        }

                        this.Uri = new Uri(cmdLineArgs[i]);
                        break;
                }
            }
        }

        /// <summary>
        /// Show info about the app and its various options.
        /// </summary>
        public static void ShowOptions()
        {
            ConsoleEx.WriteObjects(
                Environment.NewLine,
                "Usage: mirror <url> [options]",
                Environment.NewLine,
                Environment.NewLine,
                "Options:",
                Environment.NewLine,
                "  -t <MILLISECONDS>  Set timeout for all HTTP calls. Defaults to 5 seconds.",
                Environment.NewLine,
                "  -p <PATH>          Set path to store the local copies and reports. Defaults to current working directory.",
                Environment.NewLine,
                Environment.NewLine);
        }
    }
}