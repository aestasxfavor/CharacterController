using UnityEngine;

public enum PlayerMotion
{
    Locomotion = 0, // Idle, Move 공용 (Idle, walk, run 블렌드)
    Jump = 1,       // 제자리 점프 (단일 Jump 클립)
    Run_Jump = 2,   // 이동 중 점프 (Run_Jump 블렌드)
    Fall = 3,       // 낙하 
    FallingToRoll = 4, // 낙하 후 구르기
    Vault = 5,
    Mantle = 6,
    Slide = 7,
    Wall_Run = 8,

    // 4 이상은 파쿠르 동작
}
