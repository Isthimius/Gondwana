using Gondwana.Media.EventArgs;

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Runtime.Serialization;
using Gondwana.Resource;

namespace Gondwana.Media
{
    [DataContract(IsReference = true)]
    public class MediaFile : IDisposable
    {
        #region Win32 p/invoke
        [DllImport("winmm.dll")]
        private static extern int mciSendString(string command, StringBuilder buffer, int bufferSize, IntPtr hwndCallback);

        [DllImport("winmm.dll")]
        private static extern Int32 mciGetErrorString(Int32 errorCode, StringBuilder errorText, Int32 errorTextSize);
        #endregion

        #region static
        private const string tmpDir = "tmpMediaFile";

        public static Dictionary<string, MediaFile> _mediaFiles = new Dictionary<string, MediaFile>();
        public static bool MCIErrorsThrowExceptions { get; set; }

        static MediaFile()
        {
            // delete previous tmp media files
            foreach (var file in Directory.GetFiles(GetTmpDir()))
                File.Delete(file);

            // start thread for calling MCI
            _mciProcessorThread = new Thread(ScanQueue);
            _mciProcessorThread.IsBackground = true;
            _mciProcessorThread.Start();
        }

        public static MediaFile GetMediaFile(string alias)
        {
            if (_mediaFiles.ContainsKey(alias))
                return _mediaFiles[alias];
            else
                return null;
        }

        public static MediaFileType InferMediaFileType(string file)
        {
            var extension = Path.GetExtension(file).Substring(1);

            MediaFileType mediaType;
            if (Enum.TryParse<MediaFileType>(extension, out mediaType) == false)
                return MediaFileType.unknown;
            else
                return mediaType;
        }

        public static void DisposeAll()
        {
            var allAlias = new List<string>();

            foreach (var key in _mediaFiles.Keys)
                allAlias.Add(key);

            foreach (var alias in allAlias)
                _mediaFiles.Remove(alias);
        }
        #endregion

        #region public events
        public delegate void OpenFileEventHandler(OpenFileEventArgs oea);
        public static event OpenFileEventHandler OpenFile;

        public delegate void PlayFileEventHandler(PlayFileEventArgs pea);
        public static event PlayFileEventHandler PlayFile;

        public delegate void PauseFileEventHandler(PauseFileEventArgs paea);
        public static event PauseFileEventHandler PauseFile;

        public delegate void StopFileEventHandler(StopFileEventArgs sea);
        public static event StopFileEventHandler StopFile;

        public delegate void CloseFileEventHandler(CloseFileEventArgs cea);
        public static event CloseFileEventHandler CloseFile;

        public delegate void MCIErrorEventHandler(MCIErrorEventArgs eea);
        public static event MCIErrorEventHandler Error;
        #endregion

        private string _tmpFileName = null;

        #region constructor
        private MediaFile() { }

        public MediaFile(string alias, string fileName) : this(alias, fileName, MediaFile.InferMediaFileType(fileName)) { }

        public MediaFile(string alias, string fileName, MediaFileType fileType)
        {
            // if opening a media file directly, make a copy of it first, to allow multiple instances
            if (!IsTempFile)
                _tmpFileName = SaveFileToTmpDir(fileName);

            // if the alias already exists, remove the old MediaFile and replace it with this
            if (_mediaFiles.ContainsKey(alias))
                _mediaFiles[alias].Dispose();

            // add this new MediaFile to static list
            _mediaFiles.Add(alias, this);

            Alias = alias;
            FileName = fileName;
            FileType = fileType;

            IsPlaying = false;
            IsPaused = false;

            Open();
        }

        public MediaFile(string alias, Stream fileData, MediaFileType fileType)
            : this(alias, SaveStreamToTmpDir(fileData), fileType)
        {
            IsTempFile = true;
            InputStream = fileData;
        }

        public MediaFile(string alias, EngineResourceFileIdentifier resId, MediaFileType fileType)
            : this(alias, SaveStreamToTmpDir(resId.Data), fileType)
        {
            IsTempFile = true;
            InputStream = resId.Data;
        }

