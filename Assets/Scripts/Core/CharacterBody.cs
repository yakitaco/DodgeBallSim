using UnityEngine;
using DodgeBallSim.Data;

namespace DodgeBallSim.Entities
{
    public class CharacterBody : MonoBehaviour
    {
        public Team MyTeam { get; private set; }
        public PositionState CurrentPosition { get; private set; }
        public bool IsAlive { get; private set; } = true;

        public void Initialize(Team team, PositionState position)
        {
            MyTeam = team;
            CurrentPosition = position;
        }

        // 内野から外野、外野から内野への移動時の状態変更
        public void ChangePosition(PositionState newPosition)
        {
            CurrentPosition = newPosition;
            Debug.Log($"{gameObject.name} が {newPosition} に移動しました。");
        }

        // 被弾してアウトになった時
        public void Eliminate()
        {
            IsAlive = false;
            // 審判システムから呼ばれ、外野への移動処理の引き金になります
        }
    }
}