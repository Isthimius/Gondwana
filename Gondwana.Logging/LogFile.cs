using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.IO;

namespace Gondwana.Logging
{
    public class LogFile : IDisposable
    {
        #region events
        public delegate void RecordsWrittenToLogEventHandler(LogFile log, int numOfRecords);
        public event RecordsWrittenToLogEventHandler RecordsWrittenToLogFile;
        #endregion

        #region private fields
        private LoggingLevel loggingLevel = LoggingLevel.LogDebug;
        private List<LogRecord> logRecords = new List<LogRecord>();
        private WriteFrequency writeFrequency = WriteFrequency.Automatic;
        private string fileName;
        private FileMode logFileMode;
        private LogFileOpenType logOpenType;
        private int lineLimitBeforeFlush = 1;
        private string logFileDelimiter = LogRecord.DEFAULT_DELIMITER;
        private FileStream stream = null;
        private StreamWriter writer = null;
        private StreamReader reader = null;
        #endregion

        #region constructor / finalizer
        public LogFile(string file, FileMode fileMode, LogFileOpenType openType)
        {
            fileName = file;
            logFileMode = fileMode;
            logOpenType = openType;

            OpenFileStreamWriterReader(file, fileMode, openType);
        }

        public LogFile(string file, FileMode fileMode, LogFileOpenType openType, string delimiter)
        {
            fileName = file;
            logFileMode = fileMode;
            logOpenType = openType;
            logFileDelimiter = delimiter;

            OpenFileStreamWriterReader(file, fileMode, openType);
        }

        ~LogFile()
        {
            Dispose();
        }
        #endregion

        #region public properties
        public bool Dirty
        {
            get
            {
                if (logOpenType == LogFileOpenType.Read)
                    return false;
                else
                {
                    if (logRecords.Count == 0)
                        return false;
                    else
                        return true;
                }
            }
        }

        public LoggingLevel LogLevel
        {
            get { return loggingLevel; }
            set
            {
                loggingLevel = value;

                if (logOpenType == LogFileOpenType.Write)
                {
                    FlushBufferToStream();

                    writer.WriteLine();
                    writer.WriteLine(@"*** LogLevel set to [" + loggingLevel.ToString() + "] at "
                        + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + " ***");
                    writer.WriteLine();

                    writer.Flush();
                }
            }
        }

        public List<LogRecord> WriteBuffer
        {
            get
            {
                switch (logOpenType)
                {
                    case LogFileOpenType.Read:
                        return null;

                    case LogFileOpenType.Write:
                        return logRecords;

                    default:
                        return null;
                }
            }
        }

        public ReadOnlyCollection<LogRecord> ReadBuffer
        {
            get
            {
                switch (logOpenType)
                {
                    case LogFileOpenType.Read:
                        return logRecords.AsReadOnly();

                    case LogFileOpenType.Write:
                        return null;

                    default:
                        return null;
                }
            }
        }

        public WriteFrequency RecordWriteFrequency
        {
            get { return writeFrequency; }
            set
            {
                writeFrequency = value;

                // flush buffer to file if necessary
                CheckBufferForFlush();
            }
        }

        public string FileName
        {
            get { return fileName; }
        }

        public FileMode LogFileMode
        {
            get { return logFileMode; }
        }

        public LogFileOpenType LogOpenType
        {
            get { return logOpenType; }
        }

        public int LineLimitBeforeFlush
        {
            get { return lineLimitBeforeFlush; }
            set
            {
                lineLimitBeforeFlush = value;

                // flush buffer to file if necessary
                CheckBufferForFlush();
            }
        }

        public string LogFileDelimiter
        {
            get { return logFileDelimiter; }
            set { logFileDelimiter = value; }
        }

        public FileStream LogStream
        {
            get { return stream; }
        }
        #endregion

        #region public methods
        public void WriteLogRecord(LogRecord logRecord)
        {
            List<LogRecord> logRecords = new List<LogRecord>();
            logRecords.Add(logRecord);
            WriteLogRecord(logRecords);
        }

        public void WriteLogRecord(DateTime logTime, LoggingLevel logLevel, string logUser,
            string logApp, string logMsg)
        {
            LogRecord logRecord;
            logRecord.LogTime = logTime;
            logRecord.LogLevel = logLevel;
            logRecord.LogUser = logUser;
            logRecord.LogApp = logApp;
            logRecord.LogMsg = logMsg;

            WriteLogRecord(logRecord);
        }