        public MediaFile(MediaFile mediaFile, string alias, string fileName, MediaFileType fileType)
        {
            this.isMutedAll = mediaFile.isMutedAll;
            this.isMutedLeft = mediaFile.isMutedLeft;
            this.isMutedRight = mediaFile.isMutedRight;
            this.allVolume = mediaFile.allVolume;
            this.lftVolume = mediaFile.lftVolume;
            this.rtVolume = mediaFile.rtVolume;
            this.trebVolume = mediaFile.trebVolume;
            this.bassVolume = mediaFile.bassVolume;
            this.balance = mediaFile.balance;
            this.isLooping = mediaFile.isLooping;

            // if opening a media file directly, make a copy of it first, to allow multiple instances
            if (!IsTempFile)
                _tmpFileName = SaveFileToTmpDir(fileName);

            // if the alias already exists, remove the old MediaFile and replace it with this
            if (_mediaFiles.ContainsKey(alias))
                _mediaFiles[alias].Dispose();

            // add this new MediaFile to static list
            _mediaFiles.Add(alias, this);

            Alias = alias;
            FileName = fileName;
            FileType = fileType;

            IsPlaying = false;
            IsPaused = false;

            Open();

            // set variables again after Open() to issue MCI commands
            MuteAll = isMutedAll;
            MuteLeft = isMutedLeft;
            MuteRight = isMutedRight;
            VolumeAll = allVolume;
            VolumeLeft = lftVolume;
            VolumeRight = rtVolume;
            VolumeTreble = trebVolume;
            VolumeBass = bassVolume;
            Balance = balance;
            Looping = isLooping;
        }

        ~MediaFile()
        {
            Dispose();
        }

        [OnSerializing]
        private void OnSerializing(StreamingContext context)
        {
            // if IsTempFile but no ResourceFile, create resource file so it can be serialized
            if (IsTempFile && ResourceIdentifier == null)
            {
                var resFile = new EngineResourceFile(string.Format("mediafile_{0}.zip", FileName), null, false);
                resFile.Add(ResourceFileType, Alias, InputStream);
                ResourceIdentifier = new EngineResourceFileIdentifier(resFile, ResourceFileType, Alias);
            }
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            // if the alias already exists, remove the old MediaFile and replace it with this
            if (_mediaFiles.ContainsKey(Alias))
                _mediaFiles[Alias].Dispose();

            // add this new MediaFile to static list
            _mediaFiles.Add(Alias, this);

            IsPlaying = false;
            IsPaused = false;

            if (ResourceIdentifier != null)
            {
                IsTempFile = true;
                InputStream = ResourceIdentifier.Data;
            }
            else
                _tmpFileName = SaveFileToTmpDir(FileName);

            Open();

            // set variables again after Open() to issue MCI commands
            MuteAll = isMutedAll;
            MuteLeft = isMutedLeft;
            MuteRight = isMutedRight;
            VolumeAll = allVolume;
            VolumeLeft = lftVolume;
            VolumeRight = rtVolume;
            VolumeTreble = trebVolume;
            VolumeBass = bassVolume;
            Balance = balance;
            Looping = isLooping;
        }
        #endregion

        #region public properties
        [DataMember]
        public string Alias { get; private set; }

        [DataMember]
        public string FileName { get; private set; }

        [IgnoreDataMember]
        public Stream InputStream { get; private set; }

        [DataMember]
        public MediaFileType FileType { get; private set; }

        private EngineResourceFileTypes _resourceFileType;

        [IgnoreDataMember]
        public EngineResourceFileTypes ResourceFileType
        {
            get { return _resourceFileType; }
            private set
            {
                _resourceFileType = value;
                RunInBackground = (_resourceFileType == EngineResourceFileTypes.Audio);
            }
        }

        [IgnoreDataMember]
        public bool RunInBackground
        {
            get;
            private set;
        }

        [DataMember]
        public EngineResourceFileIdentifier ResourceIdentifier { get; private set; }

        [IgnoreDataMember]
        public bool IsTempFile { get; private set; }

        [IgnoreDataMember]
        public bool IsPlaying { get; private set; }

        [IgnoreDataMember]
        public bool IsPaused { get; private set; }

        [DataMember]
        private bool isMutedAll = false;

        [IgnoreDataMember]
        public bool MuteAll
        {
            get { return isMutedAll; }
            set
            {
                isMutedAll = value;
                if (isMutedAll)
                    SendMCICommand(string.Format("setaudio {0} off", Alias), 0);
                else
                    SendMCICommand(string.Format("setaudio {0} on", Alias), 0);
            }
        }

