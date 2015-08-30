using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Gondwana.Resource
{
    [DataContract(IsReference = true)]
    public class EngineResourceFile : IDisposable
    {
        #region static
        private static List<EngineResourceFile> _resourceFiles = new List<EngineResourceFile>();

        public static void ClearAll()
        {
            var tmp = new List<EngineResourceFile>(_resourceFiles);
            foreach (var file in tmp)
                file.Dispose();
        }

        public static List<EngineResourceFile> GetAll()
        {
            return _resourceFiles;
        }
        #endregion

        private ZipFile _zipFile;

        #region ctor
        private EngineResourceFile() { }

        public EngineResourceFile(string destFile, string password, bool encrypt)
        {
            CreateOrOpenZipFile(destFile, password, encrypt);
        }

        ~EngineResourceFile()
        {
            Dispose();
        }

        [OnDeserialized()]
        private void OnDeserialized(StreamingContext context)
        {
            CreateOrOpenZipFile(this.FilePath, this.Password, this.IsEncrypted);
        }
        #endregion

        #region properties
        [DataMember]
        public string FilePath { get; private set; }

        [DataMember]
        public string Password { get; private set; }

        [DataMember]
        public bool IsEncrypted { get; private set; }
        #endregion

        #region indexer
        public Stream this[EngineResourceFileTypes resType, string name]
        {
            get { return Get(resType, name); }
        }
        #endregion

        #region public methods
        public void Add(EngineResourceFileTypes resType, string file)
        {
            var name = Path.GetFileNameWithoutExtension(file);

            using (var memStream = new MemoryStream())
            {
                using (var fileStream = File.OpenRead(file))
                {
                    fileStream.Position = 0;
                    fileStream.CopyTo(memStream);
                    Add(resType, name, memStream);
                }
            }
        }

        public void Add(EngineResourceFileTypes resType, string name, Stream stream)
        {
            Zip(resType.ToString() + "_" + name, stream);
        }

        public Stream Get(EngineResourceFileTypes resType, string name)
        {
            return Extract(resType.ToString() + "_" + name);
        }

        public Dictionary<string, Stream> GetAll(EngineResourceFileTypes resType)
        {
            return ExtractAll(resType.ToString() + "_");
        }

        public List<string> GetAllNames(EngineResourceFileTypes resType)
        {
            return GetAllEntries(resType.ToString() + "_");
        }

        public bool Remove(EngineResourceFileTypes resType, string name)
        {
            return Remove(resType.ToString() + "_");
        }

        public void RemoveAll(EngineResourceFileTypes resType)
        {
            RemoveAll(resType.ToString() + "_");
        }
        #endregion

        #region private methods
        private void CreateOrOpenZipFile(string destFile, string password, bool encrypt)
        {
            if (!File.Exists(destFile))
                _zipFile = new ZipFile(destFile);
            else
                _zipFile = ZipFile.Read(destFile);

            if (!string.IsNullOrEmpty(password))
            {
                _zipFile.Password = password;
                if (encrypt)
                    _zipFile.Encryption = EncryptionAlgorithm.WinZipAes256;
            }

            _resourceFiles.Add(this);
        }

        private void Zip(string name, Stream stream)
        {
            if (_zipFile.ContainsEntry(name))
                _zipFile.RemoveEntry(name);

            _zipFile.AddEntry(name, stream);
        }

        private Stream Extract(string name)
        {
            Stream stream = null;

            if (string.IsNullOrEmpty(this.Password))
                _zipFile[name].Extract(stream);
            else
                _zipFile[name].ExtractWithPassword(stream, this.Password);

            return stream;
        }

        private Dictionary<string, Stream> ExtractAll(string entryBeginsWith)
        {
            var streams = new Dictionary<string, Stream>();

            foreach (var entry in _zipFile)
            {
                if (entry.FileName.Length < entryBeginsWith.Length)
                    continue;

                if (entry.FileName.Substring(0, entryBeginsWith.Length) != entryBeginsWith)
                    continue;

                Stream stream = null;

                if (string.IsNullOrEmpty(this.Password))
                    entry.Extract(stream);
                else
                    entry.ExtractWithPassword(stream, this.Password);

                streams.Add(entry.FileName.Substring(entry.FileName.Length - 1), stream);
            }

            return streams;
        }

        private List<string> GetAllEntries(string entryBeginsWith)
        {
            var entries = new List<string>();

            foreach (var entry in _zipFile)
            {
                if (entry.FileName.Length < entryBeginsWith.Length)
                    continue;

                if (entry.FileName.Substring(0, entryBeginsWith.Length) != entryBeginsWith)
                    continue;

                entries.Add(entry.FileName.Substring(entry.FileName.Length));
            }

            return entries;
        }

        private bool Remove(string name)
        {
            if (_zipFile.ContainsEntry(name))
            {
                _zipFile.RemoveEntry(name);
                return true;
            }

            name = Path.GetFileNameWithoutExtension(name);
            if (_zipFile.ContainsEntry(name))
            {
                _zipFile.RemoveEntry(name);
                return true;
            }

            return false;
        }

        private void RemoveAll(string entryBeginsWith)
        {
            _zipFile.RemoveEntries(GetAllEntries(entryBeginsWith));
        }
        #endregion

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (_zipFile != null)
                _zipFile.Dispose();

            _resourceFiles.Remove(this);
        }
    }
}
