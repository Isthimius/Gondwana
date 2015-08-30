using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Gondwana.Common.Utility
{
    public static class ZipHelper
    {
        #region zip files
        public static void Zip(string sourceFile, string destFile)
        {
            Zip(sourceFile, destFile, null, false);
        }

        public static void Zip(List<string> sourceFiles, string destFile)
        {
            Zip(sourceFiles, destFile, null, false);
        }

        public static void Zip(string sourceFile, string destFile, string password, bool encrypt)
        {
            var sourceFiles = new List<string>();
            sourceFiles.Add(sourceFile);

            Zip(sourceFiles, destFile, password, encrypt);
        }

        public static void Zip(List<string> sourceFiles, string destFile, string password, bool encrypt)
        {
            using (var zipFile = CreateNewZipFile(destFile, password, encrypt))
            {
                foreach (var sourceFile in sourceFiles)
                {
                    if (zipFile.ContainsEntry(sourceFile))
                        zipFile.RemoveEntry(sourceFile);
                    
                    zipFile.AddFile(sourceFile);
                }

                zipFile.Save(); 
            } 
        }

        public static void Zip(string entryName, Stream fileData, string destFile, string password = null, bool encrypt = false)
        {
            using (var zipFile = CreateNewZipFile(destFile, password, encrypt))
            {
                if (zipFile.ContainsEntry(entryName))
                    zipFile.RemoveEntry(entryName);

                zipFile.AddEntry(entryName, fileData);
            }
        }
        #endregion

        #region extract files as Stream
        public static Stream ExtractFile(string zipFile, string fileName)
        {
            return ExtractFile(zipFile, fileName, null);
        }

        public static Stream ExtractFile(string zipFile, string fileName, string password)
        {
            Stream stream = null;

            using (var zip = ZipFile.Read(zipFile))
            {
                if (zip.ContainsEntry(fileName))
                {
                    if (string.IsNullOrEmpty(password))
                        zip[fileName].Extract(stream);
                    else
                        zip[fileName].ExtractWithPassword(stream, password);
                }
            }

            return stream;
        }

        public static Dictionary<string, Stream> ExtractAllFiles(string zipFile)
        {
            return ExtractAllFiles(zipFile, null);
        }

        public static Dictionary<string, Stream> ExtractAllFiles(string zipFile, string password)
        {
            var streams = new Dictionary<string, Stream>();

            using (var zip = ZipFile.Read(zipFile))
            {
                foreach (var item in zip)
                {
                    Stream stream = null;

                    if (string.IsNullOrEmpty(password))
                        item.Extract(stream);
                    else
                        item.ExtractWithPassword(stream, password);

                    streams.Add(item.FileName, stream);
                }
            }

            return streams;
        }
        #endregion

        #region get entries
        public static List<ZipEntry> GetAllEntries(string zipFile)
        {
            using (var zip = ZipFile.Read(zipFile))
            {
                return zip.Entries.ToList();
            }
        }

        public static List<string> GetAllFileNames(string zipFile)
        {
            var files = new List<string>();

            using (var zip = ZipFile.Read(zipFile))
            {
                foreach (var item in zip)
                    files.Add(item.FileName);
            }

            return files;
        }
        #endregion

        #region remove files from zip
        public static bool RemoveFile(string zipFile, string fileName)
        {
            using (var zip = ZipFile.Read(zipFile))
            {
                if (zip.ContainsEntry(fileName))
                {
                    zip.RemoveEntry(fileName);
                    return true;
                }
            }

            return false;
        }
        #endregion

        #region private methods
        private static ZipFile CreateNewZipFile(string destFile, string password, bool encrypt)
        {
            var zipFile = new ZipFile(destFile);

            if (!string.IsNullOrEmpty(password))
            {
                zipFile.Password = password;
                if (encrypt)
                    zipFile.Encryption = EncryptionAlgorithm.WinZipAes256;
            }

            return zipFile;
        }
        #endregion
    }
}
