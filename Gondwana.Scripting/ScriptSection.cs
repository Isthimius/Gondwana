using System;
using System.Collections.Generic;
using System.Text;

namespace Gondwana.Scripting
{
    public class ScriptSection
    {
        #region public fields
        public List<CommandLine> Lines;
        #endregion

        #region constructors
        public ScriptSection(string[] lines)
        {
            Lines = new List<CommandLine>(lines.Length);
            for (int i = 0; i < lines.Length; i++)
                Lines.Add(new CommandLine(lines[i]));
        }

        public ScriptSection(List<CommandLine> lines)
        {
            Lines = lines;
        }

        public ScriptSection(CommandLine line)
        {
            Lines = new List<CommandLine>();
            Lines.Add(line);
        }
        #endregion

        public string Heading
        {
            get
            {
                // find the first instance of a SectionHeader line type and capture
                foreach (CommandLine cmdLn in Lines)
                {
                    if (cmdLn.CommandLineType == CommandLineTypes.SectionHeader)
                        return cmdLn.Command;
                }

                // if we made it here, no Heading was found
                return string.Empty;
            }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();

            foreach (CommandLine cmdLn in Lines)
                builder.AppendLine(cmdLn.ToString());

            return builder.ToString();
        }
    }
}
