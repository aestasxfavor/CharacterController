# 3D Character Controller

Unity와 C#으로 제작한 **3D 캐릭터 컨트롤러 개인 기술 데모**입니다.

3D 환경에서의 플레이어 이동과 상태 제어 구조를 직접 구현했으며, 입력·이동·점프·지면 판정·상태 관리 등의 기능을 역할별로 분리하여 구성했습니다.

## 프로젝트 정보

* **개발 기간:** 2026.04.01 ~ 2026.05.15
* **개발 환경:** Unity 6000.0.66f2 → 6000.3.15f1
* **언어:** C#
* **버전 관리:** GitHub
* **외부 리소스:** Mixamo 캐릭터 모델 및 애니메이션 에셋

## 주요 구현

* 카메라 기준 3인칭 이동
* 걷기 / 달리기
* 점프 / 낙하 / 착지
* 이동 방향 기반 캐릭터 회전
* Raycast 기반 지면 및 경사 판정
* `Idle`, `Move`, `Jump`, `Fall`, `Land` 상태 관리
* 낙하 조건에 따른 착지 상태 및 애니메이션 처리
* 3인칭 카메라 및 애니메이션 연동

## 컨트롤러 구조

```text
PlayerController
├─ InputHandler
├─ MovementController
├─ JumpController
├─ GroundDetector
├─ HighFallDetector
├─ PlayerStateMachine
├─ PlayerAnimationController
└─ CameraController
```

`PlayerController`를 중심으로 입력, 이동, 점프, 지면 판정, 상태 관리 등의 기능을 개별 모듈로 분리했습니다.

플레이어 행동은 `PlayerBaseState`를 기반으로 상태별 클래스로 분리하고, `PlayerStateMachine`에서 현재 상태와 상태 변경을 관리합니다.

```text
PlayerBaseState
├─ PlayerIdleState
├─ PlayerMoveState
├─ PlayerJumpState
├─ PlayerFallState
└─ PlayerLandState
```

## 구현 과정

초기에는 `Rigidbody` 기반으로 이동과 점프를 구현했으나, 실제 지형에서 테스트하면서 이동·회전·경사면·접지 처리에 필요한 보정 범위가 증가했습니다.

이후 플레이어 이동 방식을 `CharacterController` 기반으로 전환했으며, 기존에 분리해둔 입력 처리, 카메라 기준 이동 방향 계산, 점프·중력 처리, 지면 판정, 상태 관리 구조는 최대한 유지했습니다.

낙하 및 착지 처리에서는 접지 여부만으로 상태를 판단하지 않고 낙하 시간, 수직 속도, Raycast 기반 지면 판정과 `CollisionFlags.Below`를 함께 확인하여 낮은 단차와 착지 연출이 필요한 낙하를 구분했습니다.

## 사용 리소스

캐릭터 모델 및 애니메이션은 **Mixamo** 리소스를 활용했습니다.

플레이어의 이동, 점프, 낙하, 지면 판정, 상태 전환 등 캐릭터 제어 로직은 직접 구현했습니다.

## 관련 링크

* **포트폴리오 문서:** https://river-goal-1ae.notion.site/3D-Character-Controller-3bf1270d8f2680f88036dcbada50ed4f
* **시연 영상:** https://youtu.be/CF7NiWzTbsY
