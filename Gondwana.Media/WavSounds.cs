using Gondwana.Media.Enums;
using System.Collections.Generic;
using System.IO;
using System.Media;

namespace Gondwana.Media
{
    public static class WavSounds
    {
        #region private fields
        private static SoundPlayer player;
        private static Dictionary<string, Stream> streams;
        #endregion

        #region static constructor
        static WavSounds()
        {
            player = new SoundPlayer();
            streams = new Dictionary<string, Stream>();
        }
        #endregion

        #region public properties
        public static Dictionary<string, Stream>.KeyCollection LoadedStreams
        {
            get { return streams.Keys; }
        }

        public static Stream CurrentStream
        {
            get { return player.Stream; }
            set { player.Stream = value; }
        }
        #endregion

        #region public methods
        public static Stream GetLoadedStream(string streamName)
        {
            return streams[streamName];
        }

        public static void SetCurrentStream(string streamName)
        {
            player.Stream = streams[streamName];
        }

        public static void SetCurrentStream(Stream stream)
        {
            player.Stream = stream;
        }

        public static void Play()
        {
            if (player.Stream != null)
            {
                player.Stream.Position = 0;
                player.Play();
            }
        }

        public static void Play(string streamName)
        {
            Play(streams[streamName]);
        }

        public static void Play(Stream stream)
        {
            stream.Position = 0;
            player.Stream = null;
            player.Stream = stream;
            player.Play();
        }

        public static void Play(WavPlayType playType)
        {
            if (player.Stream != null)
            {
                player.Stream.Position = 0;

                switch (playType)
                {
                    case WavPlayType.Synch:
                        player.PlaySync();
                        break;
                    case WavPlayType.ASynch:
                        player.Play();
                        break;
                    case WavPlayType.Loop:
                        player.PlayLooping();
                        break;
                    default:
                        break;
                }
            }
        }

        public static void Play(string streamName, WavPlayType playType)
        {
            Stream stream = streams[streamName];
            stream.Position = 0;
            player.Stream = null;
            player.Stream = stream;

            switch (playType)
            {
                case WavPlayType.Synch:
                    player.PlaySync();
                    break;
                case WavPlayType.ASynch:
                    player.Play();
                    break;
                case WavPlayType.Loop:
                    player.PlayLooping();
                    break;
                default:
                    break;
            }
        }

        public static void Stop()
        {
            player.Stop();
        }

        public static void ClearStreams()
        {
            foreach (Stream stream in streams.Values)
                stream.Close();

            streams.Clear();
        }

        public static Stream LoadStream(string streamName, string path)
        {
            FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            return LoadStream(streamName, stream);
        }

        public static Stream LoadStream(string streamName, Stream stream)
        {
            if (streams.ContainsKey(streamName))
            {
                streams[streamName].Close();
                streams[streamName] = stream;
            }
            else
                streams.Add(streamName, stream);

            return streams[streamName];
        }

        public static void DisposePlayer()
        {
            foreach (var stream in streams)
                stream.Value.Dispose();

            if (player != null)
                player.Dispose();
        }
        #endregion
    }
}