        [DataMember]
        private bool isMutedLeft = false;

        [IgnoreDataMember]
        public bool MuteLeft
        {
            get { return isMutedLeft; }
            set
            {
                isMutedLeft = value;
                if (isMutedLeft)
                    SendMCICommand(string.Format("setaudio {0} left off", Alias), 0);
                else
                    SendMCICommand(string.Format("setaudio {0} left on", Alias), 0);
            }
        }

        [DataMember]
        private bool isMutedRight = false;

        [IgnoreDataMember]
        public bool MuteRight
        {
            get { return isMutedRight; }
            set
            {
                isMutedRight = value;
                if (isMutedRight)
                    SendMCICommand(string.Format("setaudio {0} right off", Alias), 0);
                else
                    SendMCICommand(string.Format("setaudio {0} right on", Alias), 0);
            }
        }

        [DataMember]
        private int allVolume = 1000;

        [IgnoreDataMember]
        public int VolumeAll
        {
            get { return allVolume; }
            set
            {
                if (value >= 0 && value <= 1000)
                {
                    allVolume = value;
                    SendMCICommand(String.Format("setaudio {0} volume to {1}", Alias, allVolume), 0);
                }
            }
        }

        [DataMember]
        private int lftVolume = 1000;

        [IgnoreDataMember]
        public int VolumeLeft
        {
            get { return lftVolume; }
            set
            {
                if (value >= 0 && value <= 1000)
                {
                    lftVolume = value;
                    SetLRVolume();
                }
            }
        }

        [DataMember]
        private int rtVolume = 1000;

        [IgnoreDataMember]
        public int VolumeRight
        {
            get { return rtVolume; }
            set
            {
                if (value >= 0 && value <= 1000)
                {
                    rtVolume = value;
                    SetLRVolume();
                }
            }
        }

        [DataMember]
        private int trebVolume = 1000;

        [IgnoreDataMember]
        public int VolumeTreble
        {
            get { return trebVolume; }
            set
            {
                if (value >= 0 && value <= 1000)
                {
                    trebVolume = value;
                    SendMCICommand(String.Format("setaudio {0} treble to {1}", Alias, trebVolume), 0);
                }
            }
        }

        [DataMember]
        private int bassVolume = 1000;

        [IgnoreDataMember]
        public int VolumeBass
        {
            get { return bassVolume; }
            set
            {
                if (value >= 0 && value <= 1000)
                {
                    bassVolume = value;
                    SendMCICommand(String.Format("setaudio {0} bass to {1}", Alias, bassVolume), 0);
                }
            }
        }

        [DataMember]
        private int balance = 0;

        [IgnoreDataMember]
        public int Balance
        {
            get { return balance; }
            set
            {
                if (value >= -1000 && value <= 1000)
                {
                    balance = value;
                    SetLRVolume();
                }
            }
        }

        [DataMember]
        private bool isLooping = false;

        [IgnoreDataMember]
        public bool Looping
        {
            get { return isLooping; }
            set
            {
                isLooping = value;

                if (IsPlaying)
                {
                    Pause();
                    Play();
                }
            }
        }

        [IgnoreDataMember]
        private bool fullScreen = false;

        [IgnoreDataMember]
        public bool FullScreen
        {
            get { return fullScreen; }
            set
            {
                fullScreen = value;
                Pause();
                Play();
            }
        }

        [IgnoreDataMember]
        public uint Duration
        {
            get
            {
                return Convert.ToUInt32(SendMCICommand(string.Format("status {0} length", Alias), 128));
            }
        }

        [IgnoreDataMember]
        public uint CurrentPosition
        {
            get
            {
                if (IsPlaying)
                    return Convert.ToUInt32(SendMCICommand(string.Format("status {0} position", Alias), 128));
                else
                    return 0;
            }
            set
            {
                Seek(value);
            }
        }

        [IgnoreDataMember]
        public string Status
        {
            get
            {
                return SendMCICommand(string.Format("status {0} mode", Alias), 128);
            }
        }
        #endregion

