using UnityEngine;
/// <summary>
/// 낙하 시간과 최저 Y속도를 기준으로 높은 낙하 여부를 판단하는 보조 클래스
/// FallState에서 착지 연출 여부를 결정할 때 사용함
/// </summary>
public class HighFallDetector : MonoBehaviour
{
    [Header("High Fall Check")]
    [SerializeField] private float highFallTime = 0.35f;
    [SerializeField] private float highFallSpeed = -4f;

    /// <summary>
    /// 현재 낙하가 지속된 시간
    /// </summary>
    public float FallTimer { get; private set; }

    /// <summary>
    /// 낙하 중 기록된 가장 낮은 Y 속도
    /// </summary>
    public float LowYSpeed { get; private set; }

    /// <summary>
    ///  현재 낙하가 높은 낙하로 판정됐는지 여부
    /// </summary>
    public bool IsHighFall { get; private set; }

    private bool isTracking;

    /// <summary>
    /// FallState 진입 시 높은 낙하와 판정을 위한 기록 시작
    /// </summary>
    /// <param name="currentYSpeed">FallState 진입 시점의 Y속도</param>
    public void StartFall(float currentYSpeed)
    {
        isTracking = true;
        FallTimer = 0f;
        LowYSpeed = currentYSpeed;
        IsHighFall = false;

        Debug.Log("HighFallDetector StartFall");
    }

    /// <summary>
    /// 낙하 중 시간과 최저 Y속도를 갱신하고 높은 낙하인지 판단
    /// </summary>
    /// <param name="deltaTime">프레임 보정 시간</param>
    /// <param name="currentYSpeed">현재 Y속도</param>
    public void CheckHighFall(float deltaTime, float currentYSpeed)
    {
        if (!isTracking) return;

        FallTimer += deltaTime;

        // 낙하 중 가장 낮은 Y속도 기록
        if (currentYSpeed < LowYSpeed)
        {
            LowYSpeed = currentYSpeed;
        }

        // 일정 시간 이상 낙하했고 하강 속도가 빠르면 높은 낙하로 판단
        if (!IsHighFall && FallTimer >= highFallTime && LowYSpeed <= highFallSpeed)
        {
            IsHighFall = true;
            Debug.Log("HighFallDetector isHighFalling true");
        }
    }

    /// <summary>
    /// 높은 낙하 판정 결과를 반환한 뒤 내부 상태 초기화
    /// </summary>
    /// <returns>높은 낙하 판정 결과</returns>
    public bool GetResultAndReset()
    {
        bool result = IsHighFall;
        ResetFall();
        return result;
    }

    /// <summary>
    /// 높은 낙하 판정에 사용한 누적값 초기화
    /// </summary>
    public void ResetFall()
    {
        isTracking = false;
        FallTimer = 0f;
        LowYSpeed = 0f;
        IsHighFall = false;
    }
}