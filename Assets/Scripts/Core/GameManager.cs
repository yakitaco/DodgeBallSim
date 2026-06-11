using UnityEngine;
using DodgeBallSim.Data;

namespace DodgeBallSim.Core
{
    public class GameManager : MonoBehaviour
    {
        [Header("設定データ（未指定の場合はデフォルトを使用）")]
        [SerializeField] private GameSettings gameSettings;

        private FieldBuilder fieldBuilder;
        private bool isGameRunning = false;

        private void Awake()
        {
            // 同じオブジェクトにFieldBuilderを追加して確保
            fieldBuilder = gameObject.AddComponent<FieldBuilder>();

            // 設定ファイルのヌルチェックとフォールバック
            if (gameSettings == null)
            {
                gameSettings = ScriptableObject.CreateInstance<GameSettings>();
                Debug.LogWarning("GameSettingsが割り当てられていないため、デフォルト設定を使用します。");
            }
        }

        private void Start()
        {
            // アプリケーション起動時に自動で箱庭を構築
            InitializeGame();
        }

        public void InitializeGame()
        {
            Debug.Log("--- 箱庭構築 ---");
            
            // フィールド、キャラクター、ボールの自動生成を実行
            fieldBuilder.BuildField(gameSettings);
            
            // 構築完了後、即座にゲームを開始
            StartGame();
        }

        public void StartGame()
        {
            isGameRunning = true;
            Debug.Log("--- ゲーム開始 ---");
            
            // フェーズ4以降で、ここに各AIキャラクターの意思決定（Update）をONにするフラグを入れます
        }

        public void EndGame(Team winningTeam)
        {
            isGameRunning = false;
            Debug.Log($"--- ゲーム終了！ 勝者チーム: {winningTeam} ---");
            
            // 時間の停止や、シミュレーション結果のログ出力をここに実装
        }

        private void Update()
        {
            if (!isGameRunning) return;

            // キーボードの「E」キーで強制的にゲームを終了
            if (Input.GetKeyDown(KeyCode.E))
            {
                EndGame(Team.TeamA);
            }
        }
    }
}