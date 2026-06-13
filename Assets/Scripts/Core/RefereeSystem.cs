using System.Collections.Generic;
using UnityEngine;
using DodgeBallSim.Data;
using DodgeBallSim.Entities;

namespace DodgeBallSim.Core
{
    public class RefereeSystem : MonoBehaviour
    {
        // どこからでもアクセスできるシングルトン（神の視点）
        public static RefereeSystem Instance { get; private set; }

        // 各ボールの「アウト候補者リスト（地面に落ちたらアウトになる人）」
        private Dictionary<Ball, List<CharacterBody>> pendingOuts = new Dictionary<Ball, List<CharacterBody>>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        // ボールがキャラクターに当たった時の報告を受ける
        public void ReportHit(Ball ball, CharacterBody victim, bool isHead)
        {
            // ボールが攻撃力を持たない状態なら無視
            if (ball.CurrentState != BallState.Thrown) return;

            // 味方のボールに当たった場合は無視
            if (ball.LastThrower != null)
            {
                CharacterBody throwerBody = ball.LastThrower.GetComponent<CharacterBody>();
                if (throwerBody != null && throwerBody.MyTeam == victim.MyTeam) return;
            }

            // 顔面セーフの判定
            if (isHead)
            {
                Debug.Log($"【審判】{victim.name} への顔面ヒット！セーフ！");
                return;
            }

            // アウト候補リストに追加（ダブルアウト対応）
            if (!pendingOuts.ContainsKey(ball)) pendingOuts[ball] = new List<CharacterBody>();
            if (!pendingOuts[ball].Contains(victim))
            {
                pendingOuts[ball].Add(victim);
                Debug.Log($"【審判】{victim.name} にヒット！落下またはキャッチ待ち...");
            }
        }

        // ボールが地面にバウンドした時の報告を受ける
        public void ReportGroundHit(Ball ball)
        {
            // アウト候補者がいれば、全員アウトを確定させる
            if (pendingOuts.ContainsKey(ball) && pendingOuts[ball].Count > 0)
            {
                foreach (CharacterBody victim in pendingOuts[ball])
                {
                    ExecuteOut(victim);
                }
                pendingOuts[ball].Clear();
            }
        }

        // 誰かがボールをキャッチした時の報告を受ける
        public void ReportCatch(Ball ball, CharacterBody catcher)
        {
            // アウト候補者がいた場合、味方によるキャッチで救済される
            if (pendingOuts.ContainsKey(ball) && pendingOuts[ball].Count > 0)
            {
                Debug.Log($"【審判】{catcher.name} のナイスキャッチ！味方はセーフ！");
                pendingOuts[ball].Clear();
            }

            // 外野がボールを捕った（または当てて内野に戻る等の処理）の判定もここで行えますが
            // 今回はテスト用としてシンプルに保ちます。
        }

        // アウト確定と外野への移動処理
        private void ExecuteOut(CharacterBody victim)
        {
            if (!victim.IsAlive) return;
            victim.Eliminate();
            victim.ChangePosition(PositionState.Outer);
            Debug.Log($"【審判】アウト！！ {victim.name} は外野へ移動します。");
        }
    }
}