        public void WriteLogRecord(List<LogRecord> logRecords)
        {
            if (logOpenType == LogFileOpenType.Read)
                throw new LogFileException("File open as read-only; cannot write to file.", null);

            foreach (LogRecord logRecord in logRecords)
            {
                // only log if record is at least as high in priority as this.LogLevel
                if (logRecord.LogLevel >= loggingLevel)
                    logRecords.Add(logRecord);
            }

            // flush buffer to file if necessary
            CheckBufferForFlush();
        }

        public void FlushBufferToStream()
        {
            if (logOpenType == LogFileOpenType.Read)
                throw new LogFileException("File open as read-only; cannot write to file.", null);

            // if no records in buffer, no need to continue
            if (!this.Dirty)
                return;

            try
            {
                foreach (LogRecord logRecord in logRecords)
                    writer.WriteLine(logRecord.ToString(logFileDelimiter));

                if (RecordsWrittenToLogFile != null)
                    RecordsWrittenToLogFile(this, logRecords.Count);

                logRecords.Clear();
                writer.Flush();
            }
            catch (Exception e)
            {
                throw new LogFileException(
                    "Unable to write to log; check inner exception for more details.", e);
            }
        }

        public ReadOnlyCollection<LogRecord> ReadLogFile()
        {
            if (logOpenType == LogFileOpenType.Write)
                throw new LogFileException("File open for writing; cannot read file.", null);

            // clear the previously-read items and move to beginning of stream
            logRecords.Clear();
            stream.Position = 0;

            try
            {
                string line;

                // read each line until end
                while ((line = reader.ReadLine()) != null)
                {
                    if (!line.StartsWith("***") && line.Trim() != string.Empty)
                    {
                        LogRecord logRecord = new LogRecord(line, logFileDelimiter);

                        // only read-in if record is at least as high in priority as this.LogLevel
                        if (logRecord.LogLevel >= loggingLevel)
                            logRecords.Add(logRecord);
                    }
                }
            }
            catch (LogFileException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new LogFileException(
                    "Error reading from file; check inner exception for more detials.", e);
            }

            return this.ReadBuffer;
        }
        #endregion

        #region private methods
        private void OpenFileStreamWriterReader(string file, FileMode fileMode, LogFileOpenType openType)
        {
            stream = new FileStream(file, fileMode);

            switch (openType)
            {
                case LogFileOpenType.Read:
                    reader = new StreamReader(stream);
                    break;

                case LogFileOpenType.Write:
                    writer = new StreamWriter(stream);

                    writer.WriteLine(@"*** Logging started with LogLevel of [" + loggingLevel.ToString() + "] at "
                        + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + " ***");
                    writer.WriteLine();
                    writer.Flush();

                    break;

                default:
                    // nothing to be done here
                    break;
            }
        }

        private void CheckBufferForFlush()
        {
            // just return if file open for read-only
            if (this.LogOpenType == LogFileOpenType.Read)
                return;

            // return if there are no records currently in buffer
            if (!this.Dirty)
                return;

            // flush buffer depending on this.LogWriteFrequency
            switch (writeFrequency)
            {
                // write all lines to file immediately
                case WriteFrequency.Automatic:
                    FlushBufferToStream();
                    break;

                // write lines to file if line limit is reached
                case WriteFrequency.LineLimit:
                    if (logRecords.Count >= lineLimitBeforeFlush)
                        FlushBufferToStream();
                    break;

                // for Manual do nothing; call to FlushBufferToStream() will be called
                // outside of class, or on this.Dispose()
                case WriteFrequency.Manual:
                default:
                    break;
            }
        }
        #endregion

        #region IDisposable Members
        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (writer != null)
            {
                FlushBufferToStream();

                writer.WriteLine();
                writer.WriteLine(@"*** Logging ended at " + DateTime.Now.ToLongDateString() 
                    + " " + DateTime.Now.ToLongTimeString() + " ***");
                writer.WriteLine();
                writer.Flush();

                writer.Dispose();
            }

            if (reader != null)
                reader.Dispose();

            if (stream != null)
                stream.Dispose();
        }
        #endregion
    }
}
