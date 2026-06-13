using UnityEngine;
using DodgeBallSim.Data;
using DodgeBallSim.Entities;
using DodgeBallSim.AI;

namespace DodgeBallSim.Core
{
    public class FieldBuilder : MonoBehaviour
    {
        private GameSettings settings;

        public void BuildField(GameSettings gameSettings)
        {
            settings = gameSettings;

            // 1. コート・環境の作成
            CreateEnvironment();

            // フィールドを囲む壁の作成（ボールの飛散防止）
            CreateWalls();

            // 2. チームAの配置（マイナスZ側）
            SpawnTeam(Team.TeamA, settings.teamAInnerCount, settings.teamAOuterCount);

            // 3. チームBの配置（プラスZ側）
            SpawnTeam(Team.TeamB, settings.teamBInnerCount, settings.teamBOuterCount);

            // 4. ボールの配置
            SpawnBalls();
        }

        private void CreateEnvironment()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.tag = "Ground";
            ground.transform.position = Vector3.zero;

            float totalLength = settings.courtLength + (settings.outerZoneWidth * 2f);
            ground.transform.localScale = new Vector3(settings.courtWidth / 10f, 1f, totalLength / 10f);

            Renderer groundRenderer = ground.GetComponent<Renderer>();
            groundRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundRenderer.material.color = new Color(0.2f, 0.6f, 0.2f);

            float lineWidth = 0.2f;
            float lineThickness = 0.01f;
            Color lineColor = Color.white;

            // 1. センターライン (Z = 0)
            CreateLine("CenterLine", new Vector3(0f, lineThickness, 0f), new Vector3(settings.courtWidth, lineThickness, lineWidth), lineColor);

            // 2. チームA側 エンドライン（内野と外野の境界線 Z = -courtLength / 2）
            float endLineA_Z = -settings.courtLength / 2f;
            CreateLine("EndLine_A", new Vector3(0f, lineThickness, endLineA_Z), new Vector3(settings.courtWidth, lineThickness, lineWidth), lineColor);

            // 3. チームB側 エンドライン（内野と外野の境界線 Z = courtLength / 2）
            float endLineB_Z = settings.courtLength / 2f;
            CreateLine("EndLine_B", new Vector3(0f, lineThickness, endLineB_Z), new Vector3(settings.courtWidth, lineThickness, lineWidth), lineColor);

            // 4. 左サイドライン (X = -courtWidth / 2, 全長にわたって描画)
            CreateLine("SideLine_Left", new Vector3(-settings.courtWidth / 2f, lineThickness, 0f), new Vector3(lineWidth, lineThickness, totalLength), lineColor);

            // 5. 右サイドライン (X = courtWidth / 2, 全長にわたって描画)
            CreateLine("SideLine_Right", new Vector3(settings.courtWidth / 2f, lineThickness, 0f), new Vector3(lineWidth, lineThickness, totalLength), lineColor);

            // 6. 外野奥のバックライン（チームA側 Z = -totalLength / 2）
            CreateLine("OuterBackLine_A", new Vector3(0f, lineThickness, -totalLength / 2f), new Vector3(settings.courtWidth, lineThickness, lineWidth), lineColor);

            // 7. 外野奥のバックライン（チームB側 Z = totalLength / 2）
            CreateLine("OuterBackLine_B", new Vector3(0f, lineThickness, totalLength / 2f), new Vector3(settings.courtWidth, lineThickness, lineWidth), lineColor);
        }

        // 【新規追加】ラインを個別に生成するヘルパーメソッド
        private void CreateLine(string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = name;
            line.transform.position = position;
            line.transform.localScale = scale;
            
            Renderer lineRenderer = line.GetComponent<Renderer>();
            lineRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            lineRenderer.material.color = color;
            
            // ボールやキャラクターの物理演算に干渉しないよう、コライダーを削除
            Destroy(line.GetComponent<Collider>());
        }

