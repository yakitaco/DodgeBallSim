using UnityEngine;
using DodgeBallSim.Data;
using DodgeBallSim.Core;

namespace DodgeBallSim.Entities
{
    public class Ball : MonoBehaviour
    {
        public BallState CurrentState { get; private set; } = BallState.Idle;
        public GameObject LastThrower { get; private set; }
        public int BounceCount { get; private set; } = 0;

        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        public void Thrown(GameObject thrower, Vector3 force)
        {
            LastThrower = thrower;
            CurrentState = BallState.Thrown;
            BounceCount = 0;

            rb.isKinematic = false;
            rb.AddForce(force, ForceMode.Impulse);
        }

        public void Hold()
        {
            CurrentState = BallState.Held;
            rb.isKinematic = true;
        }

        // 物理衝突時のイベントをアップデート
        private void OnCollisionEnter(Collision collision)
        {
            // 審判システムが存在しない場合は処理をスキップ
            if (RefereeSystem.Instance == null) return;

            if (collision.gameObject.CompareTag("Ground"))
            {
                BounceCount++;
                CurrentState = BallState.Bounded;
                // 審判に地面へのバウンドを報告
                RefereeSystem.Instance.ReportGroundHit(this);
            }
            else
            {
                // キャラクターに当たったかどうかの確認
                CharacterBody hitCharacter = collision.gameObject.GetComponentInParent<CharacterBody>();
                if (hitCharacter != null)
                {
                    // 当たった部位が「頭」かどうかを名前で判定
                    bool isHead = collision.collider.gameObject.name == "Head";
                    // 審判にキャラクターへのヒットを報告
                    RefereeSystem.Instance.ReportHit(this, hitCharacter, isHead);
                }
            }
        }
    }
}