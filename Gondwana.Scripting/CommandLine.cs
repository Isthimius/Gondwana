using System;
using System.Collections.Generic;
using System.Text;

namespace Gondwana.Scripting
{
    // put code sample here syntax;
    // for WhiteSpace, Comments, SectionHeader, Commands
    public struct CommandLine
    {
        #region fields
        public readonly string Command;
        public readonly string[] Arguments;
        public readonly CommandLineTypes CommandLineType;
        #endregion

        public static CommandLine Empty
        {
            get { return new CommandLine(string.Empty); }
        }

        #region constructors
        public CommandLine(string line)
        {
            // trim the line before parsing it
            string tmp = line.Trim();

            if (tmp.Length == 0)
            {
                Command = string.Empty;
                Arguments = null;
                CommandLineType = CommandLineTypes.WhiteSpace;
            }
            else
            {
                if (tmp.Substring(0, 1) == "[")
                {
                    Command = line.Substring(1, line.Length - 2);
                    Arguments = null;
                    CommandLineType = CommandLineTypes.SectionHeader;
                }
                else
                {
                    if (tmp.Substring(0, 2) == "//")
                    {
                        // this is a Comment line
                        Command = line;
                        Arguments = null;
                        CommandLineType = CommandLineTypes.Comment;
                    }
                    else
                    {
                        // this is a Command
                        CommandLineType = CommandLineTypes.Command;

                        int idx = tmp.IndexOf(':');

                        // Command is the text to the left of the ":"
                        if (idx > 0)
                            Command = tmp.Substring(0, idx).Trim();
                        else
                            Command = string.Empty;

                        // Arguments is the array of values to the right of the ":"
                        tmp = line.Substring(idx + 1);

                        if (idx > 0)
                        {
                            // split out the args
                            string[] args = tmp.Split(',');

                            // trim each arg
                            for (int i = 0; i < args.Length; i++)
                                args[i] = args[i].Trim();

                            Arguments = args;
                        }
                        else
                            Arguments = null;
                    }
                }
            }
        }

        public CommandLine(CommandLineTypes commandLineType, string command, string[] args)
        {
            CommandLineType = commandLineType;
            command = command.Trim();

            switch (CommandLineType)
            {
                case CommandLineTypes.Comment:
                    if (command.Substring(0, 2) != "//")
                        command = "// " + command;

                    Command = command;
                    Arguments = null;
                    break;

                case CommandLineTypes.WhiteSpace:
                    Command = string.Empty;
                    Arguments = null;
                    break;

                case CommandLineTypes.SectionHeader:
                    // if the "[" was passed in, strip it for this type
                    if (command.Substring(0, 1) == "[")
                        command = command.Substring(1);

                    // if the "]" was passed in, strip it for this type
                    if (command.Substring(command.Length - 1, 1) == "]")
                        command = command.Substring(0, command.Length - 1);

                    Command = command;
                    Arguments = null;
                    break;

                case CommandLineTypes.Command:
                    Command = command;
                    Arguments = args;
                    break;

                default:
                    Command = string.Empty;
                    Arguments = null;
                    break;
            }
        }
        #endregion

        /// <summary>
        /// override of ValueType.ToString()
        /// </summary>
        /// <returns>The complete text string of the CommandLine as it is written in a script file</returns>
        public override string ToString()
        {
            string ret;

            switch (CommandLineType)
            {
                case CommandLineTypes.Comment:
                case CommandLineTypes.WhiteSpace:
                    ret = Command;
                    break;
                case CommandLineTypes.SectionHeader:
                    ret = "[" + Command + "]";
                    break;
                case CommandLineTypes.Command:
                    ret = Command + ":";
                    if (Arguments != null)
                    {
                        for (int i = 0; i < Arguments.Length; i++)
                        {
                            if (i == 0)
                                ret += Arguments[i];
                            else
                                ret += "," + Arguments[i];
                        }
                    }
                    break;
                default:
                    ret = base.ToString();
                    break;
            }

            return ret;
        }
    }
}
