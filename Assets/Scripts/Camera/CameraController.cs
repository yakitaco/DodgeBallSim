using UnityEngine;
using DodgeBallSim.Entities;
using DodgeBallSim.AI;
using DodgeBallSim.Data;

namespace DodgeBallSim.Core
{
    public class CameraController : MonoBehaviour
    {
        [Header("追従対象")]
        [Tooltip("追従するボール。未設定の場合は実行中に自動取得します。")]
        public Ball targetBall;

        [Header("トランジション（視点切り替え）設定")]
        [Tooltip("視点が切り替わる際にかかる時間（秒）")]
        public float transitionDuration = 1.5f;

        [Header("俯瞰視点 (Bird's-eye) パラメータ")]
        public Vector3 birdseyeOffset = new Vector3(0f, 8f, -6f);

        [Header("背後視点 (TPS) パラメータ")]
        public Vector3 tpsOffset = new Vector3(0f, 2.5f, -4f);

        private ActionController[] allControllers;
        private ActionController currentCarrier;

        private enum CameraMode { Birdseye, TPS }
        private CameraMode currentMode = CameraMode.Birdseye;
        
        // 起動時の初期配置が完了したかどうかのフラグ
        private bool isFirstSetupDone = false;

        // トランジション用変数
        private float transitionTimer = 0f;
        private bool isTransitioning = false;
        private Vector3 transitionStartPos;
        private Quaternion transitionStartRot;

        // 滑らかな追従用
        private Vector3 currentVelocity;

        private void LateUpdate()
        {
            // ボールが未設定の場合、動的に検索を試みる（起動時・生成待ち対策）
            if (targetBall == null)
            {
                targetBall = FindObjectOfType<Ball>();
                if (targetBall == null)
                {
                    // まだシーン内にボールが生成されていない場合は処理をスキップ
                    return;
                }
            }

            // キャラクターコントローラーも生成されたタイミングで動的に取得
            if (allControllers == null || allControllers.Length == 0)
            {
                allControllers = FindObjectsOfType<ActionController>();
            }

            // ボール発見直後の最初の1フレームだけ、カメラを初期位置へワープさせる
            if (!isFirstSetupDone)
            {
                InitializeCameraPosition();
            }

            // ボールの状態をチェックし、必要に応じて視点モードを切り替える
            CheckState();

            // 現在のモードにおける「理想のカメラ位置と向き」を計算
            UpdateTargetTransform(out Vector3 targetPos, out Quaternion targetRot);

            if (isTransitioning)
            {
                // 視点切り替え中の補間処理（イーズインアウト）
                transitionTimer += Time.deltaTime;
                float t = Mathf.Clamp01(transitionTimer / transitionDuration);
                
                // 開始と終了がゆっくり、中間が速いイーズインアウトのカーブ (SmoothStep)
                float easeT = Mathf.SmoothStep(0f, 1f, t);

                transform.position = Vector3.Lerp(transitionStartPos, targetPos, easeT);
                transform.rotation = Quaternion.Slerp(transitionStartRot, targetRot, easeT);

                // トランジション完了判定
                if (t >= 1f)
                {
                    isTransitioning = false;
                }
            }
            else
            {
                // トランジション完了後：滑らかに追従
                transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, 0.15f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
            }
        }

        /// <summary>
        /// ボール生成を検知した瞬間、カメラを最初の位置に直接セットする処理
        /// </summary>
        private void InitializeCameraPosition()
        {
            currentMode = (targetBall.CurrentState == BallState.Held) ? CameraMode.TPS : CameraMode.Birdseye;
            UpdateTargetTransform(out Vector3 initPos, out Quaternion initRot);
            
            transform.position = initPos;
            transform.rotation = initRot;
            isFirstSetupDone = true;
        }

        private void CheckState()
        {
            CameraMode nextMode = CameraMode.Birdseye;

            if (targetBall.CurrentState == BallState.Held)
            {
                nextMode = CameraMode.TPS;
                // 現在ボールを持っているキャラクターを特定する
                if (currentCarrier == null)
                {
                    currentCarrier = FindCarrier();
                }
            }
            else
            {
                currentCarrier = null;
            }

            // 状態が変わった瞬間、トランジションを開始する
            if (nextMode != currentMode)
            {
                currentMode = nextMode;
                isTransitioning = true;
                transitionTimer = 0f;
                transitionStartPos = transform.position;
                transitionStartRot = transform.rotation;
            }
        }

        private ActionController FindCarrier()
        {
            if (allControllers == null || allControllers.Length == 0) return null;

            foreach (var ac in allControllers)
            {
                Vector3 expectedBallPos = ac.transform.position + Vector3.up * 2.5f;
                if (Vector3.Distance(expectedBallPos, targetBall.transform.position) < 0.5f)
                {
                    return ac;
                }
            }
            return null;
        }

        private void UpdateTargetTransform(out Vector3 targetPos, out Quaternion targetRot)
        {
            if (currentMode == CameraMode.TPS && currentCarrier != null)
            {
                // 【背後視点】キャラクターの背後・上空に配置
                Vector3 carrierPos = currentCarrier.transform.position;
                Vector3 carrierForward = currentCarrier.transform.forward;

                targetPos = carrierPos - carrierForward * Mathf.Abs(tpsOffset.z) + Vector3.up * tpsOffset.y;
                Vector3 lookDir = carrierForward + Vector3.down * 0.1f;
                targetRot = Quaternion.LookRotation(lookDir); 
            }
            else
            {
                // 【俯瞰視点】ボールの斜め後方上空に配置
                targetPos = targetBall.transform.position + birdseyeOffset;
                targetRot = Quaternion.LookRotation(targetBall.transform.position - targetPos);
            }
        }
    }
}