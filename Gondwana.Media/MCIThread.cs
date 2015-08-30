using Gondwana.Media.EventArgs;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Gondwana.Media
{
    public class MCIThread
    {
        #region Win32 p/invoke
        [DllImport("winmm.dll")]
        private static extern int mciSendString(string command, StringBuilder buffer, int bufferSize, IntPtr hwndCallback);

        [DllImport("winmm.dll")]
        private static extern Int32 mciGetErrorString(Int32 errorCode, StringBuilder errorText, Int32 errorTextSize);
        #endregion

        private Thread mciConsumerThread;

        private object _monitorLock;
        private string _syncMciCommand;
        private int _syncMciRetLen;
        private MCIResult _syncMciResult;

        public MCIThread()
        {
            _monitorLock = new object();
            mciConsumerThread = new Thread(new ThreadStart(ThreadedMCIConsumer));
            mciConsumerThread.IsBackground = true;
            mciConsumerThread.Start();
        }
        
        private void ThreadedMCIConsumer()
        {
            // as this is a background-thread, the loop exits
            // when the thread is aborted from outside.
            while (true)
            {
                lock (_monitorLock)
                {
                    // wait for release of _monitorLock object;
                    // when this statement exits, a new mciCommand has been issued
                    Monitor.Wait(_monitorLock);

                    // the object has been released, so make the mci call
                    // from this thread and put the return value into the
                    // _syncMciResult field
                    _syncMciResult = SendMCICommand(_syncMciCommand, _syncMciRetLen);

                    // tell the other thread that a result has been obtained
                    Monitor.Pulse(_monitorLock);    // this exits the Monitor.Wait(_monitorLock) command on the other thread
                }
            }
        }

        public MCIResult Call(string command, int retLen)
        {
            MCIResult nonLockedReturn;

            lock (_monitorLock)
            {
                // the _monitorLock has been freed temporarily by the SendMCICommand thread;
                // grab the _syncMciCommand and _syncMciRetLen variables and memorize the mciCommand
                _syncMciCommand = command;
                _syncMciRetLen = retLen;

                // release the _monitorLock, so the other thread can process the mciSendString
                Monitor.Pulse(_monitorLock);        // this exits the Monitor.wait(_monitorLock) command on the other thread

                // now wait until the mci-thread signals the arrival of the result
                Monitor.Wait(_monitorLock);
                
                nonLockedReturn = _syncMciResult;
            }

            return nonLockedReturn;
        }

        private MCIResult SendMCICommand(string mciCommand, int retLen)
        {
            var mciResult = new MCIResult();
            StringBuilder ret = null;

            if (retLen > 0)
                ret = new StringBuilder(retLen);

            // send the command string to the MCI
            mciResult.ReturnValue = mciSendString(mciCommand, ret, retLen, IntPtr.Zero);

            // if an error was returned, raise the event, and throw exception if appropriate
            if (mciResult.IsError())
            {
                var buffer = new StringBuilder(128);
                mciGetErrorString(mciResult.ReturnValue, buffer, 128);
                ret = buffer;
            }

            if (ret != null)
                mciResult.ReturnMessage = ret.ToString();
            else
                mciResult.ReturnMessage = string.Empty;

            return mciResult;
        }
    }
}