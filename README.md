# Unity Robot Arm Controller

3-DOF 로봇 암 컨트롤러 with **Four-bar Parallel Linkage** 메커니즘

## 개요

Lynxmotion 3-DOF 로봇 암 구조를 참고하여 구현한 Unity용 로봇 암 컨트롤러입니다.
평행 링키지(Four-bar linkage) 메커니즘으로 엔드 이펙터가 **항상 수평을 유지**합니다.

## 특징

- **3-DOF 구조**: Base, Shoulder, Elbow
- **평행 링키지**: 엔드 이펙터 자동 수평 유지
- **패시브 조인트 시스템**: 링크 연동 지원
- **커플링 설정**: Elbow-Shoulder 연동 비율 조절

## 구조

```
Base (수평 회전)
  └── Shoulder (팔 확장/수축)
        └── Elbow (팔꿈치)
              └── EndEffector (자동 수평 유지)
```

## 사용법

### 1. 컴포넌트 추가
로봇 암 루트 오브젝트에 `RobotArmController` 컴포넌트를 추가합니다.

### 2. Transform 할당

| 필드 | 설명 | 회전 축 |
|---|---|---|
| `baseJoint` | 베이스 회전 | Y축 |
| `shoulderJoint` | 어깨 관절 | X축 또는 Z축 |
| `elbowJoint` | 팔꿈치 관절 | X축 또는 Z축 |
| `endEffectorJoint` | 그리퍼/손목 | X축 또는 Z축 |

### 3. 평행 링키지 설정

- `enableParallelLinkage`: 활성화 시 엔드 이펙터 자동 수평 유지
- `endEffectorOffset`: 미세 조정용 오프셋 각도

### 4. 패시브 조인트 (선택)

병렬 링크 구조의 경우 `passiveJoints` 리스트에 추가:
- `driver`: 연동할 주 조인트 선택
- `multiplier`: 연동 비율 (1.0 또는 -1.0)

## 수평 유지 원리

```csharp
// Four-bar 링키지 공식
EndEffector = -(Shoulder + Elbow) + offset
```

## 참고 자료

- [Lynxmotion 3-DOF Arm](https://wiki.lynxmotion.com/info/wiki/lynxmotion/view/ses-v2/ses-v2-arms/lss-3-dof-arm/)
- [Building a 4-DoF Serial Robotic Arm](https://blog.quakelogic.net/building-a-4-dof-serial-robotic-arm-with-smart-motion-devices/)

## 라이선스

MIT License
