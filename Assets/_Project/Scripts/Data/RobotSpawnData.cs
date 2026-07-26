using System;
using UnityEngine;

namespace FarmFuryArcade.Data
{
    [Serializable]
    public class RobotSpawnData
    {
        public RobotType robotType;
        public float spawnDelay;
        public Vector2Int spawnPosition;
    }
}
