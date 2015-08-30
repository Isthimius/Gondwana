using System;
using System.Collections.Generic;
using System.Text;
using Gondwana.Common.Enums;

namespace Gondwana.Scripting
{
    public class EngineScriptSectionBuilder
    {
        private List<CommandLine> lines;

        public EngineScriptSectionBuilder()
        {
            Clear();
        }

        public void AddCommentsLine(string comments)
        {
            lines.Add(new CommandLine(CommandLineTypes.Comment, comments, null));
        }

        public void AddWhiteSpace()
        {
            AddWhiteSpace(1);
        }

        public void AddWhiteSpace(int whiteCount)
        {
            for (int i = 0; i < whiteCount; i++)
                lines.Add(new CommandLine());
        }

        #region [ENGINE] Commands
        public void AddExecuteCommand(string scriptFile)
        {
            lines.Add(new CommandLine("EXEC:" + scriptFile));
        }

        public void AddExecuteCommand(string[] scriptFiles)
        {
            for (int i = scriptFiles.GetLowerBound(0); i <= scriptFiles.GetUpperBound(0); i++)
                AddExecuteCommand(scriptFiles[i]);
        }

        public void AddLoadWavSoundCommand(string wavName, string wavFilePath)
        {
            lines.Add(new CommandLine("WAV:" + wavName + "," + wavFilePath));
        }

        public void AddLoadWavSoundCommand(Dictionary<string, string> wavNamesFilePaths)
        {
            foreach (KeyValuePair<string, string> wavNameFilePath in wavNamesFilePaths)
                AddLoadWavSoundCommand(wavNameFilePath.Key, wavNameFilePath.Value);
        }

        public void AddConfigFileCommand(string configPath)
        {
            lines.Add(new CommandLine("CONFIG:" + configPath));
        }

        public void AddResolutionCommand(int width, int height)
        {
            lines.Add(new CommandLine("RESOLUTION:" + width.ToString() + "," + height.ToString()));
        }

        public void AddCursorCommand(int cursorFile)
        {
            lines.Add(new CommandLine("CURSOR:" + cursorFile));
        }
        #endregion

        public void Clear()
        {
            lines = new List<CommandLine>();
            lines.Add(new CommandLine(CommandLineTypes.SectionHeader, "ENGINE", null));
        }

        public ScriptSection ToScriptSection()
        {
            return new ScriptSection(lines);
        }

        public override string ToString()
        {
            return ToScriptSection().ToString();
        }
    }
}
