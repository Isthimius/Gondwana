using Gondwana.Common.Drawing;
using Gondwana.Common.Drawing.Animation;
using Gondwana.Common.Drawing.Sprites;
using Gondwana.Common.Grid;
using Gondwana.Common.Utility;
using Gondwana.Media;
using Gondwana.Resource;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Xml;

namespace Gondwana.Common
{
    [DataContract(Name = "GondwanaEngine")]
    public class EngineState
    {
        private EngineState() { ValueBag = new Dictionary<string, string>(); }

        #region static methods
        /// <summary>
        /// Captures the current values for the Gondwana.Common classes referenced by <see cref="EngineState"/>
        /// </summary>
        /// <returns></returns>
        public static EngineState GetEngineState()
        {
            var allTilesheets = Tilesheet.AllTilesheets;
            allTilesheets.Sort((x, y) => string.Compare(x.MaskName, y.MaskName));

            return new EngineState()
            {
                ResourceFiles = EngineResourceFile.GetAll(),
                Tilesheets = Tilesheet._tilesheets,
                Cycles = Cycle._cycles,
                GridsDisplay = GridPointMatrixes._allGridPointMatrixes,
                Grids = GridPointMatrix._allGridPointMatrix,
                Sprites = Gondwana.Common.Drawing.Sprites.Sprites._spriteList,
                MediaFiles = MediaFile._mediaFiles
            };
        }

        /// <summary>
        /// Reads in the EngineState from a serialized file
        /// </summary>
        /// <param name="file">path of file containing EngineState serialization</param>
        /// <param name="isBinary">whether or not the serialization file is binary encoded</param>
        /// <returns></returns>
        public static EngineState GetEngineState(string file, bool isBinary)
        {
            var serializer = new DataContractSerializer(typeof(EngineState));
            Dictionary<string, string> valueBag = null;

            // deserializing the file will load the instantiate the classes
            if (isBinary)
            {
                using (var filestream = new FileStream(file, FileMode.Open))
                using (var zipStream = new GZipStream(filestream, CompressionMode.Decompress))
                using (var memStream = new MemoryStream())
                {
                    zipStream.CopyTo(memStream);
                    valueBag = BinarySerializer.Deserialize<EngineState>(memStream.ToArray()).ValueBag;
                }
            }
            else
            {
                using (var filestream = new FileStream(file, FileMode.Open))
                    valueBag = ((EngineState)serializer.ReadObject(filestream)).ValueBag;
            }

            // calling GetEngineState() will set "this" properties to the
            // List / Dictionary reference inclusive of all instantiated classes
            var state = GetEngineState();
            state.ValueBag = valueBag;
            return state;
        }
        #endregion

        #region properties
        [DataMember(Order = 0)]
        public Dictionary<string, string> ValueBag { get; set; }

        [DataMember(Order = 1)]
        public List<EngineResourceFile> ResourceFiles { get; set; }

        [DataMember(Order = 2)]
        public Dictionary<string, Tilesheet> Tilesheets { get; set; }

        [DataMember(Order = 3)]
        public Dictionary<string, Cycle> Cycles { get; set; }

        [DataMember(Order = 4)]
        public List<GridPointMatrix> Grids { get; set; }

        [DataMember(Order = 5)]
        public List<GridPointMatrixes> GridsDisplay { get; set; }

        [DataMember(Order = 6)]
        public List<Sprite> Sprites { get; set; }

        [DataMember(Order = 7)]
        public Dictionary<string, MediaFile> MediaFiles { get; set; }
        #endregion

        #region public methods
        /// <summary>
        /// Clear all EngineState collection properties associated with the Engine class
        /// </summary>
        public void Clear()
        {
            ValueBag.Clear();
            EngineResourceFile.ClearAll();
            Tilesheet.ClearAllTilesheets();
            Cycle.ClearAllAnimationCycles();
            GridPointMatrixes.ClearAllGridPointMatrixes();
            GridPointMatrix.ClearAllGridPointMatrix();
            Gondwana.Common.Drawing.Sprites.Sprites.Clear();
            MediaFile.DisposeAll();
        }

        public void Save(string file, bool isBinary)
        {
            var serializer = new DataContractSerializer(this.GetType());

            if (isBinary)
            {
                byte[] streamBytes = BinarySerializer.Serialize<EngineState>(this);

                using (var filestream = new FileStream(file, FileMode.Create))
                using (var zipStream = new GZipStream(filestream, CompressionMode.Compress))
                    zipStream.Write(streamBytes, 0, streamBytes.Length);
            }
            else
            {
                using (var writer = new FileStream(file, FileMode.Create))
                    serializer.WriteObject(writer, this);
            }
        }
        #endregion
    }
}
