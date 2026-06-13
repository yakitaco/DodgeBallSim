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
        Attack,     // ボールを敵に向かって投げる
        GoToOuter
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

        // 首振り索敵用の制御変数
        private float scanTimer = 0f;
        private bool scanRight = true;

        // 受信した「声」のバッファ
        private List<ReceivedVoice> voiceQueue = new List<ReceivedVoice>();
        private float voiceCooldown = 0f; // 連呼防止

        [Header("戦術移動")]
        private Vector3 tacticalTargetPos; // AIが「次に行きたい」と考える目標座標
        private float tacticTimer = 0f;    // 目標座標を再計算するタイマー

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
            // 【変更】死んでいる場合は外野へ向かう専用処理に切り替え
            if (!myBody.IsAlive)
            {
                currentState = AIState.GoToOuter;
                ExecuteState(null); // 死んでいる時は周りの状況（MentalMap）は気にしない
                return;
            }

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

            if (FindClosestFreeBall(map, out MentalMapObject targetBall))
            {
                if (HasReceivedTeammateVoice(MessageType.GoingForBall, out ReceivedVoice voice))
                {
                    float distanceToBall = targetBall.currentRelativePosition.magnitude;
                    if (voice.volume > 0.4f && distanceToBall > 3.0f)
                    {
                        // 味方に譲って待機するが、ターゲット（ボール）は見失わない
                        currentState = AIState.Idle;
                        return;
                    }
                }

                currentState = AIState.ChaseBall;
                return;
            }

            if (myBody.CurrentPosition == PositionState.Outer && !FindClosestFreeBall(map, out _))
            {
                CallOut(MessageType.PassMe);
            }

            currentState = AIState.Idle;
        }

        private void ExecuteState(List<MentalMapObject> map)
        {
            switch (currentState)
            {
                case AIState.Idle:
                    // 索敵＆ポジション移動（速度50%でウロウロ歩く）
                    UpdateTacticalPosition(map, false);
                    Vector3 toWander = tacticalTargetPos - transform.position;
                    toWander.y = 0;

                    if (FindClosestFreeBall(map, out MentalMapObject ball)) {
                        action.RotateTowards(ball.currentRelativePosition);
                    } else if (FindClosestEnemy(map, out MentalMapObject enemyObj)) {
                        action.RotateTowards(enemyObj.currentRelativePosition);
                    } else {
                        SimulateHumanScanning();
                    }

                    if (toWander.magnitude > 0.5f) {
                        Vector3 localMoveDir = transform.InverseTransformDirection(toWander.normalized);
                        action.Move(localMoveDir, 0.5f); // 半分の速度で歩く
                    } else {
                        action.Move(Vector3.zero);
                    }
                    break;

                case AIState.ChaseBall:
                    // ボールに向かってダッシュ（速度100%）
                    if (FindClosestFreeBall(map, out MentalMapObject targetBall))
                    {
                        CallOut(MessageType.GoingForBall);
                        action.RotateTowards(targetBall.currentRelativePosition);
                        action.Move(targetBall.currentRelativePosition.normalized, 1.0f);

                        if (targetBall.currentRelativePosition.magnitude < 2.0f) {
                            action.TryGrabBall();
                        }
                    }
                    break;

                case AIState.Evade:
                    // 敵から逃げる（陣地の端に沿ってカニ歩き/後退する）
                    Vector3 evadeTarget = transform.position;
                    bool shouldEvade = false;

                    if (FindClosestThreat(map, out MentalMapObject threat)) {
                        Vector3 evadeWorldDir = -transform.TransformDirection(threat.currentRelativePosition).normalized;
                        evadeTarget = transform.position + evadeWorldDir * 5f;
                        shouldEvade = true;
                    } else if (HasReceivedTeammateVoice(MessageType.WatchOut, out ReceivedVoice voice)) {
                        Vector3 evadeWorldDir = -transform.TransformDirection(voice.relativeDirection).normalized;
                        evadeTarget = transform.position + evadeWorldDir * 5f;
                        shouldEvade = true;
                    }

                    if (shouldEvade)
                    {
                        // 壁を突き抜けず、安全な範囲内で最大限逃げる
                        evadeTarget = ClampToMyArea(evadeTarget);
                        Vector3 toEvade = evadeTarget - transform.position;
                        toEvade.y = 0;

                        if (toEvade.magnitude > 0.5f) {
                            Vector3 localEvadeDir = transform.InverseTransformDirection(toEvade.normalized);
                            action.RotateTowards(localEvadeDir); // 逃げる方向へ体を向ける
                            action.Move(localEvadeDir, 1.0f); // ダッシュで逃げる
                        } else {
                            // エリアの端に追い詰められたら立ち止まって敵を警戒
                            if (FindClosestThreat(map, out MentalMapObject threat2)) {
                                action.RotateTowards(threat2.currentRelativePosition);
                            }
                            action.Move(Vector3.zero);
                        }
                    }
                    break;

                case AIState.Attack:
                    if (HasReceivedTeammateVoice(MessageType.PassMe, out ReceivedVoice passReqVoice))
                    {
                        action.RotateTowards(passReqVoice.relativeDirection);
                        action.Move(Vector3.zero);
                        if (passReqVoice.relativeDirection.normalized.z > 0.95f) { action.ThrowBall(); }
                        break;
                    }

                    // 通常攻撃：敵を狙う際、第3引数に true を渡して「相手の内野」のみをターゲットにする
                    if (FindClosestEnemy(map, out MentalMapObject enemy, true))
                    {
                        action.RotateTowards(enemy.currentRelativePosition);

                        // 投げるために前線へ接近する（速度80%の小走り）
                        UpdateTacticalPosition(map, true); 
                        Vector3 toAttackPos = tacticalTargetPos - transform.position;
                        toAttackPos.y = 0;

                        if (toAttackPos.magnitude > 0.5f) {
                            Vector3 localMoveDir = transform.InverseTransformDirection(toAttackPos.normalized);
                            action.Move(localMoveDir, 0.8f); 
                        } else {
                            action.Move(Vector3.zero);
                        }

                        // 正面を捉えており、フェイントや接近が十分だと判断したら投げる
                        Vector3 targetDir = enemy.currentRelativePosition.normalized;
                        if (targetDir.z > 0.95f)
                        {
                            CallOut(MessageType.ImThrowing);
                            action.ThrowBall();
                        }
                    }
                    else
                    {
                        SimulateHumanScanning();
                        action.Move(Vector3.zero);
                    }
                    break;

                case AIState.GoToOuter: // 前回のプロンプトで追加した状態
                    float outerCenterZ = (myBody.MyTeam == Team.TeamA) ? 16.5f : -16.5f;
                    Vector3 targetPos = new Vector3(0f, 1f, outerCenterZ);
                    
                    Vector3 toTarget = targetPos - transform.position;
                    toTarget.y = 0;

                    if (toTarget.magnitude > 1.0f)
                    {
                        Vector3 localDir = transform.InverseTransformDirection(toTarget.normalized);
                        action.RotateTowards(localDir);
                        action.Move(localDir, 0.6f); // 外野へは少しゆっくり歩く
                    }
                    else
                    {
                        action.Move(Vector3.zero);
                        Vector3 lookCenter = transform.InverseTransformDirection((Vector3.zero - transform.position).normalized);
                        action.RotateTowards(lookCenter);
                    }
                    break;
            }
        }

        // 首を振る人間的な索敵処理
        private void SimulateHumanScanning()
        {
            scanTimer += Time.deltaTime;
            if (scanTimer > 1.2f) // 1.2秒ごとに左右へ首を振り直す
            {
                scanRight = !scanRight;
                scanTimer = 0f;
            }

            // 正面(forward)を基準に、左右に少しだけ角度をつけたローカルベクトルを作る
            // ぐるぐる回らず、現在の向きの周辺を優しく見渡す挙動になる
            Vector3 scanDir = scanRight ? (Vector3.forward + Vector3.right * 0.6f) : (Vector3.forward + Vector3.left * 0.6f);
            action.RotateTowards(scanDir);
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
            voiceCooldown = (type == MessageType.GoingForBall) ? 0.2f : 0.5f;
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

        private bool FindClosestEnemy(List<MentalMapObject> map, out MentalMapObject closestEnemy, bool targetInnerOnly = false)
        {
            closestEnemy = default; float minDistance = float.MaxValue; bool found = false;
            foreach (var obj in map) {
                if (obj.gameObject == null) continue;
                CharacterBody otherBody = obj.gameObject.GetComponent<CharacterBody>();
                if (otherBody != null && otherBody.MyTeam != myBody.MyTeam && otherBody.IsAlive) {
                    
                    // 攻撃対象を内野に限定する場合、相手が外野ならスキップする
                    if (targetInnerOnly && otherBody.CurrentPosition != PositionState.Inner) continue;

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
        
        // コート内の自分の陣地（または外野）からはみ出さないように座標を丸める
        private Vector3 ClampToMyArea(Vector3 pos)
        {
            float minX = -9f; float maxX = 9f; // コート横幅（端から少し余裕を持たせる）
            float minZ = 0f;  float maxZ = 0f;

            if (myBody.CurrentPosition == PositionState.Inner)
            {
                // 内野: センターライン(Z=0)を越えないように制限
                if (myBody.MyTeam == Team.TeamA) { minZ = -14f; maxZ = -0.5f; }
                else                             { minZ = 0.5f; maxZ = 14f; }
            }
            else
            {
                // 外野: 相手チームの奥のエリアに制限
                if (myBody.MyTeam == Team.TeamA) { minZ = 15.5f; maxZ = 17.5f; }
                else                             { minZ = -17.5f; maxZ = -15.5f; }
            }

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.z = Mathf.Clamp(pos.z, minZ, maxZ);
            pos.y = transform.position.y;
            return pos;
        }

        // 数秒ごとに「ウロウロする」「分散する」「前に詰める」目標地点を決定する
        private void UpdateTacticalPosition(List<MentalMapObject> map, bool isAttacking = false)
        {
            tacticTimer -= Time.deltaTime;
            if (tacticTimer > 0f) return;
            
            tacticTimer = Random.Range(1.0f, 2.5f); // 1〜2.5秒ごとに次の動きを考える

            Vector3 newTarget = transform.position;

            if (isAttacking)
            {
                // 攻撃時：相手に近い前線（センターライン寄り）へ接近する
                float attackZ = (myBody.MyTeam == Team.TeamA && myBody.CurrentPosition == PositionState.Inner) ? -2f :
                                (myBody.MyTeam == Team.TeamB && myBody.CurrentPosition == PositionState.Inner) ?  2f : 
                                transform.position.z;
                
                // 外野からの攻撃時は内野ラインギリギリまで寄る
                if (myBody.CurrentPosition == PositionState.Outer) {
                    attackZ = (myBody.MyTeam == Team.TeamA) ? 15.5f : -15.5f;
                }
                newTarget = new Vector3(transform.position.x, 1f, attackZ);
            }
            else
            {
                // 通常時：ランダムにウロウロする（ボールを探す＆的を絞らせない）
                newTarget += new Vector3(Random.Range(-4f, 4f), 0, Random.Range(-4f, 4f));
                
                // 味方同士の分散（固まっていると当てられやすいので離れる）
                foreach (var obj in map)
                {
                    if (obj.gameObject == null) continue;
                    CharacterBody other = obj.gameObject.GetComponent<CharacterBody>();
                    if (other != null && other.MyTeam == myBody.MyTeam && other.CurrentPosition == myBody.CurrentPosition)
                    {
                        if (obj.currentRelativePosition.magnitude < 3.0f) // 味方が近すぎたら
                        {
                            // 離れる方向へ反発力を加える
                            Vector3 awayWorldDir = -transform.TransformDirection(obj.currentRelativePosition).normalized;
                            newTarget += awayWorldDir * 2.0f;
                        }
                    }
                }
            }
            // 最終的な目標地点がコート外に出ないように制限
            tacticalTargetPos = ClampToMyArea(newTarget);
        }
    }
}
