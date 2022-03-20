namespace mirror
{
    public static class ConsoleEx
    {
        /// <summary>
        /// Lock object.
        /// </summary>
        private static readonly object ConsoleLock = new();

        /// <summary>
        /// Write an exception to console.
        /// </summary>
        /// <param name="ex">Exception to write.</param>
        public static void WriteException(Exception ex)
        {
            var list = new List<object>
            {
                ConsoleColor.Red,
                "Error",
                (byte) 0x00,
                ": "
            };

            while (true)
            {
                list.Add($"{ex.Message}{Environment.NewLine}");

                if (ex.InnerException == null)
                {
                    break;
                }

                ex = ex.InnerException;
            }

            list.Add(Environment.NewLine);

            WriteObjects(list.ToArray());
        }

        /// <summary>
        /// Use objects to manupulate the console.
        /// </summary>
        /// <param name="list">List of objects.</param>
        public static void WriteObjects(params object[] list)
        {
            lock (ConsoleLock)
            {
                foreach (var item in list)
                {
                    // Is is a color?
                    if (item is ConsoleColor cc)
                    {
                        Console.ForegroundColor = cc;
                    }

                    // Do we need to reset the color?
                    else if (item is byte b &&
                             b == 0x00)
                    {
                        Console.ResetColor();
                    }

                    // Anything else, just write it to console.
                    else
                    {
                        Console.Write(item);
                    }
                }
            }
        }
    }
}