using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;

namespace Gondwana.Scripting
{
    public static class Parser
    {
        #region public static fields
        public static int WriteWhiteSpaceAbove = 0;
        public static int WriteWhiteSpaceBelow = 1;
        public static List<ParserError> Errors = new List<ParserError>();
        #endregion

        #region private static fields
        private static int lineNumber;
        #endregion

        #region public static methods
        public static void WriteToFile(string file, FileMode fileMode, IScriptable scriptable)
        {
            List<IScriptable> scriptableList = new List<IScriptable>();
            scriptableList.Add(scriptable);
            WriteToFile(file, fileMode, scriptableList);
        }

        public static void WriteToFile(string file, FileMode fileMode, ICollection<IScriptable> scriptableList)
        {
            // if passing in null or an empty collection, just return
            if ((scriptableList == null) || (scriptableList.Count == 0))
                return;

            // open the specified file stream
            using (FileStream filestream = File.Open(file, fileMode))
            {
                using (StreamWriter writer = new StreamWriter(filestream))
                {
                    // write each scriptable object to the stream
                    foreach (IScriptable scriptable in scriptableList)
                    {
                        // include "above" whitespace
                        for (int i = 0; i < WriteWhiteSpaceAbove; i++)
                            writer.WriteLine();

                        // write the SectionHeader
                        writer.WriteLine("[" + scriptable.GetType().ToString() + "]");

                        // write the Script value defined by the scriptable instance
                        foreach (CommandLine line in scriptable.Script.Lines)
                        {
                            switch (line.CommandLineType)
                            {
                                case CommandLineTypes.Comment:
                                    if (scriptable.IncludeScriptComments)
                                        writer.WriteLine(line.ToString());
                                    break;

                                case CommandLineTypes.WhiteSpace:
                                    if (scriptable.IncludeScriptWhiteSpace)
                                        writer.WriteLine(line.ToString());
                                    break;

                                case CommandLineTypes.Command:
                                    writer.WriteLine(line.ToString());
                                    break;

                                case CommandLineTypes.SectionHeader:
                                default:
                                    // SectionHeader already written,
                                    // and shouldn't get to default
                                    break;
                            }
                        }

                        // include "below" whitespace
                        for (int i = 0; i < WriteWhiteSpaceBelow; i++)
                            writer.WriteLine();
                    }

                    // save to the file stream
                    writer.Flush();
                }
            }
        }

        public static void WriteToFile(string file, FileMode fileMode, ScriptSection section)
        {
            List<ScriptSection> sections = new List<ScriptSection>();
            sections.Add(section);
            WriteToFile(file, fileMode, sections);
        }

        public static void WriteToFile(string file, FileMode fileMode, List<ScriptSection> sections)
        {
            // if passing in null or empty section, just return
            if ((sections == null) || (sections.Count == 0))
                return;

            // open the specified file stream
            using (FileStream filestream = File.Open(file, fileMode))
            {
                using (StreamWriter writer = new StreamWriter(filestream))
                {
                    // write each ScriptSection to the file
                    foreach (ScriptSection section in sections)
                    {
                        if ((section != null) && (section.Lines.Count != 0))
                            writer.WriteLine(section.ToString());
                    }

                    // save to the file stream
                    writer.Flush();
                }
            }
        }

        public static List<ScriptSection> ReadFromFile(string file)
        {
            List<ScriptSection> sections = new List<ScriptSection>();
            StreamReader reader = null;

            try
            {
                // open the file for read-only access
                using (FileStream filestream = File.Open(file, FileMode.Open, FileAccess.Read))
                {
                    reader = new StreamReader(filestream);

                    // read the entire file into string array, split by line feeds
                    string[] lines = reader.ReadToEnd().Split('\n');

                    // break lines into List of ScriptSection objects
                    sections = FindScriptSections(lines);
                }
            }
            catch (Exception e)
            {
                MethodBase method = MethodBase.GetCurrentMethod();
                string errMsg = "Exception thrown in " + method.DeclaringType.Name + "." + method.Name + "()";
                Errors.Add(ParserError.New(file, errMsg, e, null, CommandLine.Empty));
            }
            finally
            {
                if (reader != null)
                    reader.Dispose();
            }

            return sections;
        }

        public static List<ScriptReadableBase> ExecuteScript(string file)
        {
            return ExecuteScript(ReadFromFile(file));
        }

