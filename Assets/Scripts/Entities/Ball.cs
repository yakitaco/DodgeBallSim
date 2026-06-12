using UnityEngine;
using DodgeBallSim.Data;
using DodgeBallSim.Core;

namespace DodgeBallSim.Entities
{
    public class Ball : MonoBehaviour
    {
        public BallState CurrentState { get; private set; } = BallState.Free;
        public GameObject LastThrower { get; private set; }
        public int BounceCount { get; private set; } = 0;

        private Rigidbody rb;
        private Collider col;

        [Header("保持設定")]
        private Vector3 holdLocalPos = new Vector3(0f, 0.2f, 0.7f); // 胸の前の位置
        private bool isMovingToHand = false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
        }

        private void Update()
        {
            // 場外落下時のワープ
            if (transform.position.y < -2.0f)
            {
                ResetToCenter();
            }

            // 持たれていて、かつ親（キャラクター）に紐付いている場合
            if (CurrentState == BallState.Held && transform.parent != null)
            {
                if (isMovingToHand)
                {
                    // ActionControllerが頭の上に移動させた直後でも、ここから胸の前に滑らかに移動（Lerp）させます
                    transform.localPosition = Vector3.Lerp(transform.localPosition, holdLocalPos, Time.deltaTime * 15f);

                    if (Vector3.Distance(transform.localPosition, holdLocalPos) < 0.05f)
                    {
                        isMovingToHand = false;
                        transform.localPosition = holdLocalPos;
                    }
                }
                else
                {
                    // 移動完了後は胸の前にガッチリ固定（お団子状態でズレるのを防ぐ）
                    transform.localPosition = holdLocalPos;
                }
            }
        }

        public void Hold()
        {
            CurrentState = BallState.Held;
            rb.isKinematic = true;
            col.enabled = false;
            LastThrower = null;
            
            // 胸の前に移動するアニメーションフラグをON
            isMovingToHand = true; 
        }

        public void Thrown(GameObject thrower, Vector3 throwVelocity)
        {
            CurrentState = BallState.Thrown;
            isMovingToHand = false;
            LastThrower = thrower;

            transform.SetParent(null);
            rb.isKinematic = false;
            col.enabled = true;

            // ActionControllerで計算されたベクトル（速さ込み）をそのまま物理演算に適用
            rb.linearVelocity = throwVelocity; 
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

        private void ResetToCenter()
        {
            transform.SetParent(null);
            CurrentState = BallState.Free;
            isMovingToHand = false;

            rb.isKinematic = false;
            col.enabled = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            transform.position = new Vector3(0f, 2.0f, 0f);
            LastThrower = null;
            Debug.LogWarning("ボールリセット");
        }
    }
}