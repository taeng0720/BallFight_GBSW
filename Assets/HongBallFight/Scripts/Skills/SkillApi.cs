using System;
using System.Collections.Generic;
using System.Linq;

namespace HongBallFight
{
    public sealed class SkillApi
    {
        public string Name, Signature, Description, Example, Category;
        public int Arguments;
        SkillApi(string name, string parameters, string description, string example, string category)
        {
            Name = name; Signature = name + "(" + parameters + ")"; Description = description; Example = example; Category = category;
            Arguments = string.IsNullOrEmpty(parameters) ? 0 : parameters.Split(',').Length;
        }
        public static readonly SkillApi[] All =
        {
            new SkillApi("Fire", "image, speed, damage", "적을 향해 이미지 발사. 발사체 ID 반환", "Fire(\"orb\", 360, 12)", "발사"),
            new SkillApi("FireAngle", "image, angle, speed, damage", "각도 지정 발사. 0도=오른쪽, 90도=아래", "FireAngle(\"orb\", 45, 360, 12)", "발사"),
            new SkillApi("FireSpread", "image, count, spread, speed, damage", "적 방향으로 부채꼴 발사. 마지막 ID 반환", "FireSpread(\"orb\", 5, 60, 320, 8)", "발사"),
            new SkillApi("FireRing", "image, count, speed, damage", "360도 원형 발사", "FireRing(\"orb\", 12, 280, 6)", "발사"),
            new SkillApi("Spawn", "image, x, y, size, lifetime", "위치에 이미지 생성. ID 반환", "Spawn(\"orb\", Self.X, Self.Y, 24, 5)", "생성"),
            new SkillApi("Orbit", "image, count, radius, degreesPerSecond, damage, lifetime", "자신 주변을 도는 구 생성", "Orbit(\"orb\", 6, 80, 120, 8, 10)", "생성"),
            new SkillApi("LaunchOrbitals", "speed", "회전 중인 구를 적을 향해 발사", "LaunchOrbitals(400)", "발사"),
            new SkillApi("Destroy", "id", "발사체 제거", "Destroy(1)", "오브젝트"),
            new SkillApi("DestroyAll", "", "자신이 만든 발사체 모두 제거", "DestroyAll()", "오브젝트"),
            new SkillApi("Exists", "id", "오브젝트 존재 여부", "Exists(1)", "오브젝트"),
            new SkillApi("SetVelocity", "id, x, y", "발사체 속도 설정. 회전 상태 해제", "SetVelocity(1, 250, -100)", "오브젝트"),
            new SkillApi("SetPosition", "id, x, y", "발사체 위치 설정", "SetPosition(1, 200, 200)", "오브젝트"),
            new SkillApi("SetSize", "id, size", "발사체 지름과 충돌 크기 설정", "SetSize(1, 36)", "오브젝트"),
            new SkillApi("SetDamage", "id, damage", "발사체 접촉 피해 설정", "SetDamage(1, 20)", "오브젝트"),
            new SkillApi("SetLifetime", "id, seconds", "발사체 남은 수명 설정", "SetLifetime(1, 8)", "오브젝트"),
            new SkillApi("SetHoming", "id, strength", "유도 강도 0~10. 0은 해제", "SetHoming(1, 3)", "오브젝트"),
            new SkillApi("SetBounce", "id, count", "벽에서 반사할 횟수", "SetBounce(1, 3)", "오브젝트"),
            new SkillApi("SetPiercing", "id, count", "추가 관통 횟수. 적 재타격 간격 0.35초", "SetPiercing(1, 2)", "오브젝트"),
            new SkillApi("SetRotation", "id, degrees", "이미지 회전 각도", "SetRotation(1, 90)", "오브젝트"),
            new SkillApi("SetSpin", "id, degreesPerSecond", "이미지의 초당 회전 속도", "SetSpin(1, 180)", "오브젝트"),
            new SkillApi("SetTint", "id, r, g, b, a", "이미지 색상 곱하기. 각 성분 0~1", "SetTint(1, 1, 0.5, 0.2, 1)", "이미지"),
            new SkillApi("SetImage", "id, image", "발사체 이미지 교체", "SetImage(1, \"star\")", "이미지"),
            new SkillApi("GetX", "id", "발사체 X 좌표", "GetX(1)", "조회"),
            new SkillApi("GetY", "id", "발사체 Y 좌표", "GetY(1)", "조회"),
            new SkillApi("GetCount", "", "자신의 발사체 수", "GetCount()", "조회"),
            new SkillApi("AimAngle", "", "자신에서 적을 향한 각도", "AimAngle()", "조회"),
            new SkillApi("EnemyDistance", "", "자신과 적의 거리", "EnemyDistance()", "조회"),
            new SkillApi("Explode", "x, y, radius, damage", "범위 피해와 짧은 폭발 이미지 생성", "Explode(Self.X, Self.Y, 150, 20)", "전투"),
            new SkillApi("Damage", "amount", "적에게 즉시 피해", "Damage(Self.SkillPower)", "전투"),
            new SkillApi("Heal", "amount", "자신 회복. 최대 HP 이내", "Heal(20)", "전투"),
            new SkillApi("Shield", "amount", "자신 보호막 설정. 중첩되지 않음", "Shield(30)", "전투"),
            new SkillApi("Dash", "speed", "적 방향으로 이동 속도 변경. SetSpeed로 복구", "Dash(450)", "이동"),
            new SkillApi("SetSpeed", "speed", "자신의 이동 속도 설정", "SetSpeed(250)", "이동"),
            new SkillApi("SetDirection", "angle", "자신의 이동 각도 설정", "SetDirection(90)", "이동"),
            new SkillApi("Teleport", "x, y", "경기장 안으로 순간이동", "Teleport(200, 200)", "이동"),
            new SkillApi("SetSprite", "image", "캐릭터 표시 이미지 교체", "SetSprite(\"hero\")", "이미지"),
            new SkillApi("CreateImage", "name, width, height, r, g, b, a", "RGBA 단색 이미지 생성. 크기 최대 128. 같은 이름은 유지", "CreateImage(\"orb\", 32, 32, 1, 0.5, 0.1, 1)", "이미지"),
            new SkillApi("SetPixel", "name, x, y, r, g, b, a", "생성한 이미지의 픽셀 수정", "SetPixel(\"orb\", 10, 10, 1, 1, 1, 1)", "이미지"),
            new SkillApi("DrawCircle", "name, x, y, radius, r, g, b, a", "생성한 이미지에 원 그리기", "DrawCircle(\"orb\", 16, 16, 14, 1, 0.4, 0.1, 1)", "이미지"),
            new SkillApi("DrawRect", "name, x, y, width, height, r, g, b, a", "생성한 이미지에 사각형 그리기", "DrawRect(\"orb\", 4, 4, 24, 24, 1, 1, 0, 1)", "이미지"),
            new SkillApi("Every", "name, seconds", "이름별 타이머. 주기가 지나면 true", "Every(\"shot\", 0.8)", "시간"),
            new SkillApi("Random.Range", "min, max", "시드 기반 난수. 숫자 반환", "Random.Range(0, 360)", "수학"),
            new SkillApi("Mathf.Sin", "radians", "사인", "Mathf.Sin(Time.time)", "수학"),
            new SkillApi("Mathf.Cos", "radians", "코사인", "Mathf.Cos(Time.time)", "수학"),
            new SkillApi("Mathf.Abs", "value", "절댓값", "Mathf.Abs(-1)", "수학"),
            new SkillApi("Mathf.Sqrt", "value", "제곱근", "Mathf.Sqrt(9)", "수학"),
            new SkillApi("Mathf.Min", "a, b", "작은 값", "Mathf.Min(1, 2)", "수학"),
            new SkillApi("Mathf.Max", "a, b", "큰 값", "Mathf.Max(1, 2)", "수학"),
            new SkillApi("Mathf.Clamp", "value, min, max", "범위 제한", "Mathf.Clamp(20, 0, 10)", "수학"),
            new SkillApi("Mathf.Lerp", "a, b, t", "선형 보간. t는 0~1", "Mathf.Lerp(0, 100, 0.5)", "수학"),
            new SkillApi("Mathf.Floor", "value", "내림", "Mathf.Floor(1.5)", "수학"),
            new SkillApi("Mathf.Ceil", "value", "올림", "Mathf.Ceil(1.5)", "수학"),
            new SkillApi("Mathf.Pow", "value, power", "거듭제곱", "Mathf.Pow(2, 3)", "수학"),
            new SkillApi("Distance", "x1, y1, x2, y2", "두 위치 사이 거리", "Distance(Self.X, Self.Y, Enemy.X, Enemy.Y)", "수학"),
            new SkillApi("Log", "message", "테스트 콘솔에 출력", "Log(\"HP: \" + Self.HP)", "디버그")
        };
        public static SkillApi Find(string name) => All.FirstOrDefault(a => a.Name == name);
        public static IEnumerable<string> Complete(string prefix, IEnumerable<string> locals = null)
        {
            return All.Select(a => a.Name).Concat(SkillScript.Properties).Concat(SkillScript.Events)
                .Concat(new[] { "float", "int", "bool", "string", "void", "if", "else", "for", "while", "return", "true", "false", "break", "continue" })
                .Concat(locals ?? Array.Empty<string>()).Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Distinct().OrderBy(n => n).Take(10);
        }
    }

}
