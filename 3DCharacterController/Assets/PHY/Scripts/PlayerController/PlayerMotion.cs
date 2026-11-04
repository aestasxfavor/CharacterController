using UnityEngine;

public enum PlayerMotion
{
    Locomotion = 0, // Idle, Move 공용 (Idle, walk, run 블렌드)
    PreJump = 1,    // 점프 시작 (보류)
    Jump = 2,       // 점프 도약
    InAir = 3,      // 공중 (낙하)
    PreLand = 4,    // 착지 준비 (보류)
    Land = 5,       // 착지 
    Run_Jump = 6,
    // 6 이상은 파쿠르 동작
}