        public static List<ScriptReadableBase> ExecuteScript(List<ScriptSection> sections)
        {
            // reset line number
            lineNumber = 1;

            List<ScriptReadableBase> results = new List<ScriptReadableBase>();

            // each ScriptSection instantiates an object; add each object to the results List
            foreach (ScriptSection section in sections)
            {
                try
                {
                    // [ENGINE] headers are handled separately
                    if (section.Heading.ToUpper() == "ENGINE")
                        ExecuteEngineScript(section);
                    else
                    {
                        // not [ENGINE]; instantiate ScriptReadableBase object with passed-in ScriptSection
                        Type scriptReadableType = Type.GetType(section.Heading);
                        results.Add((ScriptReadableBase)Activator.CreateInstance(scriptReadableType, section));
                    }
                }
                catch (Exception e)
                {
                    MethodBase method = MethodBase.GetCurrentMethod();
                    string errMsg = "Unable to execute script; exception thrown in " + method.DeclaringType.Name + "." + method.Name + "()";
                    Errors.Add(ParserError.New(section.ToString(), errMsg, e, null, CommandLine.Empty));
                }
                finally
                {
                    // accumulate line numbers
                    lineNumber += section.Lines.Count;
                }
            }

            return results;
        }
        #endregion

        #region private static methods
        private static List<ScriptSection> FindScriptSections(string[] lines)
        {
            int lineNumber = 0;
            string line = string.Empty;
            List<CommandLine> cmds = new List<CommandLine>(lines.Length);

            // convert strings into List of CommandLine objects
            try
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    lineNumber++;
                    line = lines[i];
                    cmds.Add(new CommandLine(line));
                }
            }
            catch (Exception e)
            {
                string msg = "Error parsing script.  See inner exception for more details.";
                throw new ParserException(lineNumber, line, msg, e);
            }

            // now break the List of CommandLine objects into a List of ScriptSection objects
            List<ScriptSection> sections = new List<ScriptSection>();
            List<CommandLine> currentSectionCommands = new List<CommandLine>();

            foreach (CommandLine cmd in cmds)
            {
                // new section header found, so capture currentSectionCommands before continuing
                if (cmd.CommandLineType == CommandLineTypes.SectionHeader)
                {
                    // add previous currentSectionCommands to the sections List
                    // and reset currentSectionCommands
                    if (currentSectionCommands.Count > 0)
                    {
                        sections.Add(new ScriptSection(currentSectionCommands));
                        currentSectionCommands = new List<CommandLine>();
                    }
                }

                // add the CommandLine to the currentSectionCommands List
                currentSectionCommands.Add(cmd);
            }

            // capture the last section in the List
            if (currentSectionCommands.Count > 0)
                sections.Add(new ScriptSection(currentSectionCommands));

            // return the List of ScriptSection objects
            return sections;
        }

        private static void ExecuteEngineScript(ScriptSection section)
        {
            // TODO: refactor
            throw new NotImplementedException();

            //int lineNumber = 0;

            //try
            //{
            //    foreach (CommandLine line in section.Lines)
            //    {
            //        // increment the line number regardless of the CommandLineType
            //        lineNumber++;

            //        if (line.CommandLineType == CommandLineTypes.Command)
            //        {
            //            switch (line.Command.ToUpper())
            //            {
            //                case "EXEC":
            //                    // arg 0 --> path of script file to execute
            //                    ExecuteScript(line.Arguments[0]);
            //                    break;

            //                case "WAV":
            //                    // arg 0 --> custom / friendly stream name
            //                    // arg 1 --> path to WAV file
            //                    WavSounds.LoadStream(line.Arguments[0], line.Arguments[1]);
            //                    break;

            //                case "CONFIG":
            //                    // arg 0 --> path of configuration file to load
            //                    Settings.LoadEngineSettingsFile(line.Arguments[0]);
            //                    break;

            //                case "RENDERMODE":
            //                    // arg 0 --> ToString() value of GridRenderMode to use
            //                    Engine.RenderMode = (GridRenderMode)Enum.Parse(
            //                        typeof(GridRenderMode), line.Arguments[0], true);
            //                    break;

            //                case "RESOLUTION":
            //                    // arg 0 --> width in pixels
            //                    // arg 1 --> height in pixels
            //                    ScreenResolution.SetScreenResolution(
            //                        new Size(int.Parse(line.Arguments[0]), int.Parse(line.Arguments[1])));
            //                    break;

            //                case "CURSOR":
            //                    // arg 0 --> path to cursor file
            //                    Cursors.SetCursor(line.Arguments[0]);
            //                    break;

            //                default:
            //                    throw new ArgumentException("Invalid [ENGINE] Command in statement: "
            //                        + line.ToString() + " at line " + lineNumber.ToString());
            //            }
            //        }
            //    }
            //}
            //catch (Exception e)
            //{
            //    throw new ParserException(lineNumber, section.ToString(),
            //        "Error executing [ENGINE] script", e);
            //}
        }
        #endregion
    }
}
