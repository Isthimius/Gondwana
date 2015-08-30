using System;
using System.Collections.Generic;
using System.Text;

namespace Gondwana.Logging
{
    public struct LogRecord
    {
        public static readonly string DEFAULT_DELIMITER = ";";

        public DateTime LogTime;
        public LoggingLevel LogLevel;
        public string LogUser;
        public string LogApp;
        public string LogMsg;

        public LogRecord(string line)
        {
            this = new LogRecord(line, DEFAULT_DELIMITER);
        }

        public LogRecord(string line, string delimiter)
        {
            string[] fields = line.Split(delimiter.ToCharArray());

            try
            {
                LogTime = DateTime.Parse(fields[0]);
                LogLevel = (LoggingLevel)Enum.Parse(typeof(LoggingLevel), fields[1], true);
                LogUser = fields[2];
                LogApp = fields[3];
                LogMsg = fields[4];
            }
            catch (Exception e)
            {
                throw new LogFileException(
                    "Cannot parse line '" + line + "' with delimiter '" + delimiter + "'", e);
            }
        }

        public override string ToString()
        {
            return ToString(DEFAULT_DELIMITER);
        }

        public string ToString(string delimiter)
        {
            string ret = DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString();
            ret += delimiter + LogLevel.ToString();
            ret += delimiter + LogUser;
            ret += delimiter + LogApp;
            ret += delimiter + LogMsg;
            return ret;
        }
    }
}
