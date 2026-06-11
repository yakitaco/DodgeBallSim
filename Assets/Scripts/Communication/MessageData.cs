using UnityEngine;
using DodgeBallSim.Data;

namespace DodgeBallSim.Communication
{
    // 発言の種類
    public enum MessageType
    {
        WatchOut,   // 「危ない！敵が狙ってるぞ！」（死角からの攻撃を回避させる）
        PassMe,     // 「パスくれ！」（外野が内野に、または内野同士での要求）
        ImThrowing  // 「今から投げるぞ！」（味方に心の準備をさせる）
    }

    // 空間を飛び交う音声データの構造
    public struct VoiceMessage
    {
        public Team senderTeam;             // 誰の味方（あるいは敵）の声か
        public GameObject sender;           // 発言者
        public MessageType type;            // 発言内容
        public Vector3 senderWorldPos;      // 発言者の位置（ネットワークが処理用に使用）
        public float volume;                // 声の大きさ（1.0 = 最大）
    }

    // 受信したAIが脳内で解釈するためのデータ構造（絶対座標は剥奪される）
    public struct ReceivedVoice
    {
        public MessageType type;
        public Vector3 relativeDirection;   // 「自分から見てどっちの方向から聞こえたか」
        public float volume;                // 聞こえた音の大きさ（距離減衰後）
        public bool isTeammate;             // 味方の声かどうか
    }
}