using Unity.Cinemachine;
using UnityEngine;
/// <summary>
/// 마우스 입력을 기반으로 클로즈 백뷰 카메라 회전을 처리하는 컨트롤러 
/// yawTarget, pitchTarget, cameraSocket을 분리해 좌우 회전, 상하 회전
/// 실제 추적 위치를 나누어서 관리함
/// </summary>
public class CameraController : MonoBehaviour
{
    // 카메라 기준 좌우 회전 타겟
    [SerializeField] private Transform yawTarget;

    // 카메라 기준 상하 회전 타겟
    [SerializeField] private Transform pitchTarget;

    // Cinemachine카메라가 실제로 추적할 위치
    [SerializeField] private Transform cameraSocket;

    // 플레이어를 추적하는 Cinemachine 카메라
    [SerializeField] private CinemachineCamera cinemachineCamera;

    // 마우스 Look 입력을 가져오기 위한 입력 처리 컴포넌트
    [SerializeField] private InputHandler inputHandler;

    [SerializeField] private float mouseSensitivity = 0.08f;
    [SerializeField] private float minPitch = -25f;
    [SerializeField] private float maxPitch = 60f;
    [SerializeField] private bool useMouseLook = true;

    private float yaw;
    private float pitch;

    private void Reset()
    {
        FindReferences();
    }

    private void Awake()
    {
        FindReferences();
    }

    private void Start()
    {
        BindCameraTarget();
        InitCameraRotation();

        PlayerController playerController = GetComponent<PlayerController>();

        if (playerController != null && Camera.main != null)
        {
            // MovementController에서 카메라 기준 이동 방향을 계산할 수 있도록
            // 메인 카메라 Transform 전달함
            playerController.SetCameraTransform(Camera.main.transform);
        }
    }

    private void LateUpdate()
    {
        if (!useMouseLook) return;
        if (inputHandler == null || yawTarget == null) return;

        Vector2 lookInput = inputHandler.Look;

        // 마우스 입력을 누적하여 좌우 yaw와 상하 pitch 값 계산
        yaw += lookInput.x * mouseSensitivity;
        pitch -= lookInput.y * mouseSensitivity;

        // 상하 회전은 지정한 각도 범위 안에서 제한
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // yawTarget은 좌우 회전만 담당
        yawTarget.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (pitchTarget != null)
        {
            // pitchTarget은 상하 회전만 담당
            pitchTarget.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    /// <summary>
    /// 카메라 제어에 필요한 참조를 자동으로 검색함
    /// 인스펙터에서 직접 연결 하지 않아도 기본 구조를 찾을 수 있도록 보조
    /// </summary>
    private void FindReferences()
    {
        if (inputHandler == null)
        {
            inputHandler = GetComponent<InputHandler>();
        }

        if (cinemachineCamera == null)
        {
            cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
        }

        if (yawTarget == null)
        {
            yawTarget = transform.Find("TrackingCamera_CloseBackView");
        }

        if (pitchTarget == null && yawTarget != null)
        {
            pitchTarget = yawTarget.Find("CameraPitchTarget");
        }

        if (pitchTarget == null)
        {
            pitchTarget = transform.Find("CameraPitchTarget");
        }

        if (cameraSocket == null && pitchTarget != null)
        {
            cameraSocket = pitchTarget.Find("CameraSocket");
        }
    }

    /// <summary>
    /// CinemachineCamera의 TrackingTarget을 cameraSocket에 연결
    /// 회전 기준 타겟과 실제 카메라 추적 위치를 분리해 카메라 구조를 안정적으로 유지
    /// </summary>
    private void BindCameraTarget()
    {
        FindReferences();

        if (yawTarget == null)
        {
            Debug.LogWarning("yawTarget을 찾지 못함");
            return;
        }

        if (pitchTarget == null)
        {
            Debug.LogWarning("pitchTarget을 찾지 못함");
            return;
        }

        if (cameraSocket == null)
        {
            Debug.LogWarning("cameraSocket을 찾지 못함");
            return;
        }

        if (cinemachineCamera == null)
        {
            Debug.LogWarning("시네머신 카메라를 찾지 못함");
            return;
        }

        cinemachineCamera.Target.TrackingTarget = cameraSocket;
    }

    /// <summary>
    /// 현재 카메라 타겟 회전값을 기준으로 yaw, pitch 초기값 설정
    /// 플레이 시작시 카메라 각도가 갑자기 튀지 않도록 보정
    /// </summary>
    private void InitCameraRotation()
    {
        if (yawTarget != null)
        {
            yaw = yawTarget.eulerAngles.y;
        }

        if (pitchTarget != null)
        {
            pitch = NormalizeAngle(pitchTarget.eulerAngles.x);
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
    }

    /// <summary>
    /// 0도에서 360도 범위의 각도를 -180도에서 180도 범위로 변환
    /// pitch 제한 계산을 안정적으로 처리하기 위해 사용
    /// </summary>
    /// <param name="angle">변환할 각도</param>
    /// <returns>-180도에서 180도 범위로 변환된 각도</returns>
    private float NormalizeAngle(float angle)
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }
}