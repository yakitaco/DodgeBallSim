using System.Collections.Generic;
using UnityEngine;
using DodgeBallSim.Entities;
using DodgeBallSim.Data;
using DodgeBallSim.Communication;

namespace DodgeBallSim.AI
{

    public enum AIState
    {
        Idle,       // 視界を回して状況を把握する
        ChaseBall,  // 落ちているボールを拾いに行く
        Evade,      // ボールを持った敵から逃げる
        Attack      // ボールを敵に向かって投げる
    }

    [RequireComponent(typeof(MemorySystem), typeof(ActionController), typeof(CharacterBody))]
    public class DecisionMaker : MonoBehaviour
    {
        private MemorySystem memory;
        private ActionController action;
        private CharacterBody myBody;

        [Header("AI状態")]
        public AIState currentState = AIState.Idle;

        [Header("パラメータ")]
        public float attackRange = 15f;
        public float evadeDistance = 8f;
        public float voiceRange = 20f; // 声の届く最大距離

        // 受信した「声」のバッファ
        private List<ReceivedVoice> voiceQueue = new List<ReceivedVoice>();
        private float voiceCooldown = 0f; // 連呼防止

        private void Awake()
        {
            memory = GetComponent<MemorySystem>();
            action = GetComponent<ActionController>();
            myBody = GetComponent<CharacterBody>();
        }

        // 外部の CommsNetwork から声が届く物理窓口
        public void ReceiveVoice(VoiceMessage msg, float finalVolume)
        {
            if (!myBody.IsAlive) return;

            // 声がした絶対方向を、自分起点の相対方向に変換（神の視点の排除）
            Vector3 worldDirToSender = (msg.senderWorldPos - transform.position).normalized;
            Vector3 localDirToSender = transform.InverseTransformDirection(worldDirToSender);

            ReceivedVoice rxVoice = new ReceivedVoice
            {
                type = msg.type,
                relativeDirection = localDirToSender,
                volume = finalVolume,
                isTeammate = (msg.senderTeam == myBody.MyTeam)
            };

            voiceQueue.Add(rxVoice);
        }

        private void Update()
        {
            if (!myBody.IsAlive) return;

            if (voiceCooldown > 0) voiceCooldown -= Time.deltaTime;

            List<MentalMapObject> mentalMap = memory.GetMentalMap();

            DetermineState(mentalMap);
            ExecuteState(mentalMap);

            // フレームの最後に処理した声のキューをクリア
            voiceQueue.Clear();
        }

        private void DetermineState(List<MentalMapObject> map)
        {
            if (action.HasBall)
            {
                currentState = AIState.Attack;
                return;
            }

            // 視界になくても、味方から「危ない！」と言われたら強制回避
            if (HasReceivedTeammateVoice(MessageType.WatchOut, out _))
            {
                currentState = AIState.Evade;
                Debug.Log($"{gameObject.name}は味方の警告を聞いて死角から回避！");
                return;
            }

            if (IsThreatened(map))
            {
                // 自分を狙っている敵を視界に捉えたら、周囲の味方に「危ない！」と叫ぶ
                CallOut(MessageType.WatchOut);
                currentState = AIState.Evade;
                return;
            }

            // 外野にいて、フリーのボールが転がっていない場合、内野の味方に「パスくれ！」と要求
            if (myBody.CurrentPosition == PositionState.Outer && !FindClosestFreeBall(map, out _))
            {
                CallOut(MessageType.PassMe);
            }

            if (FindClosestFreeBall(map, out _))
            {
                currentState = AIState.ChaseBall;
                return;
            }

            currentState = AIState.Idle;
        }

