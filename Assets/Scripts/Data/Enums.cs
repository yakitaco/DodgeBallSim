namespace DodgeBallSim.Data
{
    // チームの定義
    public enum Team 
    { 
        TeamA, 
        TeamB 
    }

    // 内野・外野の状態
    public enum PositionState 
    { 
        Inner, 
        Outer 
    }

    // ボールの物理・ルール上の状態
    public enum BallState 
    { 
        Idle,       // 誰にも持たれず転がっている
        Held,       // 誰かが持っている
        Thrown,     // 投げられてノーバウンドの状態（攻撃力あり）
        Bounded     // 地面にバウンドした状態（攻撃力消失）
    }
}