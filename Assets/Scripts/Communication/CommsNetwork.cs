using System.Collections.Generic;
using UnityEngine;
using DodgeBallSim.AI;
using DodgeBallSim.Entities;

namespace DodgeBallSim.Communication
{
    public class CommsNetwork : MonoBehaviour
    {
        // 簡易的なシングルトンとして配置
        public static CommsNetwork Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        // キャラクターが「声」を発した時に呼び出すグローバル関数
        public void BroadcastVoice(VoiceMessage msg, float maxRange)
        {
            // シーン内のすべてのAI（DecisionMaker）を検索（簡易実装）
            DecisionMaker[] allAI = FindObjectsByType<DecisionMaker>(FindObjectsSortMode.None);

            foreach (var ai in allAI)
            {
                if (ai.gameObject == msg.sender) continue; // 自分自身には聞こえない

                // 物理的な距離を計算
                float distance = Vector3.Distance(msg.senderWorldPos, ai.transform.position);

                if (distance <= maxRange)
                {
                    // 距離による音量の減衰（線形減衰）
                    float attenuatedVolume = msg.volume * (1f - (distance / maxRange));

                    // 受信側のAIへ「音」としてローカルに変換して届ける
                    ai.ReceiveVoice(msg, attenuatedVolume);
                }
            }
        }
    }
}