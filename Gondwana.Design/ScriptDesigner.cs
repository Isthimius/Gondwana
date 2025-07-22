using Gondwana.Common;
using Gondwana.Design.Controls;
using Gondwana.Design.Forms;
using Gondwana.Resource;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gondwana.Design
{
    public partial class ScriptDesigner : Form
    {
        #region ctor
        public ScriptDesigner()
        {
            InitializeComponent();
            Program.State.EngineStateFileLoaded += State_EngineStateFileLoaded;
            Program.State.EngineStateFileSaved += State_EngineStateFileUpdated;
            Program.State.EngineStateOnDirty += State_EngineStateFileUpdated;
            PopulateTreeView();
        }
        #endregion
        
        internal void treeSelect_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (BitmapChangeColorPopUp._singleton != null)
                BitmapChangeColorPopUp._singleton.Close();

            this.Text = e.Node.FullPath;

            switch (e.Node.Name)
            {
                case "ndFile":
                    LoadControl(new Controls.ProjectFile(), false);
                    return;
                case "ndAssets":
                    LoadControl(new Controls.AssetFolder(), false);
                    return;
                case "ndBitmaps":
                    LoadControl(new Controls.AssetFolder(), false);
                    return;
                case "ndAudioFiles":
                    LoadControl(new Controls.AssetFolder(), false);
                    return;
                case "ndVideoFiles":
                    LoadControl(new Controls.AssetFolder(), false);
                    return;
                case "ndCursors":
                    LoadControl(new Controls.AssetFolder(), false);
                    return;
                case "ndMiscFiles":
                    LoadControl(new Controls.AssetFolder(), false);
                    return;
                case "ndResourceFiles":
                    LoadControl(new Controls.ResourceFile(null), false);
                    return;
                case "ndTilesheets":
                    LoadControl(new Controls.TilesheetMenu(), false);
                    return;
                case "ndCycles":
                    LoadControl(new Controls.Cycle(), true);
                    return;
                case "ndGrids":
                    LoadControl(new Controls.Grid(), true);
                    return;
                case "ndGridsDisplays":
                    LoadControl(new Controls.GridsDisplay(), false);
                    return;
                case "ndSprites":
                    LoadControl(new Controls.Sprite(), true);
                    return;
                case "ndMediaFiles":
                    LoadControl(new Controls.MediaFileMenu(), false);
                    return;
                case "ndAudio":
                    LoadControl(new Controls.MediaFileMenu(), false);
                    return;
                case "ndVideo":
                    LoadControl(new Controls.MediaFileMenu(), false);
                    return;
                default:
                    break;
            }

            switch (e.Node.Parent.Name)
            {
                case "ndBitmaps":
                    LoadControl(new Controls.AssetViewer(e.Node.Name, EngineResourceFileTypes.Bitmap), false);
                    break;
                case "ndAudioFiles":
                    LoadControl(new Controls.AssetViewer(e.Node.Name, EngineResourceFileTypes.Audio), false);
                    break;
                case "ndVideoFiles":
                    LoadControl(new Controls.AssetViewer(e.Node.Name, EngineResourceFileTypes.Video), false);
                    break;
                case "ndCursors":
                    LoadControl(new Controls.AssetViewer(e.Node.Name, EngineResourceFileTypes.Cursor), false);
                    break;
                case "ndMiscFiles":
                    LoadControl(new Controls.AssetViewer(e.Node.Name, EngineResourceFileTypes.Misc), false);
                    break;
                case "ndResourceFiles":
                    break;
                case "ndTilesheets":
                    var ctl = splitUD.Panel1.Controls[0];
                    var tilesheet = Gondwana.Common.Drawing.Tilesheet.AllTilesheets.Find(x => x.Name == e.Node.Name);

                    if (ctl.GetType() != typeof(Controls.Tilesheet))
                        LoadControl(new Controls.Tilesheet(tilesheet), false);
                    else
                        ((Controls.Tilesheet)ctl).Sheet = tilesheet;
                    break;

                case "ndCycles":
                    LoadControl(new Controls.Cycle(Program.State.EngineState.Cycles[e.Node.Name]), true);
                    break;
                case "ndGrids":
                    break;
                case "ndGridsDisplays":
                    break;
                case "ndSprites":
                    break;
                case "ndAudio":
                    LoadControl(new Controls.MediaFile(Media.MediaFile.GetMediaFile(e.Node.Text)), false);
                    break;
                case "ndVideo":
                    LoadControl(new Controls.MediaFile(Media.MediaFile.GetMediaFile(e.Node.Text)), false);
                    break;
                default:
                    break;
            }
        }

        private void LoadControl(UserControl ctl, bool displayFramesBar)
        {
            splitUD.Panel1.Controls.Clear();

            if (displayFramesBar)
            {
                splitUD.Panel2Collapsed = false;
                splitUD.SplitterWidth = 2;
                splitUD.Panel2.Controls.Clear();
                var framesBar = new FramesBar();
                framesBar.Dock = DockStyle.Fill;
                splitUD.Panel2.Controls.Add(framesBar);
            }
            else
                splitUD.Panel2Collapsed = true;

            if (ctl != null)
            {
                splitUD.Panel1.Controls.Add(ctl);
                ctl.Location = new Point();
                ctl.Size = splitUD.Panel1.ClientSize;
                ctl.Margin = new System.Windows.Forms.Padding(0);
                ctl.Anchor = AnchorStyles.Bottom | AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                ctl.Visible = true;
            }
        }

        internal void PopulateTreeView(bool onlyAssets = false)
        {
            menuSave.Enabled = Program.State.IsDirty;
            menuSaveAs.Enabled = (Program.State.CurrentFile != Program.State.NoFile);

            var parentNode = treeSelect.Nodes["ndFile"];
            var assetsNode = parentNode.Nodes["ndAssets"];

            if (Program.State.CurrentFile == Program.State.NoFile)
                parentNode.Text = "File (not saved)";
            else
                parentNode.Text = string.Format("File ({0})", Path.GetFileName(Program.State.CurrentFile));

            if (parentNode.Nodes != null)
            {
                // clear Assets nodes
                if (assetsNode != null)
                {
                    foreach (TreeNode assetNode in assetsNode.Nodes)
                        assetNode.Nodes.Clear();
                }

                // populate Assets nodes
                foreach (var file in Directory.GetFiles(Program.State.AssetsDirectory))
                {
                    var fileName = Path.GetFileName(file);

                    switch (DesignerState.GetAssetType(fileName))
                    {
                        case EngineResourceFileTypes.Bitmap:
                            assetsNode.Nodes["ndBitmaps"].Nodes.Add(file, fileName, "bitmap.ico", "bitmap.ico");
                            break;
                        case EngineResourceFileTypes.Audio:
                            assetsNode.Nodes["ndAudioFiles"].Nodes.Add(file, fileName, "media.ico", "media.ico");
                            break;
                        case EngineResourceFileTypes.Video:
                            assetsNode.Nodes["ndVideoFiles"].Nodes.Add(file, fileName, "media.ico", "media.ico");
                            break;
                        case EngineResourceFileTypes.Cursor:
                            assetsNode.Nodes["ndCursors"].Nodes.Add(file, fileName, "cursor.ico", "cursor.ico");
                            break;
                        case EngineResourceFileTypes.Misc:
                            assetsNode.Nodes["ndMiscFiles"].Nodes.Add(file, fileName, "appfile.ico", "appfile.ico");
                            break;
                        default:
                            break;
                    }
                }

                if (!onlyAssets)
                {
                    // clear Engine nodes
                    parentNode.Nodes["ndResourceFiles"].Nodes.Clear();
                    parentNode.Nodes["ndTilesheets"].Nodes.Clear();
                    parentNode.Nodes["ndCycles"].Nodes.Clear();
                    parentNode.Nodes["ndGrids"].Nodes.Clear();
                    parentNode.Nodes["ndGridsDisplays"].Nodes.Clear();
                    parentNode.Nodes["ndSprites"].Nodes.Clear();
                    parentNode.Nodes["ndMediaFiles"].Nodes["ndAudio"].Nodes.Clear();
                    parentNode.Nodes["ndMediaFiles"].Nodes["ndVideo"].Nodes.Clear();

                    // populate Engine nodes
                    foreach (var resFile in Program.State.EngineState.ResourceFiles)
                        parentNode.Nodes["ndResourceFiles"].Nodes.Add(Path.GetFileName(resFile.FilePath), Path.GetFileName(resFile.FilePath), "resource2.ico", "resource2.ico");

                    foreach (var tilesheet in Program.State.EngineState.Tilesheets)
                        parentNode.Nodes["ndTilesheets"].Nodes.Add(tilesheet.Key, tilesheet.Key, "tilesheet.ico", "tilesheet.ico");

                    foreach (var cycle in Program.State.EngineState.Cycles)
                        parentNode.Nodes["ndCycles"].Nodes.Add(cycle.Key, cycle.Key, "cycle.ico", "cycle.ico");

                    foreach (var grid in Program.State.EngineState.Grids)
                        parentNode.Nodes["ndGrids"].Nodes.Add(grid.ID, grid.ID, "grid2.ico", "grid2.ico");

                    foreach (var display in Program.State.EngineState.GridsDisplay)
                        parentNode.Nodes["ndGridsDisplays"].Nodes.Add(display.ID, display.ID, "layers.ico", "layers.ico");

                    foreach (var sprite in Program.State.EngineState.Sprites)
                        parentNode.Nodes["ndSprites"].Nodes.Add(sprite.ID, sprite.ID, "sprite.ico", "sprite.ico");

                    foreach (var mediaFile in Program.State.EngineState.MediaFiles)
                    {
                        switch (mediaFile.Value.ResourceFileType)
                        {
                            case EngineResourceFileTypes.Audio:
                                parentNode.Nodes["ndMediaFiles"].Nodes["ndAudio"].Nodes.Add(mediaFile.Key, mediaFile.Key, "wav.ico", "wav.ico");
                                break;
                            case EngineResourceFileTypes.Video:
                                parentNode.Nodes["ndMediaFiles"].Nodes["ndVideo"].Nodes.Add(mediaFile.Key, mediaFile.Key, "multimedia.ico", "multimedia.ico");
                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            treeSelect.ExpandAll();
        }

        private void State_EngineStateFileLoaded(object sender, EventArgs e)
        {
            PopulateTreeView();
            this.Text = treeSelect.SelectedNode.FullPath;
            this.fswAssets.Path = Program.State.AssetsDirectory;
        }

        private void State_EngineStateFileUpdated(object sender, EventArgs e)
        {
            if (!Program.AppIsClosing)
            {
                menuSave.Enabled = Program.State.IsDirty;
                menuSaveAs.Enabled = (Program.State.CurrentFile != Program.State.NoFile);
                this.Text = treeSelect.SelectedNode.FullPath;
            }
        }

        private void fswAssets_Changed(object sender, FileSystemEventArgs e)
        {
            PopulateTreeView(true);
        }

        private void fswAssets_Renamed(object sender, RenamedEventArgs e)
        {
            PopulateTreeView(true);
        }

        private void menuSave_Click(object sender, EventArgs e)
        {
            Program.State.Save(Program.State.IsBinary);
        }

        private void menuSaveAs_Click(object sender, EventArgs e)
        {
            Program.State.Save(null);
        }

        private void menuOpen_Click(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = DesignerState.GetDialogFilter(null);

            var result = dlg.ShowDialog();
            if (result == DialogResult.OK)
            {
                bool isBinary = DesignerState.IsBinaryExtension(dlg.FileName);
                Program.State.LoadEngineState(dlg.FileName, isBinary);
            }
        }

        private void menuNew_Click(object sender, EventArgs e)
        {
            Program.State.LoadEngineState();
        }

        private void menuExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}