        private void ExecuteState(List<MentalMapObject> map)
        {
            switch (currentState)
            {
                case AIState.Idle:
                    action.RotateTowards(new Vector3(1f, 0f, 1f));
                    action.Move(Vector3.zero);
                    break;

                case AIState.ChaseBall:
                    if (FindClosestFreeBall(map, out MentalMapObject targetBall))
                    {
                        action.RotateTowards(targetBall.currentRelativePosition);
                        action.Move(targetBall.currentRelativePosition.normalized);

                        if (targetBall.currentRelativePosition.magnitude < 2.0f)
                        {
                            action.TryGrabBall();
                        }
                    }
                    break;

                case AIState.Evade:
                    // 視界の敵から逃げる
                    if (FindClosestThreat(map, out MentalMapObject threat))
                    {
                        Vector3 evadeDir = -threat.currentRelativePosition;
                        action.RotateTowards(evadeDir);
                        action.Move(evadeDir.normalized);
                    }
                    // 視界にいないが声で警告された場合、聞こえた方向の「真後ろ」へ逃げる
                    else if (HasReceivedTeammateVoice(MessageType.WatchOut, out ReceivedVoice voice))
                    {
                        Vector3 evadeDir = -voice.relativeDirection;
                        action.RotateTowards(evadeDir);
                        action.Move(evadeDir.normalized);
                    }
                    break;

                case AIState.Attack:
                    // 外野の味方から「パス！」の要求があり、かつ目の前に敵が詰まっていない場合パスを出す（挟み撃ち戦術）
                    if (HasReceivedTeammateVoice(MessageType.PassMe, out ReceivedVoice passReqVoice))
                    {
                        Debug.Log($"【AI協調】{gameObject.name} は味方のパス要求を検知。声の方向へパスします！");
                        action.RotateTowards(passReqVoice.relativeDirection);
                        action.Move(Vector3.zero);
                        
                        if (passReqVoice.relativeDirection.normalized.z > 0.95f)
                        {
                            action.ThrowBall();
                        }
                        break;
                    }

                    // 通常攻撃：敵を狙う
                    if (FindClosestEnemy(map, out MentalMapObject enemy))
                    {
                        action.RotateTowards(enemy.currentRelativePosition);
                        action.Move(Vector3.zero);

                        Vector3 targetDir = enemy.currentRelativePosition.normalized;
                        if (targetDir.z > 0.95f)
                        {
                            CallOut(MessageType.ImThrowing); // 「投げるぞ！」と味方に宣言
                            action.ThrowBall();
                        }
                    }
                    else
                    {
                        action.RotateTowards(new Vector3(1f, 0f, 1f));
                    }
                    break;
            }
        }

        // 空間ネットワークへ声を発信する処理
        private void CallOut(MessageType type)
        {
            if (voiceCooldown > 0f || CommsNetwork.Instance == null) return;

            VoiceMessage msg = new VoiceMessage
            {
                senderTeam = myBody.MyTeam,
                sender = gameObject,
                type = type,
                senderWorldPos = transform.position,
                volume = 1.0f
            };

            CommsNetwork.Instance.BroadcastVoice(msg, voiceRange);
            voiceCooldown = 0.5f; // 連呼スロットリング（0.5秒間は静かにする）
        }

        // 特定のメッセージを味方から受信したかチェックするヘルパー
        private bool HasReceivedTeammateVoice(MessageType type, out ReceivedVoice foundVoice)
        {
            foreach (var voice in voiceQueue)
            {
                if (voice.isTeammate && voice.type == type)
                {
                    foundVoice = voice;
                    return true;
                }
            }
            foundVoice = default;
            return false;
        }

        private bool FindClosestFreeBall(List<MentalMapObject> map, out MentalMapObject closestBall)
        {
            closestBall = default; float minDistance = float.MaxValue; bool found = false;
            foreach (var obj in map) {
                if (obj.gameObject == null || !obj.gameObject.CompareTag("Ball")) continue;
                Ball ballComp = obj.gameObject.GetComponent<Ball>();
                if (ballComp != null && ballComp.CurrentState != BallState.Held) {
                    float dist = obj.currentRelativePosition.magnitude;
                    if (dist < minDistance) { minDistance = dist; closestBall = obj; found = true; }
                }
            }
            return found;
        }

        private bool FindClosestEnemy(List<MentalMapObject> map, out MentalMapObject closestEnemy)
        {
            closestEnemy = default; float minDistance = float.MaxValue; bool found = false;
            foreach (var obj in map) {
                if (obj.gameObject == null) continue;
                CharacterBody otherBody = obj.gameObject.GetComponent<CharacterBody>();
                if (otherBody != null && otherBody.MyTeam != myBody.MyTeam && otherBody.IsAlive) {
                    float dist = obj.currentRelativePosition.magnitude;
                    if (dist < minDistance) { minDistance = dist; closestEnemy = obj; found = true; }
                }
            }
            return found;
        }

        private bool FindClosestThreat(List<MentalMapObject> map, out MentalMapObject threat)
        {
            threat = default;
            if (FindClosestEnemy(map, out MentalMapObject enemy)) {
                if (enemy.currentRelativePosition.magnitude < evadeDistance) { threat = enemy; return true; }
            }
            return false;
        }

        private bool IsThreatened(List<MentalMapObject> map) { return FindClosestThreat(map, out _); }
    }
}