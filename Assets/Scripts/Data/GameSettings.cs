using UnityEngine;

namespace DodgeBallSim.Data
{
    [CreateAssetMenu(fileName = "GameSettings", menuName = "Dodgeball/GameSettings")]
    public class GameSettings : ScriptableObject
    {
        [Header("フィールド設定")]
        public float courtWidth = 20f;      // 横幅
        public float courtLength = 30f;     // 全長（両チームの内野を合わせた長さ）
        public float outerZoneWidth = 3f;   // 外野ゾーンの幅

        [Header("チームA設定")]
        public int teamAInnerCount = 5;
        public int teamAOuterCount = 2;

        [Header("チームB設定")]
        public int teamBInnerCount = 5;
        public int teamBOuterCount = 2;

        [Header("ボール設定")]
        public int ballCount = 1;
        [Range(0f, 1f)] public float ballBounciness = 0.8f; // ボールの跳ね返り係数
        public float ballFriction = 0.1f;                  // ボールの摩擦係数
    }
}