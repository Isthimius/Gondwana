using Gondwana.Common;
using Gondwana.Common.Drawing;
using Gondwana.Media;
using Gondwana.Resource;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Gondwana.Design
{
    internal class DesignerState : IDisposable
    {
        internal event EventHandler EngineStateFileLoaded;
        internal event EventHandler EngineStateFileSaved;
        internal event EventHandler EngineStateOnDirty;

        private static readonly string AssetPath = "Assets" + Path.DirectorySeparatorChar.ToString();
        private static readonly string TempPath = "Temp" + Path.DirectorySeparatorChar.ToString();

        internal readonly string NoFile = "no current file";

        internal EngineState EngineState;
        internal string CurrentFile;
        internal bool IsBinary;

        #region ctor
        internal DesignerState(string[] args)
        {
            bool tmp;
            if (args.Count() != 2 || bool.TryParse(args[1], out tmp) == false)
                LoadEngineState();
            else
                LoadEngineState(args[0], bool.Parse(args[1]));
        }
        #endregion

        #region properties
        private string _workingDirectory;
        internal string WorkingDirectory
        {
            get { return _workingDirectory; }
            set
            {
                var oldAssets = AssetsDirectory;

                if (value.Substring(value.Length - 1) != Path.DirectorySeparatorChar.ToString())
                    value += Path.DirectorySeparatorChar.ToString();

                _workingDirectory = value;

                CreateDirs(oldAssets);
                CopyFilesToAssets();
            }
        }

        internal string AssetsDirectory
        {
            get { return WorkingDirectory + AssetPath; }
        }

        internal string TempDirectory
        {
            get { return WorkingDirectory + TempPath; }
        }

        private bool _isDirty;
        internal bool IsDirty
        {
            get { return _isDirty; }
            set
            {
                _isDirty = value;

                if (_isDirty && (EngineStateOnDirty != null))
                    EngineStateOnDirty(this, new EventArgs());
            }
        }

        public Frame SelectedFrame { get; internal set; }
        #endregion

        #region internal methods
        internal void LoadEngineState()
        {
            CheckForSave();

            Cursor.Current = Cursors.WaitCursor;

            if (this.EngineState != null)
                this.EngineState.Clear();

            this.CurrentFile = NoFile;
            this.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath);
            this.EngineState = Gondwana.Common.EngineState.GetEngineState(); 
            this.IsBinary = false;
            this.IsDirty = false;

            Cursor.Current = Cursors.Default;

            if (EngineStateFileLoaded != null)
                EngineStateFileLoaded(this, new EventArgs());
        }

        internal void LoadEngineState(string file, bool isBinary)
        {
            CheckForSave();
            
            if (this.EngineState != null)
                this.EngineState.Clear();
            
            this.CurrentFile = file;
            this.WorkingDirectory = Path.GetDirectoryName(file);
            this.EngineState = Gondwana.Common.EngineState.GetEngineState(file, isBinary);
            this.IsBinary = isBinary;
            this.IsDirty = false;

            if (EngineStateFileLoaded != null)
                EngineStateFileLoaded(this, new EventArgs());
        }

        /// <summary>
        /// Return of false signifies save was canceled.  Return of true signifies save was successful, or not needed.
        /// </summary>
        internal bool Save(bool? asBinary)
        {
            if (string.IsNullOrWhiteSpace(this.CurrentFile)
                || this.CurrentFile == NoFile
                || !Directory.Exists(Path.GetDirectoryName(this.CurrentFile))
                || asBinary == null)
            {
                var dlg = new SaveFileDialog();
                dlg.Filter = GetDialogFilter(asBinary);
                var result = dlg.ShowDialog();

                if (result == DialogResult.Cancel || result == DialogResult.None)
                    return false;

                if (result == DialogResult.OK)
                    this.CurrentFile = dlg.FileName;
            }

            this.IsBinary = IsBinaryExtension(this.CurrentFile);

            this.EngineState.Save(this.CurrentFile, this.IsBinary);
            this.WorkingDirectory = Path.GetDirectoryName(this.CurrentFile);
            IsDirty = false;

            if (EngineStateFileSaved != null)
                EngineStateFileSaved(this, new EventArgs());

            return true;
        }

        /// <summary>
        /// Return of false signifies save was canceled.  Return of true signifies save was successful, or not needed.
        /// </summary>
        internal bool CheckForSave()
        {
            if (this.EngineState == null)
                return true;

            if (!IsDirty)
                return true;

            string msg;
            MessageBoxButtons buttons;

            if (Program.AppIsClosing)
            {
                msg = "Save project before exiting?";
                buttons = MessageBoxButtons.YesNo;
            }
            else
            {
                msg = "Save project before continuing?";
                buttons = MessageBoxButtons.YesNoCancel;
            }

            var dialogResult = MessageBox.Show(msg, "Save Project", buttons);
            switch (dialogResult)
            {
                case DialogResult.Yes:
                    return Save(Program.State.IsBinary);
                case DialogResult.No:
                    return true;
                case DialogResult.Cancel:
                case DialogResult.None:
                default:
                    return false;
            }
        }
        #endregion

        #region static methods
        internal static string GetDialogFilter(bool? binaryFile)
        {
            if (binaryFile == null)
                return "Gondwana Engine State (*.gstate)|*.gstate|Gondwana Engine State Binary (*.gbinary)|*.gbinary";

            if (binaryFile == true)
                return "Gondwana Engine State Binary (*.gbinary)|*.gbinary";

            if (binaryFile == false)
                return "Gondwana Engine State (*.gstate)|*.gstate";

            return null;
        }

        internal static bool IsBinaryExtension(string filename)
        {
            return filename.EndsWith(".gbinary");
        }

        internal static DataTable GetDataTableFromValueBag(Dictionary<string, string> dictionary)
        {
            var dt = new DataTable();

            dt.Columns.Add("Key");
            dt.Columns.Add("Value");

            dt.Constraints.Add("kvpKey", dt.Columns["Key"], true);

            foreach (var kvp in dictionary)
                dt.Rows.Add(kvp.Key, kvp.Value);

            return dt;
        }

        internal static Dictionary<string, string> GetValueBagFromDataTable(DataTable dt)
        {
            var dict = new Dictionary<string, string>();

            foreach (DataRow row in dt.Rows)
                dict.Add(row["Key"].ToString(), row["Value"].ToString());

            return dict;
        }

        internal static EngineResourceFileTypes GetAssetType(string file)
        {
            string fileExt = Path.GetExtension(file).ToLower();

            if (fileExt == ".bmp" ||
                fileExt == ".gif" ||
                fileExt == ".jpg" ||
                fileExt == ".jpeg" ||
                fileExt == ".png" ||
                fileExt == ".tif" ||
                fileExt == ".tiff")
                return EngineResourceFileTypes.Bitmap;

            if (fileExt == ".cur")
                return EngineResourceFileTypes.Cursor;

            if (fileExt == ".wav" ||
                fileExt == ".mid" ||
                fileExt == ".midi" ||
                fileExt == ".wma" ||
                fileExt == ".mp3")
                return EngineResourceFileTypes.Audio;

            if (fileExt == ".avi" ||
                fileExt == ".ogg" ||
                fileExt == ".mpg" ||
                fileExt == ".mpeg" ||
                fileExt == ".wmv" ||
                fileExt == ".asx")
                return EngineResourceFileTypes.Video;

            return EngineResourceFileTypes.Misc;
        }
        #endregion

        #region private methods
        private void CreateDirs(string oldAssets)
        {
            if (!Directory.Exists(WorkingDirectory))
                Directory.CreateDirectory(WorkingDirectory);

            if (!Directory.Exists(AssetsDirectory))
                Directory.CreateDirectory(AssetsDirectory);

            if (!Directory.Exists(TempDirectory))
                Directory.CreateDirectory(TempDirectory);

            oldAssets = Path.GetFullPath(oldAssets);
            if (oldAssets != AssetsDirectory)
            {
                if (!string.IsNullOrWhiteSpace(oldAssets))
                {
                    foreach (var oldFile in Directory.GetFiles(oldAssets, "*.*", SearchOption.AllDirectories))
                        File.Copy(oldFile, oldFile.Replace(oldAssets, AssetsDirectory), true);
                }
            }

            DeleteTempFiles();
        }

        private void CopyFilesToAssets()
        {
            if (EngineState == null)
                return;

            foreach (var tilesheet in new List<Tilesheet>(EngineState.Tilesheets.Values))
            {
                if (tilesheet.ResourceIdentifier != null)
                    continue;

                if (Path.GetDirectoryName(tilesheet.ImageFilePath) != Path.GetDirectoryName(AssetsDirectory))
                {
                    string destFile = AssetsDirectory + Path.GetFileName(tilesheet.ImageFilePath);
                    File.Copy(tilesheet.ImageFilePath, destFile, true);
                    var tmpTilesheet = new Tilesheet(tilesheet, tilesheet.Name, destFile);      // this will replace original Tilesheet
                    this.IsDirty = true;
                }
            }

            foreach (var mediaFile in new List<MediaFile>(EngineState.MediaFiles.Values))
            {
                if (mediaFile.ResourceIdentifier != null)
                    continue;

                if (Path.GetDirectoryName(mediaFile.FileName) != Path.GetDirectoryName(AssetsDirectory))
                {
                    string destFile = AssetsDirectory + Path.GetFileName(mediaFile.FileName);
                    File.Copy(mediaFile.FileName, destFile, true);
                    var tmpMediaFile = new MediaFile(mediaFile, mediaFile.Alias, destFile, mediaFile.FileType);     // this will replace original MediaFile
                    this.IsDirty = true;
                }
            }

            foreach (var resourceFile in new List<Resource.EngineResourceFile>(EngineState.ResourceFiles))
            {
                if (Path.GetDirectoryName(resourceFile.FilePath) != Path.GetDirectoryName(AssetsDirectory))
                {
                    string sourceFile = resourceFile.FilePath;
                    string destFile = AssetsDirectory + Path.GetFileName(sourceFile);
                    string password = resourceFile.Password;
                    bool isEcnrypted = resourceFile.IsEncrypted;

                    resourceFile.Dispose();

                    File.Copy(sourceFile, destFile, true);
                    var tmpResourceFile = new Resource.EngineResourceFile(destFile, password, isEcnrypted);
                    this.IsDirty = true;
                }
            }
        }

        private void DeleteTempFiles()
        {
            foreach (var file in Directory.GetFiles(TempDirectory))
                File.Delete(file);
        }
        #endregion

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            CheckForSave();
            DeleteTempFiles();
        }
    }
}