        #region public methods
        public void Seek(uint time)
        {
            if (time <= Duration)
            {
                if (IsPlaying)
                {
                    if (IsPaused)
                        SendMCICommand(String.Format("seek {0} to {1}", Alias, time), 0);
                    else
                    {
                        SendMCICommand(String.Format("seek {0} to {1}", Alias, time), 0);

                        StringBuilder mciCommand = new StringBuilder(string.Format("play {0} notify", Alias));

                        if (fullScreen)
                            mciCommand.Append(" FullScreen");

                        if (isLooping)
                            mciCommand.Append(" REPEAT");

                        SendMCICommand(mciCommand.ToString(), 0);
                    }
                }
            }
        }

        public void Play()
        {
            string mciCommand = "play " + Alias;

            if (fullScreen)
                mciCommand += " FullScreen";

            if (isLooping)
                mciCommand += " REPEAT";

            SendMCICommand(string.Format("seek {0} to start", Alias), 0);
            SendMCICommand(mciCommand, 0);

            if (PlayFile != null)
                PlayFile(new PlayFileEventArgs(this, mciCommand));

            IsPlaying = true;
            IsPaused = false;
        }

        public void Play(Control control)
        {
            Play(control, new Rectangle(0, 0, control.Width, control.Height));
        }

        public void Play(Control control, Rectangle rectangle)
        {
            Close();
            Open(control, rectangle);
            Play();
        }

        public void Pause()
        {
            if (IsPlaying)
            {
                if (!IsPaused)
                {
                    string mciCommand = "pause " + Alias;
                    SendMCICommand(mciCommand, 0);
                    IsPaused = true;

                    if (PauseFile != null)
                        PauseFile(new PauseFileEventArgs(this, mciCommand));
                }
                else
                    Play();
            }
        }

        public void Stop()
        {
            if (IsPlaying)
            {
                SendMCICommand("stop " + Alias, 0);

                IsPlaying = false;
                IsPaused = false;

                if (StopFile != null)
                    StopFile(new StopFileEventArgs(this));
            }
        }
        #endregion

        #region private methods
        private void Open(Control control = null, Rectangle rectangle = new Rectangle())
        {
            EngineResourceFileTypes resFileType;
            string mciOpenCmd;

            if (control == null)
                mciOpenCmd = string.Format("open \"{0}\" type {1} alias {2}",
                    _tmpFileName ?? FileName, OpenFileType(FileType, out resFileType), Alias);
            else
                mciOpenCmd = string.Format("open \"{0}\" type {1} alias {2} style child parent {3}",
                    _tmpFileName ?? FileName, OpenFileType(FileType, out resFileType), Alias, control.Handle.ToString());

            ResourceFileType = resFileType;

            if (control != null)
            {
                SendMCICommand(mciOpenCmd, 0);
                SendMCICommand(string.Format("set {0} time format milliseconds", Alias), 0);
                //SendMCICommand(string.Format("set {0} seek exactly on", Alias), 0, false);

                if (rectangle == Rectangle.Empty)
                    rectangle = new Rectangle(new Point(0, 0), control.Size);

                SendMCICommand(string.Format("put {0} window at {1} {2} {3} {4}",
                    Alias,
                    rectangle.X.ToString(),
                    rectangle.Y.ToString(),
                    rectangle.Width.ToString(),
                    rectangle.Height.ToString()), 0);
            }
            else
            {
                SendMCICommand(mciOpenCmd, 0);
                SendMCICommand(string.Format("set {0} time format milliseconds", Alias), 0);
                //SendMCICommand(string.Format("set {0} seek exactly on", Alias), 0, false);
            }

            if (OpenFile != null)
                OpenFile(new OpenFileEventArgs(this, _tmpFileName ?? FileName));
        }

        private void Close()
        {
            string mciCommand = "close " + Alias;
            SendMCICommand(mciCommand, 0);

            if (CloseFile != null)
                CloseFile(new CloseFileEventArgs(this, mciCommand));
        }

        private static string GetTmpDir()
        {
            var path = string.Format("{0}\\{1}", Path.GetDirectoryName(Application.ExecutablePath), tmpDir);

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }

        private static string SaveFileToTmpDir(string sourceFile)
        {
            var filePath = string.Format("{0}\\{1}", GetTmpDir(), Guid.NewGuid());
            File.Copy(sourceFile, filePath);
            return filePath;
        }

