using UnityEngine;

namespace FarmFuryArcade.Data
{
    [CreateAssetMenu(fileName = "RobotData_XX", menuName = "Farm Fury Arcade/Robot Data")]
    public class RobotData : ScriptableObject
    {
        public RobotType robotType;
        public string displayName;
        public Sprite portraitSprite;
        public Sprite[] walkAnimationFrames;

        public float movementSpeed;
        public AIBehaviourType behaviour;

        [Tooltip("1 for standard robots, 2 for Heavy (tanks one power pellet strike).")]
        public int healthPoints = 1;
    }
}
