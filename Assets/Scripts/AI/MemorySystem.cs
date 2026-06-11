using System.Collections.Generic;
using UnityEngine;

namespace DodgeBallSim.AI
{
    // 脳内に保持する記憶のデータ構造
    public class MemoryRecord
    {
        public GameObject gameObject;
        public Vector3 lastKnownWorldPosition; // 脳内にメモする絶対位置
        public string tag;
        public float timeSinceLastSeen;        // 最後に見失ってからの経過時間

        public MemoryRecord(GameObject obj, Vector3 worldPos, string t)
        {
            gameObject = obj;
            lastKnownWorldPosition = worldPos;
            tag = t;
            timeSinceLastSeen = 0f;
        }
    }

    // 思考システム（DecisionMaker）に渡すための相対データ構造
    public struct MentalMapObject
    {
        public GameObject gameObject;
        public Vector3 currentRelativePosition; // 現時点の自分から見た推測相対座標
        public float age;                       // 記憶の古さ（秒）
        public string tag;
    }

    public class MemorySystem : MonoBehaviour
    {
        [Header("記憶設定")]
        public float memoryDuration = 3.0f; // 視界から消えて何秒間記憶を保持するか

        // 脳内メンタルマップ（オブジェクトのインスタンスIDをキーに保持）
        private Dictionary<int, MemoryRecord> mentalMap = new Dictionary<int, MemoryRecord>();

        private SensorSystem sensor;

        private void Awake()
        {
            sensor = GetComponent<SensorSystem>();
        }

        private void Update()
        {
            // 1. センサーから今見えている最新の情報を取得
            if (sensor != null)
            {
                List<SensorDetection> currentDetections = sensor.ScanEnvironment();
                UpdateMemory(currentDetections);
            }

            // 2. 記憶の風化処理（時間の更新と忘却）
            AgeAndCleanMemory();
        }

        // センサーの情報を元に脳内マップを更新・上書き
        private void UpdateMemory(List<SensorDetection> detections)
        {
            foreach (var det in detections)
            {
                if (det.gameObject == null) continue;

                int id = det.gameObject.GetInstanceID();
                // 自分から見た相対座標を、現在の自分の位置を基準にワールド座標に復元
                Vector3 worldPos = transform.TransformPoint(det.relativePosition);

                if (mentalMap.ContainsKey(id))
                {
                    // 既に知っているオブジェクトなら位置を更新し、経過時間をリセット
                    mentalMap[id].lastKnownWorldPosition = worldPos;
                    mentalMap[id].timeSinceLastSeen = 0f;
                }
                else
                {
                    // 新しく見つけたオブジェクトを記憶に登録
                    mentalMap[id] = new MemoryRecord(det.gameObject, worldPos, det.tag);
                }
            }
        }

        // 時間を進行させ、古い記憶を消去
        private void AgeAndCleanMemory()
        {
            List<int> keysToRemove = new List<int>();
            List<int> keysToIncrement = new List<int>(mentalMap.Keys);

            foreach (int id in keysToIncrement)
            {
                MemoryRecord record = mentalMap[id];
                record.timeSinceLastSeen += Time.deltaTime;

                // 一定時間見失ったものは忘れる
                if (record.timeSinceLastSeen > memoryDuration)
                {
                    keysToRemove.Add(id);
                }
            }

            foreach (int id in keysToRemove)
            {
                mentalMap.Remove(id);
            }
        }

        // 思考ルーチンへ「現在の自分から見た相対位置」に変換して記憶を返す
        public List<MentalMapObject> GetMentalMap()
        {
            List<MentalMapObject> currentMentalMap = new List<MentalMapObject>();

            foreach (var pair in mentalMap.Values)
            {
                if (pair.gameObject == null) continue;

                MentalMapObject mapObj = new MentalMapObject
                {
                    gameObject = pair.gameObject,
                    // 記憶している絶対座標を、現在の自分の位置・向きを基準にした相対座標へ再変換
                    currentRelativePosition = transform.InverseTransformPoint(pair.lastKnownWorldPosition),
                    age = pair.timeSinceLastSeen,
                    tag = pair.tag
                };
                currentMentalMap.Add(mapObj);
            }

            return currentMentalMap;
        }

        // デバッグ用：脳内メモリにある位置に Gizmos で球体を表示（Sceneビューでのみ視覚化）
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            foreach (var record in mentalMap.Values)
            {
                // 見えているものは青、見失って記憶で推測しているものは黄色
                Gizmos.color = (record.timeSinceLastSeen == 0f) ? Color.blue : Color.yellow;
                Gizmos.DrawWireSphere(record.lastKnownWorldPosition, 0.4f);
            }
        }
    }
}