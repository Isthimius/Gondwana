﻿using System.Runtime.Serialization;

namespace Gondwana.Common.Collisions
{
    [DataContract]
    public struct CollisionDetectionAdjustment
    {
        [DataMember]
        public int Top;

        [DataMember]
        public int Bottom;

        [DataMember]
        public int Left;

        [DataMember]
        public int Right;
    }
}