        private static string SaveStreamToTmpDir(Stream stream)
        {
            var filePath = string.Format("{0}\\{1}", GetTmpDir(), Guid.NewGuid());

            using (var fileStream = File.Create(filePath))
            {
                stream.Seek(0, SeekOrigin.Begin);
                stream.CopyTo(fileStream);
            }

            return filePath;
        }

        private string OpenFileType(MediaFileType fileType, out EngineResourceFileTypes resFileType)
        {
            switch (fileType)
            {
                case MediaFileType.wav:
                    resFileType = EngineResourceFileTypes.Audio;
                    return "waveaudio";

                case MediaFileType.mid:
                case MediaFileType.midi:
                    resFileType = EngineResourceFileTypes.Audio;
                    return "sequencer";

                case MediaFileType.wma:         // audio
                case MediaFileType.mp3:         // audio
                    resFileType = EngineResourceFileTypes.Audio;
                    return "mpegvideo";

                case MediaFileType.avi:         // video
                case MediaFileType.ogg:         // video
                case MediaFileType.mpg:         // video
                case MediaFileType.mpeg:        // video
                case MediaFileType.wmv:         // video
                case MediaFileType.asx:         // video
                case MediaFileType.unknown:     // assume video
                default:
                    resFileType = EngineResourceFileTypes.Video;
                    return "mpegvideo";
            }
        }

        private void SetLRVolume()
        {
            int left = lftVolume;
            int right = rtVolume;

            float leftBalMod = 1;
            float rightBalMod = 1;

            if (balance < 0)
                rightBalMod = ((float)(1000 + balance) / (float)1000);

            if (balance > 0)
                leftBalMod = ((float)(1000 - balance) / (float)1000);

            left = (int)((float)lftVolume * leftBalMod);
            right = (int)((float)rtVolume * rightBalMod);

            SendMCICommand(String.Format("setaudio {0} left volume to {1}", Alias, left), 0);
            SendMCICommand(String.Format("setaudio {0} right volume to {1}", Alias, right), 0);
        }
        #endregion

        #region threading
        private static List<MCIRequest> _requests = new List<MCIRequest>();
        private static object _lockObj = new object();
        private static Thread _mciProcessorThread;

        private string SendMCICommand(string mciCommand, int retLen)
        {
            // TODO: how to handle return values?

            if (!this.RunInBackground)
            {
                return ProcessMCIRequest(new MCIRequest(mciCommand, retLen)).ReturnMessage;
            }
            else
            {
                lock (_lockObj)
                {
                    _requests.Add(new MCIRequest(mciCommand, retLen));
                }

                return string.Empty;
            }
        }

        private static void ScanQueue()
        {
            while (true)
            {
                lock (_lockObj)
                {
                    if (_requests.Count > 0)
                    {
                        var request = _requests[0];
                        ProcessMCIRequest(request);
                        _requests.Remove(request);
                    }
                }
            }
        }

        private static MCIResult ProcessMCIRequest(MCIRequest request, IntPtr hwndHandle = new IntPtr())
        {
            var mciResult = new MCIResult();
            StringBuilder ret = null;

            if (request.retLen > 0)
                ret = new StringBuilder(request.retLen);

            // send the command string to the MCI
            mciResult.ReturnValue = mciSendString(request.MCICommand, ret, request.retLen, hwndHandle);

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

            if (mciResult.IsError())
            {
                if (Error != null)
                    Error(new MCIErrorEventArgs(request.MCICommand, mciResult.ReturnValue, mciResult.ReturnMessage));

                if (MCIErrorsThrowExceptions)
                    throw new MediaPlayerException(request.MCICommand, mciResult.ReturnValue, mciResult.ReturnMessage);
            }

#if DEBUG
            Console.WriteLine(string.Format("Sent at: {4}   Tick Count: {0}   Command: {1}   Result: {2} '{3}'",
                Environment.TickCount,
                request.MCICommand,
                mciResult.ReturnValue.ToString(),
                mciResult.ReturnMessage,
                request.Tick));
#endif

            return mciResult;
        }
        #endregion

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            Close();

            IsPlaying = false;
            IsPaused = false;

            if (_mediaFiles.ContainsKey(Alias))
                _mediaFiles.Remove(Alias);

            // TODO: why can't delete?
            //File.Delete(_tmpFileName ?? FileName);
        }
    }
}
