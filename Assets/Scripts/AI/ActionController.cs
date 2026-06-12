using UnityEngine;
using DodgeBallSim.Entities;
using DodgeBallSim.Core;
using DodgeBallSim.Data;

namespace DodgeBallSim.AI
{
    public class ActionController : MonoBehaviour
    {
        private CharacterBody myBody;
        private Rigidbody rb;
        
        [Header("運動性能")]
        public float moveSpeed = 6f;
        public float rotationSpeed = 10f;
        public float throwForce = 20f;
        public float grabRadius = 2.5f;

        private Ball heldBall = null;
        public bool HasBall => heldBall != null;

        private void Awake()
        {
            myBody = GetComponent<CharacterBody>();
            rb = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            UpdateHeldBallPosition();
        }

        // --- DecisionMakerから呼ばれる運動API ---

        // 相対方向（ローカルベクトル）へ移動する
        public void Move(Vector3 localDirection)
        {
            if (localDirection == Vector3.zero)
            {
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
                return;
            }

            // ローカル方向をワールド方向の速度に変換
            Vector3 worldDirection = transform.TransformDirection(localDirection.normalized);
            rb.linearVelocity = new Vector3(worldDirection.x * moveSpeed, rb.linearVelocity.y, worldDirection.z * moveSpeed);
        }

        // 相対方向（ローカルベクトル）へ体を向ける
        public void RotateTowards(Vector3 localDirection)
        {
            if (localDirection == Vector3.zero) return;

            // ローカル方向をワールド方向に変換し、そこへ向かって徐々に回転
            Vector3 worldDirection = transform.TransformDirection(localDirection.normalized);
            worldDirection.y = 0; // 上下には傾かないようにする

            if (worldDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(worldDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        // ボールを拾う（手探りの物理判定）
        public void TryGrabBall()
        {
            if (HasBall) return;

            // 近くのボールを物理的に探す
            Collider[] colliders = Physics.OverlapSphere(transform.position, grabRadius);
            foreach (Collider col in colliders)
            {
                Ball ball = col.GetComponent<Ball>();
                if (ball != null && ball.CurrentState != BallState.Held)
                {
                    heldBall = ball;
                    heldBall.Hold();
                    
                    if (RefereeSystem.Instance != null)
                    {
                        RefereeSystem.Instance.ReportCatch(heldBall, myBody);
                    }
                    Debug.Log($"{gameObject.name} が自律的にボールを取得しました。");
                    break;
                }
            }
        }

        // 正面（ワールド前方）へボールを投げる
        public void ThrowBall()
        {
            if (!HasBall) return;

            // 頭上やや前方から射出
            //heldBall.transform.position = transform.position + transform.forward * 1.5f + Vector3.up * 1.2f;
            
            // 相手の内野に届くように斜め上へ投げる
            Vector3 throwVector = transform.forward * throwForce + Vector3.up * 3f;
            heldBall.Thrown(gameObject, throwVector);
            
            heldBall = null;
        }

        // --- 内部処理 ---

        private void UpdateHeldBallPosition()
        {
            // ボールを持っている間は頭上にキープ
            if (HasBall)
            {
                //heldBall.transform.position = transform.position + Vector3.up * 2.5f;
            }
        }
    }
}