        // 外野も含めた全エリアを囲む壁を生成するメソッド
        private void CreateWalls()
        {
            float totalLength = settings.courtLength + (settings.outerZoneWidth * 2f);
            float wallHeight = 5f; // ボールが飛び越えない十分な高さ
            float wallThickness = 0.5f; // 壁の厚み

            // 半透明のグレーの共通マテリアル（視覚的に壁がわかるように）
            Material wallMat = Resources.Load<Material>("WallMaterial");

            // 1. 奥の壁（チームB側外野の後ろ）
            BuildWall("Wall_Back_B", new Vector3(0f, wallHeight / 2f, totalLength / 2f), new Vector3(settings.courtWidth, wallHeight, wallThickness), wallMat);

            // 2. 手前の壁（チームA側外野の後ろ）
            BuildWall("Wall_Back_A", new Vector3(0f, wallHeight / 2f, -totalLength / 2f), new Vector3(settings.courtWidth, wallHeight, wallThickness), wallMat);

            // 3. 左の壁
            BuildWall("Wall_Left", new Vector3(-settings.courtWidth / 2f, wallHeight / 2f, 0f), new Vector3(wallThickness, wallHeight, totalLength), wallMat);

            // 4. 右の壁
            BuildWall("Wall_Right", new Vector3(settings.courtWidth / 2f, wallHeight / 2f, 0f), new Vector3(wallThickness, wallHeight, totalLength), wallMat);
        }

        // 壁の個別生成ヘルパー
        private void BuildWall(string name, Vector3 position, Vector3 scale, Material mat)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().material = mat;
        }

        private void SpawnTeam(Team team, int innerCount, int outerCount)
        {
            Color teamColor = (team == Team.TeamA) ? Color.blue : Color.red;
            float sideSign = (team == Team.TeamA) ? -1f : 1f;

            for (int i = 0; i < innerCount; i++)
            {
                float row = i / 3;
                float col = i % 3;
                float posX = (col - 1f) * (settings.courtWidth / 4f);
                float posZ = sideSign * (5f + row * 3f);
                
                CreateCharacter(team, PositionState.Inner, new Vector3(posX, 1f, posZ), teamColor);
            }

            for (int i = 0; i < outerCount; i++)
            {
                float posX = (i - (outerCount - 1) / 2f) * (settings.courtWidth / (outerCount + 1));
                float posZ = -sideSign * (settings.courtLength / 2f + settings.outerZoneWidth / 2f);

                CreateCharacter(team, PositionState.Outer, new Vector3(posX, 1f, posZ), teamColor);
            }
        }

        private void CreateCharacter(Team team, PositionState position, Vector3 spawnPos, Color color)
        {
            GameObject charObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            charObj.name = $"{team}_{position}_Character";
            charObj.transform.position = spawnPos;
            charObj.GetComponent<Renderer>().material.color = color;

            // キャラクター自身のレイヤーをデフォルトにする
            charObj.layer = LayerMask.NameToLayer("Default");

            // 子供のHeadも同様
            GameObject headObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            headObj.name = "Head";
            headObj.transform.SetParent(charObj.transform);
            headObj.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            headObj.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            headObj.GetComponent<Renderer>().material.color = new Color(0.9f, 0.7f, 0.6f);

            Rigidbody rb = charObj.AddComponent<Rigidbody>();
            rb.mass = 60f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            CharacterBody bodyComp = charObj.AddComponent<CharacterBody>();
            bodyComp.Initialize(team, position);

            charObj.AddComponent<SensorSystem>();
            charObj.AddComponent<MemorySystem>();
            charObj.AddComponent<ActionController>();
            charObj.AddComponent<DecisionMaker>();
        }

        private void SpawnBalls()
        {
            PhysicsMaterial ballPhysicMaterial = new PhysicsMaterial("BallPhysics")
            {
                bounciness = settings.ballBounciness,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                dynamicFriction = settings.ballFriction,
                staticFriction = settings.ballFriction,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };

            for (int i = 0; i < settings.ballCount; i++)
            {
                GameObject ballObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ballObj.name = $"Ball_{i}";
                ballObj.tag = "Ball";
                
                float posX = (i - (settings.ballCount - 1) / 2f) * 2f;
                ballObj.transform.position = new Vector3(posX, 0.5f, 0f);
                ballObj.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
                ballObj.GetComponent<Renderer>().material.color = Color.yellow;

                SphereCollider collider = ballObj.GetComponent<SphereCollider>();
                collider.material = ballPhysicMaterial;

                Rigidbody rb = ballObj.AddComponent<Rigidbody>();
                rb.mass = 0.4f;
                rb.linearDamping = 0.1f;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                ballObj.AddComponent<Ball>();
            }
        }
    }
}