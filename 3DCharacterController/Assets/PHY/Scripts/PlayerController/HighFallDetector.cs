using Unity.VisualScripting;
using UnityEngine;
/// <summary>
/// 낙하 거리(최고점 Y - 현재 Y)를 기록하고 착지 모션 여부를 판단
/// Airborne 시작 기준(아래 땅까지의 거리)도 Inspector에서 함께 조정
/// </summary>
public class HighFallDetector : MonoBehaviour
{
    [Header("Airborne Motion")]
    // 아래 땅까지의 거리가 이 값보다 멀 때만 Airborne 모션 시작
    // GroundDectector의 maxGroundCheckDistance(1.0)보다 크면 의미가 없음
    [SerializeField] private float airborneGroundDistance = 1.0f;

    [Header("Land Motion")]
    // 이 거리 이상 떨어졌으면 착지 모션 발생
    [SerializeField] private float landMotionDistance = 1.2f;

    public float AirborneGroundDistance => airborneGroundDistance;
    public float PeakY { get; private set; }
    public float FallDistance { get; private set; }
    public bool IsHighFall => FallDistance >= landMotionDistance;

    private bool isTracking;

    public void StartFall(float currentY)
    {
        isTracking = true;
        PeakY = currentY;
        FallDistance = 0f;
    }

    public void UpdateFall(float currentY)
    {
        if (!isTracking) return;

        if (currentY > PeakY) PeakY = currentY;
        FallDistance = PeakY - currentY;
    }

    public bool GetResultAndReset()
    {
        bool result = IsHighFall;
        ResetFall();
        return result;
    }

    public void ResetFall()
    {
        isTracking = false;
        PeakY = 0f;
        FallDistance = 0f;
    }
}