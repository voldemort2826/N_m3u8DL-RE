using System.Text;
using System.Text.RegularExpressions;

using Spectre.Console;

namespace N_m3u8DL_RE.Common.Log
{
    public sealed partial class NonAnsiWriter(TextWriter? baseWriter = null) : TextWriter
    {
        private readonly TextWriter _baseWriter = baseWriter ?? Console.Out;
        private readonly StringBuilder _buffer = new();
        private readonly Lock _lock = new();

        public override Encoding Encoding => _baseWriter.Encoding;

        public override void Write(char value)
        {
            lock (_lock)
            {
                _ = _buffer.Append(value);
                if (value == '\n' || _buffer.Length > 1024) // Flush on newline or buffer full
                {
                    FlushBuffer();
                }
            }
        }

        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            lock (_lock)
            {
                _ = _buffer.Append(value);
                if (value.Contains('\n') || _buffer.Length > 1024)
                {
                    FlushBuffer();
                }
            }
        }

        public override void WriteLine(string? value)
        {
            Write(value);
            Write('\n');
        }

        public override void Flush()
        {
            lock (_lock)
            {
                FlushBuffer();
                _baseWriter.Flush();
            }
        }

        private void FlushBuffer()
        {
            if (_buffer.Length == 0)
            {
                return;
            }

            string content = _buffer.ToString();
            _ = _buffer.Clear();

            string cleaned = RemoveAnsiEscapeSequences(content);
            if (!string.IsNullOrEmpty(cleaned))
            {
                _baseWriter.Write(cleaned);
            }
        }

        private static string RemoveAnsiEscapeSequences(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            // More comprehensive ANSI escape sequence removal
            string output = input;

            // Standard ANSI color/style codes
            output = AnsiColorRegex().Replace(output, "");

            // Cursor positioning and screen control
            output = AnsiCursorRegex().Replace(output, "");

            // Extended escape sequences (OSC, etc.)
            output = AnsiExtendedRegex().Replace(output, "");

            // Clean up excessive whitespace but preserve intentional formatting
            output = ExcessiveWhitespaceRegex().Replace(output, "\n");

            return output;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Flush();
                _baseWriter?.Dispose();
            }
            base.Dispose(disposing);
        }

        // More comprehensive regex patterns
        [GeneratedRegex(@"\x1B\[[0-9;]*[mGKHFJABCDnsuhl]", RegexOptions.Compiled)]
        private static partial Regex AnsiColorRegex();

        [GeneratedRegex(@"\x1B\[[?]?[0-9;]*[AKlhHJFCD]", RegexOptions.Compiled)]
        private static partial Regex AnsiCursorRegex();

        [GeneratedRegex(@"\x1B\][0-9];[^\x07]*\x07", RegexOptions.Compiled)]
        private static partial Regex AnsiExtendedRegex();

        [GeneratedRegex(@"[\r\n]\s*[\r\n]+", RegexOptions.Compiled)]
        private static partial Regex ExcessiveWhitespaceRegex();
    }

    /// <summary>
    /// Enhanced console capable of writing ANSI escape sequences with better configuration.
    /// </summary>
    public static class CustomAnsiConsole
    {
        private static IAnsiConsole? _console;
        private static readonly Lock _lock = new();

        public static IAnsiConsole Console
        {
            get
            {
                if (_console == null)
                {
                    lock (_lock)
                    {
                        _console ??= CreateDefaultConsole();
                    }
                }
                return _console;
            }
            private set => _console = value;
        }

        public static void InitConsole(bool forceAnsi = false, bool noAnsiColor = false, int? width = null)
        {
            lock (_lock)
            {
                AnsiConsoleSettings settings = new();

                // Configure ANSI support
                if (forceAnsi)
                {
                    settings.Ansi = AnsiSupport.Yes;
                    settings.Interactive = InteractionSupport.Yes;
                }
                else
                {
                    settings.Ansi = AnsiSupport.Detect;
                    settings.Interactive = InteractionSupport.Detect;
                }

                // Configure color output
                if (noAnsiColor)
                {
                    settings.Out = new AnsiConsoleOutput(new NonAnsiWriter());
                }

                // Set console width
                Console = AnsiConsole.Create(settings);
                if (width.HasValue)
                {
                    Console.Profile.Width = width.Value;
                }
                else if (forceAnsi)
                {
                    Console.Profile.Width = int.MaxValue;
                }
            }
        }

        private static IAnsiConsole CreateDefaultConsole()
        {
            AnsiConsoleSettings settings = new()
            {
                Ansi = AnsiSupport.Detect,
                Interactive = InteractionSupport.Detect
            };
            return AnsiConsole.Create(settings);
        }

        /// <summary>
        /// Writes the specified markup to the console.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public static void Markup(string value)
        {
            Console.Markup(value);
        }

        /// <summary>
        /// Writes the specified markup, followed by the current line terminator, to the console.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public static void MarkupLine(string value)
        {
            Console.MarkupLine(value);
        }

        /// <summary>
        /// Writes plain text without markup processing.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public static void Write(string value)
        {
            Console.Write(value);
        }

        /// <summary>
        /// Writes plain text followed by a line terminator.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public static void WriteLine(string value)
        {
            Console.WriteLine(value);
        }

        /// <summary>
        /// Clears the console if supported.
        /// </summary>
        public static void Clear()
        {
            Console.Clear();
        }

        /// <summary>
        /// Gets whether the console supports ANSI escape sequences.
        /// </summary>
        public static bool SupportsAnsi => Console.Profile.Capabilities.Ansi;

        /// <summary>
        /// Gets whether the console supports colors.
        /// </summary>
        public static bool SupportsColors => Console.Profile.Capabilities.ColorSystem != ColorSystem.NoColors;

        /// <summary>
        /// Resets console to default settings.
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                Console = CreateDefaultConsole();
            }
        }
    }

    /// <summary>
    /// Configuration options for console initialization.
    /// </summary>
    public sealed class ConsoleConfig
    {
        public bool ForceAnsi { get; set; }
        public bool NoAnsiColor { get; set; }
        public int? Width { get; set; }
        public bool BufferOutput { get; set; } = true;
        public TextWriter? CustomOutput { get; set; }

        public static ConsoleConfig Default => new();

        public static ConsoleConfig ForceAnsiWithColors => new()
        {
            ForceAnsi = true,
            NoAnsiColor = false
        };

        public static ConsoleConfig PlainTextOnly => new()
        {
            ForceAnsi = false,
            NoAnsiColor = true
        };
    }

    /// <summary>
    /// Extension methods for enhanced console operations.
    /// </summary>
    public static class ConsoleExtensions
    {
        /// <summary>
        /// Initializes console with a configuration object.
        /// </summary>
        public static void InitConsole(this ConsoleConfig config)
        {
            CustomAnsiConsole.InitConsole(config.ForceAnsi, config.NoAnsiColor, config.Width);
        }

        /// <summary>
        /// Writes colored text only if colors are supported.
        /// </summary>
        public static void WriteColored(this IAnsiConsole console, string text, string color)
        {
            if (CustomAnsiConsole.SupportsColors)
            {
                console.MarkupLine($"[{color}]{text.EscapeMarkup()}[/]");
            }
            else
            {
                console.WriteLine(text);
            }
        }
    }
}
