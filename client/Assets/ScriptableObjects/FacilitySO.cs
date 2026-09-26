using UnityEngine;

namespace PixelCleaners
{
    public enum FacilityType
    {
        PollutionCollector,
        DissolutionRefinery,
        ForgingRefinery,
        CompressionRefinery,
        PixelReconstructor
    }

    public enum ResourceType
    {
        // 기초 자원
        Garbage,            // 쓰레기 — 오염 집합소

        // 1차 가공 자원
        Plastic,            // 플라스틱 — 용해 정제소 A
        Glass,              // 유리 — 용해 정제소 B
        Metal,              // 금속 — 단조 정제소 A
        Can,                // 캔 — 단조 정제소 B
        Paper,              // 종이 — 압축 정제소 A
        Textile,            // 섬유 — 압축 정제소 B
        PixelFragment,      // 픽셀 파편 — 픽셀 재구성소

        // 2차 가공 자원
        RecycledComposite,  // 재생 복합재 — 합성 작업소 1
        RecycledAlloy,      // 재생 합금 — 합성 작업소 2

        // 제작 출력물
        DeliveryItem,        // 납품 물품 — 고급 제작소 "납품 패키지"
        AdvancedDeliveryItem // 고급 납품 물품 — 고급 제작소 "고급 납품 패키지"
        // 새 값은 항상 맨 뒤에 추가 (세이브는 이름으로 저장하지만 순서 의존 코드 대비)
    }

    [CreateAssetMenu(fileName = "New Facility", menuName = "PixelCleaners/Facility")]
    public class FacilitySO : ScriptableObject
    {
        public FacilityType  facilityType;
        public StatType      requiredStat;
        public float         baseCycleSeconds = 60f;
        public ResourceType  outputResource;
        public ResourceType  secondaryOutput;  // 정제소 두 번째 선택지
        public bool          hasChoice;        // 두 가지 생산 선택 가능
        public bool          pixelExclusiveOnly; // 픽셀 전용 생명체(제로픽셀)만 배치 가능 — 픽셀 재구성소
        public Sprite        icon;
    }
}
